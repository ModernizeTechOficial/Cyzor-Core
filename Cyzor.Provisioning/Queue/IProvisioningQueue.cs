using Cyzor.Core.Domain.Entities;

namespace Cyzor.Provisioning.Queue;

public interface IProvisioningQueue
{
    ValueTask EnqueueAsync(Instance instance);
    ValueTask<Instance> DequeueAsync(CancellationToken cancellationToken);
}
