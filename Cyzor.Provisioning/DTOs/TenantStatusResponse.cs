using System.ComponentModel.DataAnnotations;

namespace Cyzor.Provisioning.DTOs;

public class TenantStatusResponse
{
    public Guid InstanceId { get; set; }
    public string Domain { get; set; } = default!;
    public string State { get; set; } = default!;
    public int? Port { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsHealthy { get; set; }
    public string? HealthStatus { get; set; }
}
