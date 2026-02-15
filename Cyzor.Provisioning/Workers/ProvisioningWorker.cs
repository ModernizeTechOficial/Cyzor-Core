using Cyzor.Core.Domain.Entities;
using Cyzor.Provisioning.Application.Pipeline;

namespace Cyzor.Provisioning.Workers;

public class ProvisioningWorker : BackgroundService
{
    private readonly ProvisionInstancePipeline _pipeline;

    public ProvisioningWorker(ProvisionInstancePipeline pipeline)
    {
        _pipeline = pipeline;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var instance = new Instance(Guid.NewGuid(), "cliente1.cyzor.local");
        await _pipeline.ExecuteAsync(instance);

        Console.WriteLine($"FINAL STATE: {instance.State}");
    }
}
