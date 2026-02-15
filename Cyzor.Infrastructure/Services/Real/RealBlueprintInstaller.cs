using Cyzor.Core.Configuration;
using Cyzor.Core.Domain.Interfaces;
using Cyzor.Infrastructure.Services.Command;
using Microsoft.Extensions.Options;

namespace Cyzor.Infrastructure.Services.Real;

public class RealBlueprintInstaller : IBlueprintInstaller
{
    private readonly ITenantDeploymentService _deployment;
    private readonly ProvisioningSettings _settings;

    public RealBlueprintInstaller(
        ITenantDeploymentService deployment,
        IOptions<ProvisioningSettings> settings)
    {
        _deployment = deployment;
        _settings = settings.Value;
    }

    public async Task InstallAsync(Guid instanceId)
    {
        var tenantName = instanceId.ToString("N").Substring(0, 8);
        
        Console.WriteLine($"[INSTALL] Copying blueprint for {tenantName}");
        await _deployment.CopyAppBuildAsync(tenantName, _settings.AppType);
    }
}
