namespace Cyzor.Provisioning.Services;

public interface IMetricsService
{
    void IncrementTenantProvisioned(string appType);
    void IncrementHealthCheckSuccess();
    void IncrementHealthCheckFailure();
    void IncrementRollback();
    void UpdateTenantState(string state, int count);
    string GetMetricsSnapshot();
}

public class PrometheusMetricsService : IMetricsService
{
    private readonly object _lockObj = new();
    private int _tenantsProvisioned = 0;
    private int _healthCheckSuccesses = 0;
    private int _healthCheckFailures = 0;
    private int _rollbacksExecuted = 0;
    private Dictionary<string, int> _tenantsByState = new();
    private Dictionary<string, int> _tenantsByAppType = new();

    public void IncrementTenantProvisioned(string appType)
    {
        lock (_lockObj)
        {
            _tenantsProvisioned++;
            if (!_tenantsByAppType.ContainsKey(appType))
                _tenantsByAppType[appType] = 0;
            _tenantsByAppType[appType]++;
        }
        Console.WriteLine($"[METRICS] Tenant provisioned (total: {_tenantsProvisioned}, type: {appType})");
    }

    public void IncrementHealthCheckSuccess()
    {
        lock (_lockObj)
        {
            _healthCheckSuccesses++;
        }
    }

    public void IncrementHealthCheckFailure()
    {
        lock (_lockObj)
        {
            _healthCheckFailures++;
        }
    }

    public void IncrementRollback()
    {
        lock (_lockObj)
        {
            _rollbacksExecuted++;
        }
        Console.WriteLine($"[METRICS] Rollback executed (total: {_rollbacksExecuted})");
    }

    public void UpdateTenantState(string state, int count)
    {
        lock (_lockObj)
        {
            _tenantsByState[state] = count;
        }
    }

    public string GetMetricsSnapshot()
    {
        lock (_lockObj)
        {
            var metrics = new System.Text.StringBuilder();
            metrics.AppendLine("# HELP cyzor_tenants_total Total tenants provisioned");
            metrics.AppendLine("# TYPE cyzor_tenants_total counter");
            metrics.AppendLine($"cyzor_tenants_total {_tenantsProvisioned}");
            
            metrics.AppendLine("# HELP cyzor_health_checks_success_total Successful health checks");
            metrics.AppendLine("# TYPE cyzor_health_checks_success_total counter");
            metrics.AppendLine($"cyzor_health_checks_success_total {_healthCheckSuccesses}");
            
            metrics.AppendLine("# HELP cyzor_health_checks_failure_total Failed health checks");
            metrics.AppendLine("# TYPE cyzor_health_checks_failure_total counter");
            metrics.AppendLine($"cyzor_health_checks_failure_total {_healthCheckFailures}");
            
            metrics.AppendLine("# HELP cyzor_rollbacks_total Total rollbacks executed");
            metrics.AppendLine("# TYPE cyzor_rollbacks_total counter");
            metrics.AppendLine($"cyzor_rollbacks_total {_rollbacksExecuted}");
            
            metrics.AppendLine("# HELP cyzor_tenants_by_state Tenants by lifecycle state");
            metrics.AppendLine("# TYPE cyzor_tenants_by_state gauge");
            foreach (var (state, count) in _tenantsByState)
            {
                metrics.AppendLine($"cyzor_tenants_by_state{{state=\"{state}\"}} {count}");
            }
            
            metrics.AppendLine("# HELP cyzor_tenants_by_app_type Tenants by application type");
            metrics.AppendLine("# TYPE cyzor_tenants_by_app_type gauge");
            foreach (var (appType, count) in _tenantsByAppType)
            {
                metrics.AppendLine($"cyzor_tenants_by_app_type{{type=\"{appType}\"}} {count}");
            }
            
            return metrics.ToString();
        }
    }
}
