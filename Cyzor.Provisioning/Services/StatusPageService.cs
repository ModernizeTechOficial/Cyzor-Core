using Cyzor.Infrastructure.Services.Command;

namespace Cyzor.Provisioning.Services;

public interface IStatusPageService
{
    Task<string> GenerateStatusPageAsync(string domain, Guid instanceId, string? message = null);
    Task RemoveStatusPageAsync(string domain);
}

public class StatusPageService : IStatusPageService
{
    private readonly ICommandExecutor _executor;

    public StatusPageService(ICommandExecutor executor)
    {
        _executor = executor;
    }

    public async Task<string> GenerateStatusPageAsync(string domain, Guid instanceId, string? message = null)
    {
        var statusDir = $"/var/www/cyzor/{domain}";
        var statusPage = $"{statusDir}/index.html";

        // Create directory
        await _executor.ExecuteAsync($"mkdir -p {statusDir}");

        // Generate HTML status page
        var html = GenerateHtml(domain, instanceId, message ?? "Configurando seu ambiente...");

        // Write file
        var writeCommand = $"cat > {statusPage} << 'EOF'\n{html}\nEOF";
        await _executor.ExecuteAsync(writeCommand);

        // Create a simple Nginx location block content for inclusion in main config
        // This will be used as a location block in the main nginx config
        Console.WriteLine($"[STATUS] Status page generated at {statusPage}");
        
        return statusPage;
    }

    public async Task RemoveStatusPageAsync(string domain)
    {
        try
        {
            var statusDir = $"/var/www/cyzor/{domain}";
            await _executor.ExecuteAsync($"rm -rf {statusDir}");
            Console.WriteLine($"[STATUS] Status page removed for {domain}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[STATUS] Warning: Failed to remove status page for {domain}: {ex.Message}");
            // Don't fail if removal fails
        }
    }

    private string GenerateHtml(string domain, Guid instanceId, string message)
    {
        return $@"<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Cyzor - Configurando seu ambiente</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}

        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 20px;
        }}

        .container {{
            background: white;
            border-radius: 12px;
            box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
            padding: 60px 40px;
            max-width: 600px;
            text-align: center;
        }}

        .logo {{
            font-size: 48px;
            margin-bottom: 30px;
        }}

        h1 {{
            color: #333;
            font-size: 32px;
            margin-bottom: 15px;
            font-weight: 600;
        }}

        .domain {{
            color: #667eea;
            font-size: 24px;
            margin-bottom: 30px;
            font-family: 'Courier New', monospace;
            word-break: break-all;
        }}

        .message {{
            color: #666;
            font-size: 16px;
            margin-bottom: 40px;
            line-height: 1.6;
        }}

        .spinner {{
            display: inline-block;
            width: 40px;
            height: 40px;
            border: 4px solid #f3f3f3;
            border-top: 4px solid #667eea;
            border-radius: 50%;
            animation: spin 1s linear infinite;
            margin-bottom: 30px;
        }}

        @keyframes spin {{
            0% {{ transform: rotate(0deg); }}
            100% {{ transform: rotate(360deg); }}
        }}

        .status-info {{
            background: #f8f9fa;
            border-left: 4px solid #667eea;
            padding: 15px;
            margin: 30px 0;
            text-align: left;
            border-radius: 4px;
        }}

        .status-info p {{
            color: #666;
            font-size: 14px;
            margin: 5px 0;
            font-family: 'Courier New', monospace;
        }}

        .status-label {{
            font-weight: 600;
            color: #333;
        }}

        .footer {{
            margin-top: 40px;
            padding-top: 20px;
            border-top: 1px solid #eee;
            color: #999;
            font-size: 12px;
        }}

        .progress-bar {{
            height: 4px;
            background: #f3f3f3;
            border-radius: 2px;
            overflow: hidden;
            margin: 20px 0;
        }}

        .progress-fill {{
            height: 100%;
            background: linear-gradient(90deg, #667eea 0%, #764ba2 100%);
            animation: progress 3s ease-in-out infinite;
        }}

        @keyframes progress {{
            0% {{ width: 0%; }}
            50% {{ width: 100%; }}
            100% {{ width: 0%; }}
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""logo"">🚀</div>
        
        <h1>Bem-vindo ao Cyzor</h1>
        
        <div class=""domain"">{domain}</div>
        
        <div class=""spinner""></div>
        
        <p class=""message"">{message}</p>
        
        <div class=""progress-bar"">
            <div class=""progress-fill""></div>
        </div>
        
        <div class=""status-info"">
            <p><span class=""status-label"">🔧 Status:</span> Provisioning em progresso</p>
            <p><span class=""status-label"">📦 Instância:</span> {instanceId}</p>
            <p><span class=""status-label"">🌐 Domínio:</span> {domain}</p>
            <p><span class=""status-label"">⏱️ Tempo:</span> <span id=""elapsed"">0s</span></p>
        </div>
        
        <p class=""message"" style=""font-size: 14px; color: #999;"">
            ⏳ Isso pode levar alguns minutos. Por favor, não feche esta página.
        </p>
        
        <div class=""footer"">
            <p>Cyzor Multi-tenant Provisioning Platform</p>
            <p id=""refresh"">Atualizando a cada 5 segundos...</p>
        </div>
    </div>

    <script>
        let elapsed = 0;
        
        // Update elapsed time
        setInterval(() => {{
            elapsed++;
            document.getElementById('elapsed').textContent = elapsed + 's';
        }}, 1000);
        
        // Auto-refresh page every 5 seconds to check if provisioning is complete
        setTimeout(() => {{
            location.reload();
        }}, 5000);
    </script>
</body>
</html>";
    }


    private async Task GenerateStatusNginxConfigAsync(string domain, string statusDir)
    {
        // Removed - status pages are now served via main nginx config
        // This method is no longer needed
        await Task.CompletedTask;
    }
}
