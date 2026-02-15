using Cyzor.Core.Configuration;

namespace Cyzor.Infrastructure.Services.Real;

public interface ITenantDeploymentService
{
    Task CreateTenantDirectoryAsync(string tenantName);
    Task CopyAppBuildAsync(string tenantName, string appType);
    Task StartApplicationAsync(string tenantName, int port);
    Task VerifyApplicationAsync(string tenantName);
}
