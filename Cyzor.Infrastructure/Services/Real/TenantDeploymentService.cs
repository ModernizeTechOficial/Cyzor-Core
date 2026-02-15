using Cyzor.Infrastructure.Services.Command;

namespace Cyzor.Infrastructure.Services.Real;

public class TenantDeploymentService : ITenantDeploymentService
{
    private readonly ICommandExecutor _executor;

    public TenantDeploymentService(ICommandExecutor executor)
    {
        _executor = executor;
    }

    public async Task CreateTenantDirectoryAsync(string tenantName)
    {
        var command = $"mkdir -p /var/www/{tenantName}/publish";
        await _executor.ExecuteAsync(command);
    }

    public async Task CopyAppBuildAsync(string tenantName, string appType)
    {
        var command = $"cp -r /var/www/builds/{appType}/* /var/www/{tenantName}/publish/";
        await _executor.ExecuteAsync(command);
    }

    public async Task StartApplicationAsync(string tenantName, int port)
    {
        var command = $"bash -c 'cd /var/www/{tenantName}/publish && PORT={port} pm2 start server.js --name {tenantName} --watch && pm2 save'";
        await _executor.ExecuteAsync(command);
    }

    public async Task VerifyApplicationAsync(string tenantName)
    {
        var command = $"pm2 status {tenantName}";
        await _executor.ExecuteAsync(command);
    }
}
