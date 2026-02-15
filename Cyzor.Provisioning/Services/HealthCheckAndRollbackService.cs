using Cyzor.Core.Domain.Interfaces;
using Cyzor.Infrastructure.Services.Command;

namespace Cyzor.Provisioning.Services;

public interface IHealthCheckService
{
    Task<bool> CheckTenantHealthAsync(Guid tenantId, int port, int maxRetries = 5, int delayMs = 2000);
}

public interface IRollbackService
{
    Task RollbackTenantAsync(Guid tenantId);
}

public class HealthCheckService : IHealthCheckService
{
    private readonly HttpClient _httpClient;

    public HealthCheckService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public async Task<bool> CheckTenantHealthAsync(Guid tenantId, int port, int maxRetries = 5, int delayMs = 2000)
    {
        var tenantName = tenantId.ToString("N").Substring(0, 8);
        var healthUrl = $"http://localhost:{port}/";

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                Console.WriteLine($"[HEALTH] Checking tenant {tenantName} on port {port} (attempt {attempt}/{maxRetries})");
                var response = await _httpClient.GetAsync(healthUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[HEALTH] ✓ Tenant {tenantName} is healthy (port {port})");
                    return true;
                }

                Console.WriteLine($"[HEALTH] ⚠ Status {response.StatusCode} from {healthUrl}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HEALTH] ✗ Health check failed: {ex.Message}");
            }

            if (attempt < maxRetries)
            {
                await Task.Delay(delayMs);
            }
        }

        Console.WriteLine($"[HEALTH] ✗ Tenant {tenantName} failed health check after {maxRetries} attempts");
        return false;
    }
}

public class RollbackService : IRollbackService
{
    private readonly ICommandExecutor _executor;
    private readonly ITenantRepository _tenants;
    private readonly IAlertService _alerts;

    public RollbackService(ICommandExecutor executor, ITenantRepository tenants, IAlertService alerts)
    {
        _executor = executor;
        _tenants = tenants;
        _alerts = alerts;
    }

    public async Task RollbackTenantAsync(Guid tenantId)
    {
        var tenantName = tenantId.ToString("N").Substring(0, 8);
        
        try
        {
            Console.WriteLine($"[ROLLBACK] Starting rollback for tenant {tenantName}");

            // Kill PM2 app
            try
            {
                await _executor.ExecuteAsync($"pm2 delete {tenantName}");
                Console.WriteLine($"[ROLLBACK] Deleted PM2 app {tenantName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ROLLBACK] Warning: Failed to delete PM2 app: {ex.Message}");
            }

            // Remove tenant directory
            try
            {
                await _executor.ExecuteAsync($"rm -rf /var/www/{tenantName}");
                Console.WriteLine($"[ROLLBACK] Removed tenant directory {tenantName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ROLLBACK] Warning: Failed to remove directory: {ex.Message}");
            }

            // Update tenant state to Failed in database
            try
            {
                await _tenants.UpdateStateAsync(tenantId, "Failed");
                Console.WriteLine($"[ROLLBACK] Marked tenant {tenantName} as Failed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ROLLBACK] Warning: Failed to update DB: {ex.Message}");
            }

            // Send alert
            try
            {
                var tenant = await _tenants.GetByIdAsync(tenantId);
                var domain = tenant?.Domain ?? "unknown";
                var reason = "Health check failed during provisioning";
                await _alerts.SendRollbackAlertAsync(tenantId, domain, reason);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ROLLBACK] Warning: Failed to send alert: {ex.Message}");
            }

            Console.WriteLine($"[ROLLBACK] Completed rollback for tenant {tenantName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ROLLBACK] Error during rollback: {ex.Message}");
        }
    }
}
