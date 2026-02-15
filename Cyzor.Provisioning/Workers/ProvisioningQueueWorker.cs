using Cyzor.Provisioning.Application.Pipeline;
using Cyzor.Provisioning.Queue;

namespace Cyzor.Provisioning.Workers;

public class ProvisioningQueueWorker : BackgroundService
{
    private readonly IProvisioningQueue _queue;
    private readonly ProvisionInstancePipeline _pipeline;

    public ProvisioningQueueWorker(
        IProvisioningQueue queue,
        ProvisionInstancePipeline pipeline)
    {
        _queue = queue;
        _pipeline = pipeline;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var instance = await _queue.DequeueAsync(stoppingToken);

            Console.WriteLine($"[QUEUE] Processing {instance.Id}");

            try
            {
                await _pipeline.ExecuteAsync(instance);
                Console.WriteLine($"[QUEUE] Finished {instance.Id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[QUEUE] Error processing {instance.Id}: {ex.Message}");
                try
                {
                    instance.SetState(Cyzor.Core.Domain.Enums.LifecycleState.Failed);
                }
                catch { }
            }
        }
    }
}
