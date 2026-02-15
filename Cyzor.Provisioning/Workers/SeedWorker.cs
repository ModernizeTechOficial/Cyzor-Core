using Cyzor.Core.Domain.Entities;
using Cyzor.Provisioning.Queue;

namespace Cyzor.Provisioning.Workers;

public class SeedWorker : BackgroundService
{
    private readonly IProvisioningQueue _queue;

    public SeedWorker(IProvisioningQueue queue)
    {
        _queue = queue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(2000, stoppingToken);

        for (int i = 1; i <= 3; i++)
        {
            var instance = new Instance(Guid.NewGuid(), $"cliente{i}.cyzor.local");

            Console.WriteLine($"[SEED] Enqueuing {instance.Domain}");

            await _queue.EnqueueAsync(instance);
        }
    }
}
