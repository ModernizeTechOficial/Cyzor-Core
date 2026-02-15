namespace Cyzor.Core.Domain.Entities;

public class TenantRecord
{
    public Guid Id { get; set; }
    public string Domain { get; set; } = default!;
    public string State { get; set; } = default!;
    public int? Port { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
