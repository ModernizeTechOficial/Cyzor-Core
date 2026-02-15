using Cyzor.Core.Domain.Interfaces;
using Cyzor.Infrastructure.Services.Command;

namespace Cyzor.Infrastructure.Services.Real;

public class LocalEnvironmentProvisioner : IEnvironmentProvisioner
{
    private readonly ICommandExecutor _executor;

    public LocalEnvironmentProvisioner(ICommandExecutor executor)
    {
        _executor = executor;
    }

    public async Task CreateEnvironmentAsync(Guid instanceId)
    {
        var tenantName = instanceId.ToString("N").Substring(0, 8);
        var command = $"mkdir -p /var/www/{tenantName}/publish";

        await _executor.ExecuteAsync(command);
    }
}
