using System.Threading.Channels;
using Cyzor.Core.Domain.Entities;

namespace Cyzor.Provisioning.Queue;

public class InMemoryProvisioningQueue : IProvisioningQueue
{
    private readonly Channel<Instance> _queue = Channel.CreateUnbounded<Instance>();

    public async ValueTask EnqueueAsync(Instance instance)
    {
        await _queue.Writer.WriteAsync(instance);
    }

    public async ValueTask<Instance> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}
