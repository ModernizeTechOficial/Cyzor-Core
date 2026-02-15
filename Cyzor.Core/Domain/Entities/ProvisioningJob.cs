namespace Cyzor.Core.Domain.Entities;

public class ProvisioningJob
{
    public Guid Id { get; private set; }
    public Guid InstanceId { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public string? Error { get; private set; }

    public ProvisioningJob() { }

    public ProvisioningJob(Guid instanceId)
    {
        Id = Guid.NewGuid();
        InstanceId = instanceId;
        StartedAt = DateTime.UtcNow;
    }

    public void Complete()
    {
        FinishedAt = DateTime.UtcNow;
    }

    public void Fail(string error)
    {
        Error = error;
        FinishedAt = DateTime.UtcNow;
    }
}
