using Cyzor.Core.Domain.Enums;

namespace Cyzor.Core.Domain.Entities;

public class Instance
{
    public Guid Id { get; private set; }
    public Guid BlueprintId { get; private set; }
    public string Domain { get; private set; } = default!;
    public LifecycleState State { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public Instance() { }

    public Instance(Guid blueprintId, string domain)
    {
        Id = Guid.NewGuid();
        BlueprintId = blueprintId;
        Domain = domain;
        State = LifecycleState.Requested;
        CreatedAt = DateTime.UtcNow;
    }

    public void SetState(LifecycleState state)
    {
        State = state;
    }
}
