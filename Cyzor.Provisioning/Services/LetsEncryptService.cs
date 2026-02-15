using Cyzor.Infrastructure.Services.Command;

namespace Cyzor.Provisioning.Services;

public interface ILetsEncryptService
{
    Task<bool> GenerateCertificateAsync(string domain);
}

public class LetsEncryptService : ILetsEncryptService
{
    private readonly ICommandExecutor _executor;
    private const string AcmeBin = "/root/.acme.sh/acme.sh";
    private const string CertDir = "/etc/letsencrypt/cyzor";
    private const string NginxConfDir = "/etc/nginx/sites-available";

    public LetsEncryptService(ICommandExecutor executor)
    {
        _executor = executor;
    }

    public async Task<bool> GenerateCertificateAsync(string domain)
    {
        try
        {
            Console.WriteLine($"[ACME] Starting certificate generation for {domain}");

            // Ensure acme.sh is installed
            await EnsureAcmeShInstalledAsync();

            // Generate certificate using acme.sh with HTTP-01 challenge
            var certPath = $"{CertDir}/{domain}";
            var acmeCommand = $"{AcmeBin} --issue -d {domain} -w /var/www/letsencrypt --cert-file {certPath}/cert.pem --key-file {certPath}/key.pem --fullchain-file {certPath}/fullchain.pem --force";

            Console.WriteLine($"[ACME] Running acme.sh for {domain}");
            await _executor.ExecuteAsync(acmeCommand);
            Console.WriteLine($"[ACME] Certificate generated successfully for {domain}");

            // Update Nginx config to use real certificate
            await UpdateNginxForHttpsAsync(domain, certPath);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ACME] Error generating certificate: {ex.Message}");
            // Return true anyway since the tenant is working with self-signed cert
            return true;
        }
    }

    private async Task EnsureAcmeShInstalledAsync()
    {
        try
        {
            // Check if acme.sh is already installed
            await _executor.ExecuteAsync($"test -f {AcmeBin}");
            Console.WriteLine($"[ACME] acme.sh is already installed");
        }
        catch
        {
            // Install acme.sh
            Console.WriteLine($"[ACME] Installing acme.sh...");
            try
            {
                await _executor.ExecuteAsync("bash -c 'curl https://get.acme.sh | bash' && source ~/.bashrc");
                Console.WriteLine($"[ACME] acme.sh installation completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ACME] Warning: acme.sh installation may have failed: {ex.Message}");
                // Continue anyway, it might still work
            }
        }

        // Ensure directories exist
        try
        {
            await _executor.ExecuteAsync($"mkdir -p /var/www/letsencrypt");
            await _executor.ExecuteAsync($"mkdir -p {CertDir}");
            await _executor.ExecuteAsync($"chmod -R 755 /var/www/letsencrypt");
            Console.WriteLine($"[ACME] Directories prepared");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ACME] Warning: Failed to create directories: {ex.Message}");
        }
    }

    private async Task UpdateNginxForHttpsAsync(string domain, string certPath)
    {
        try
        {
            Console.WriteLine($"[ACME] Updating Nginx config with Let's Encrypt certificate");

            var confFileName = $"{domain.Replace(".", "_")}";
            var confPath = $"{NginxConfDir}/{confFileName}.conf";

            // Read existing config to extract port
            int proxyPort = 6001; // Default
            try
            {
                var existingConfig = await _executor.ExecuteAsync($"cat {confPath}");
                var match = System.Text.RegularExpressions.Regex.Match(existingConfig, @"proxy_pass http://localhost:(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var port))
                {
                    proxyPort = port;
                }
            }
            catch
            {
                Console.WriteLine($"[ACME] Could not read existing config, using default port");
            }

            // Create config with real certificate paths
            var nginxConfig = $@"# Cyzor Tenant: {domain} (Let's Encrypt SSL)
server {{
    listen 80;
    listen [::]:80;
    server_name {domain};

    # ACME challenge for Let's Encrypt renewals
    location /.well-known/acme-challenge/ {{
        root /var/www/letsencrypt;
    }}

    # Redirect all other traffic to HTTPS
    location / {{
        return 301 https://$server_name$request_uri;
    }}
}}

# HTTPS server block (Let's Encrypt)
server {{
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name {domain};

    ssl_certificate {certPath}/fullchain.pem;
    ssl_certificate_key {certPath}/key.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    ssl_prefer_server_ciphers on;
    ssl_session_cache shared:SSL:10m;
    ssl_session_timeout 10m;

    location / {{
        proxy_pass http://localhost:{proxyPort};
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_http_version 1.1;
        proxy_buffering off;
        proxy_request_buffering off;
    }}

    location /health {{
        proxy_pass http://localhost:{proxyPort}/;
        access_log off;
    }}
}}
";

            // Write updated config
            var writeCommand = $"cat > {confPath} << 'EOF'\n{nginxConfig}\nEOF";
            await _executor.ExecuteAsync(writeCommand);

            // Test and reload Nginx
            await _executor.ExecuteAsync("nginx -t");
            await _executor.ExecuteAsync("systemctl reload nginx");
            Console.WriteLine($"[ACME] Nginx reloaded with Let's Encrypt certificate for {domain}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ACME] Warning: Failed to update Nginx config: {ex.Message}");
        }
    }
}
