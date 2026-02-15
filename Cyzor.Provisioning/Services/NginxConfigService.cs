using Cyzor.Infrastructure.Services.Command;

namespace Cyzor.Provisioning.Services;

public interface INginxConfigService
{
    Task GenerateAndReloadAsync(Guid tenantId, string domain, int port);
}

public class NginxConfigService : INginxConfigService
{
    private readonly ICommandExecutor _executor;
    private const string NginxConfDir = "/etc/nginx/sites-available";
    private const string NginxSitesEnabled = "/etc/nginx/sites-enabled";
    private const string NginxConfD = "/etc/nginx/conf.d";

    public NginxConfigService(ICommandExecutor executor)
    {
        _executor = executor;
    }

    public async Task GenerateAndReloadAsync(Guid tenantId, string domain, int port)
    {
        var tenantName = tenantId.ToString("N").Substring(0, 8);
        var confFileName = $"{domain.Replace(".", "_")}";
        var confPath = $"{NginxConfDir}/{confFileName}.conf";
        var enabledPath = $"{NginxSitesEnabled}/{confFileName}.conf";

        try
        {
            Console.WriteLine($"[NGINX] Generating config for {domain} -> localhost:{port}");

            // Create Nginx config
            var nginxConfig = GenerateNginxConfig(domain, port);

            // Write config file
            var writeCommand = $"cat > {confPath} << 'EOF'\n{nginxConfig}\nEOF";
            await _executor.ExecuteAsync(writeCommand);
            Console.WriteLine($"[NGINX] Config written to {confPath}");

            // Enable site (create symlink)
            try
            {
                await _executor.ExecuteAsync($"ln -sf {confPath} {enabledPath}");
                Console.WriteLine($"[NGINX] Site enabled at {enabledPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NGINX] Warning: Failed to enable site: {ex.Message}");
            }

            // Update central dynamic proxy (map) so host routing wins over defaults
            try
            {
                await UpdateDynamicProxyAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NGINX] Warning: Failed to update dynamic proxy: {ex.Message}");
            }

            // Test config
            try
            {
                await _executor.ExecuteAsync("nginx -t");
                Console.WriteLine($"[NGINX] Config test passed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NGINX] Warning: Config test failed: {ex.Message}");
                throw;
            }

            // Reload Nginx
            try
            {
                await _executor.ExecuteAsync("systemctl reload nginx");
                Console.WriteLine($"[NGINX] Reloaded successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NGINX] Warning: Failed to reload nginx: {ex.Message}");
                throw;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NGINX] Error generating config: {ex.Message}");
            throw;
        }
    }

    private string GenerateNginxConfig(string domain, int port)
    {
        var template = @"# Cyzor Tenant: {DOMAIN}
server {
    listen 80;
    listen [::]:80;
    server_name {DOMAIN};

    # ACME challenge for Let's Encrypt
    location /.well-known/acme-challenge/ {
        root /var/www/letsencrypt;
    }

    # Redirect all other traffic to HTTPS
    location / {
        return 301 https://$server_name$request_uri;
    }
}

# HTTPS server block (with self-signed cert initially, will be updated by Let's Encrypt)
server {
    listen 443 ssl http2;
    listen [::]:443 ssl http2;
    server_name {DOMAIN};

    # Use self-signed cert initially (will be replaced by Let's Encrypt)
    ssl_certificate /etc/ssl/certs/ssl-cert-snakeoil.pem;
    ssl_certificate_key /etc/ssl/private/ssl-cert-snakeoil.key;
    
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers HIGH:!aNULL:!MD5;
    ssl_prefer_server_ciphers on;

    location / {
        proxy_pass http://localhost:{PORT};
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_http_version 1.1;
        proxy_buffering off;
        proxy_request_buffering off;
    }

    location /health {
        proxy_pass http://localhost:{PORT}/;
        access_log off;
    }
}
";

        return template.Replace("{DOMAIN}", domain).Replace("{PORT}", port.ToString());
    }

    private async Task UpdateDynamicProxyAsync()
    {
        var target = "/etc/nginx/conf.d/cyzor_dynamic_proxy.conf";

        // Gather list of site files
        var listOutput = await _executor.ExecuteAsync("bash -lc \"printf '%s\\n' /etc/nginx/sites-available/*.conf 2>/dev/null || true\"");
        var files = listOutput?.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

        var mappings = new System.Collections.Generic.List<(string host, string port)>();

        foreach (var file in files)
        {
            if (string.IsNullOrEmpty(file)) continue;
            try
            {
                var host = (await _executor.ExecuteAsync($"bash -lc \"grep -Po 'server_name\\s+\\K\\S+' {file} | head -1\""))?.Trim();
                var port = (await _executor.ExecuteAsync($"bash -lc \"grep -Po 'proxy_pass\\s+http://(?:localhost|127\\.0\\.0\\.1):\\K\\d+' {file} | head -1\""))?.Trim();

                if (!string.IsNullOrEmpty(host) && !string.IsNullOrEmpty(port))
                {
                    mappings.Add((host, port));
                    Console.WriteLine($"[NGINX] Found mapping: {host} -> {port}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NGINX] Warning: failed to parse {file}: {ex.Message}");
            }
        }

        Console.WriteLine($"[NGINX] Total mappings found: {mappings.Count}");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Auto-generated cyzor proxy mappings");
        sb.AppendLine("map $http_host $backend_pool {");
        foreach (var m in mappings)
        {
            sb.AppendLine($"    {m.host} {m.port};");
        }
        sb.AppendLine("    default 6001;");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# Fallback server for tenants");
        sb.AppendLine("server {");
        sb.AppendLine("    listen 80 default_server;");
        sb.AppendLine("    listen [::]:80 default_server;");
        sb.AppendLine("    server_name ~^.*\\.cyzor\\.com\\.br$ localhost;");
        sb.AppendLine();
        sb.AppendLine("    # Proxy to backend using dynamic mapping");
        sb.AppendLine("    location / {");
        sb.AppendLine("        proxy_pass http://127.0.0.1:$backend_pool;");
        sb.AppendLine("        proxy_set_header Host $host;");
        sb.AppendLine("        proxy_set_header X-Real-IP $remote_addr;");
        sb.AppendLine("        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;");
        sb.AppendLine("        proxy_set_header X-Forwarded-Proto $scheme;");
        sb.AppendLine("        proxy_http_version 1.1;");
        sb.AppendLine("        proxy_buffering off;");
        sb.AppendLine("        proxy_connect_timeout 30s;");
        sb.AppendLine("        proxy_send_timeout 30s;");
        sb.AppendLine("        proxy_read_timeout 30s;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# HTTPS fallback server");
        sb.AppendLine("server {");
        sb.AppendLine("    listen 443 ssl http2 default_server;");
        sb.AppendLine("    listen [::]:443 ssl http2 default_server;");
        sb.AppendLine("    server_name ~^.*\\.cyzor\\.com\\.br$ localhost;");
        sb.AppendLine();
        sb.AppendLine("    # Use self-signed cert as fallback");
        sb.AppendLine("    ssl_certificate /etc/ssl/certs/ssl-cert-snakeoil.pem;");
        sb.AppendLine("    ssl_certificate_key /etc/ssl/private/ssl-cert-snakeoil.key;");
        sb.AppendLine();
        sb.AppendLine("    ssl_protocols TLSv1.2 TLSv1.3;");
        sb.AppendLine("    ssl_ciphers HIGH:!aNULL:!MD5;");
        sb.AppendLine("    ssl_prefer_server_ciphers on;");
        sb.AppendLine();
        sb.AppendLine("    location / {");
        sb.AppendLine("        proxy_pass http://127.0.0.1:$backend_pool;");
        sb.AppendLine("        proxy_set_header Host $host;");
        sb.AppendLine("        proxy_set_header X-Real-IP $remote_addr;");
        sb.AppendLine("        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;");
        sb.AppendLine("        proxy_set_header X-Forwarded-Proto $scheme;");
        sb.AppendLine("        proxy_http_version 1.1;");
        sb.AppendLine("        proxy_buffering off;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        var content = sb.ToString();

        var writeCmd = new System.Text.StringBuilder();
        writeCmd.AppendLine($"cat > {target} <<'EOF'");
        writeCmd.AppendLine(content);
        writeCmd.AppendLine("EOF");

        await _executor.ExecuteAsync(writeCmd.ToString());
        Console.WriteLine($"[NGINX] Dynamic proxy written to {target}");
    }
}