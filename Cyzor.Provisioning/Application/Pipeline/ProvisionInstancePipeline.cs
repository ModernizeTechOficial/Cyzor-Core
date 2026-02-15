using Cyzor.Core.Domain.Entities;
using Cyzor.Core.Domain.Enums;
using Cyzor.Core.Domain.Interfaces;
using Cyzor.Provisioning.Services;

namespace Cyzor.Provisioning.Application.Pipeline;

public class ProvisionInstancePipeline
{
    private readonly IResourceAllocator _allocator;
    private readonly IEnvironmentProvisioner _environment;
    private readonly IBlueprintInstaller _installer;
    private readonly IDomainConfigurator _domain;
    private readonly Cyzor.Core.Domain.Interfaces.ITenantRepository _tenants;
    private readonly IHealthCheckService _healthCheck;
    private readonly IRollbackService _rollback;
    private readonly INginxConfigService _nginx;
    private readonly ILetsEncryptService _letsEncrypt;
    private readonly IStatusPageService _statusPage;

    public ProvisionInstancePipeline(
        IResourceAllocator allocator,
        IEnvironmentProvisioner environment,
        IBlueprintInstaller installer,
        IDomainConfigurator domain,
        Cyzor.Core.Domain.Interfaces.ITenantRepository tenants,
        IHealthCheckService healthCheck,
        IRollbackService rollback,
        INginxConfigService nginx,
        ILetsEncryptService letsEncrypt,
        IStatusPageService statusPage)
    {
        _allocator = allocator;
        _environment = environment;
        _installer = installer;
        _domain = domain;
        _tenants = tenants;
        _healthCheck = healthCheck;
        _rollback = rollback;
        _nginx = nginx;
        _letsEncrypt = letsEncrypt;
        _statusPage = statusPage;
    }

    public async Task ExecuteAsync(Instance instance)
    {
        try
        {
            // Display status page immediately so user sees something
            try
            {
                await _statusPage.GenerateStatusPageAsync(instance.Domain, instance.Id, 
                    $"Iniciando provisioning do seu ambiente Cyzor...");
                Console.WriteLine($"[PIPELINE] Status page displayed for {instance.Domain}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PIPELINE] Warning: Failed to generate status page: {ex.Message}");
                // Continue even if status page fails
            }

            instance.SetState(LifecycleState.AllocatingResources);
            await _tenants.CreateAsync(new Cyzor.Core.Domain.Entities.TenantRecord
            {
                Id = instance.Id,
                Domain = instance.Domain,
                State = LifecycleState.AllocatingResources.ToString(),
                CreatedAt = DateTime.UtcNow
            });

            await _allocator.AllocateAsync(instance.Id);

            instance.SetState(LifecycleState.CreatingEnvironment);
            await _tenants.UpdateStateAsync(instance.Id, LifecycleState.CreatingEnvironment.ToString());
            await _environment.CreateEnvironmentAsync(instance.Id);

            instance.SetState(LifecycleState.InstallingBlueprint);
            await _tenants.UpdateStateAsync(instance.Id, LifecycleState.InstallingBlueprint.ToString());
            await _installer.InstallAsync(instance.Id);

            instance.SetState(LifecycleState.ConfiguringDomain);
            await _tenants.UpdateStateAsync(instance.Id, LifecycleState.ConfiguringDomain.ToString());
            var port = await _domain.ConfigureAsync(instance.Id, instance.Domain);
            if (port.HasValue)
            {
                await _tenants.SetPortAsync(instance.Id, port.Value);
            }

            // Health Check
            instance.SetState(LifecycleState.HealthChecking);
            await _tenants.UpdateStateAsync(instance.Id, LifecycleState.HealthChecking.ToString());
            
            if (!port.HasValue)
            {
                throw new InvalidOperationException("Port allocation failed");
            }

            var isHealthy = await _healthCheck.CheckTenantHealthAsync(instance.Id, port.Value);
            if (!isHealthy)
            {
                Console.WriteLine($"[PIPELINE] Health check failed for tenant {instance.Id}. Rolling back...");
                instance.SetState(LifecycleState.RollingBack);
                await _tenants.UpdateStateAsync(instance.Id, LifecycleState.RollingBack.ToString());
                await _rollback.RollbackTenantAsync(instance.Id);
                instance.SetState(LifecycleState.Failed);
                await _tenants.UpdateStateAsync(instance.Id, LifecycleState.Failed.ToString());
                throw new InvalidOperationException("Tenant failed health check");
            }

            // Remove status page now that app is healthy
            try
            {
                await _statusPage.RemoveStatusPageAsync(instance.Domain);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PIPELINE] Warning: Failed to remove status page: {ex.Message}");
            }

            // Nginx Configuration
            instance.SetState(LifecycleState.Finalizing);
            await _tenants.UpdateStateAsync(instance.Id, LifecycleState.Finalizing.ToString());
            try
            {
                await _nginx.GenerateAndReloadAsync(instance.Id, instance.Domain, port.Value);
                Console.WriteLine($"[PIPELINE] Nginx config generated for {instance.Domain}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PIPELINE] Warning: Nginx config generation failed: {ex.Message}");
                // Don't fail the entire provisioning, just warn
            }

            // SSL/TLS Certificate (Let's Encrypt)
            try
            {
                Console.WriteLine($"[PIPELINE] Starting Let's Encrypt certificate generation for {instance.Domain}");
                var certResult = await _letsEncrypt.GenerateCertificateAsync(instance.Domain);
                if (certResult)
                {
                    Console.WriteLine($"[PIPELINE] ✓ SSL certificate generated successfully for {instance.Domain}");
                }
                else
                {
                    Console.WriteLine($"[PIPELINE] ⚠ SSL certificate generation started but may be processing in background for {instance.Domain}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PIPELINE] Warning: SSL certificate generation failed: {ex.Message}");
                // Don't fail the entire provisioning, just warn
            }

            instance.SetState(LifecycleState.Running);
            await _tenants.UpdateStateAsync(instance.Id, LifecycleState.Running.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PIPELINE] Error executing pipeline for {instance.Id}: {ex.Message}");
            instance.SetState(LifecycleState.Failed);
            await _tenants.UpdateStateAsync(instance.Id, LifecycleState.Failed.ToString());
            throw;
        }
    }
}
