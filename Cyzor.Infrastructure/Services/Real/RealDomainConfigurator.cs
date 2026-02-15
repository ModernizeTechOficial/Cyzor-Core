using Cyzor.Core.Configuration;
using Cyzor.Core.Domain.Interfaces;
using Cyzor.Core.Domain.Services;
using Microsoft.Extensions.Options;

namespace Cyzor.Infrastructure.Services.Real;

public class RealDomainConfigurator : IDomainConfigurator
{
    private readonly ITenantDeploymentService _deployment;
    private readonly IPortAllocator _portAllocator;

    public RealDomainConfigurator(
        ITenantDeploymentService deployment,
        IPortAllocator portAllocator)
    {
        _deployment = deployment;
        _portAllocator = portAllocator;
    }

    public async Task<int?> ConfigureAsync(Guid instanceId, string domain)
    {
        var tenantName = instanceId.ToString("N").Substring(0, 8);
        var port = _portAllocator.AllocatePort();

        Console.WriteLine($"[DOMAIN] Starting app {tenantName} on port {port}");
        await _deployment.StartApplicationAsync(tenantName, port);

        Console.WriteLine($"[DOMAIN] Verifying app {tenantName}");
        await _deployment.VerifyApplicationAsync(tenantName);

        return port;
    }
}
