using Cyzor.Core.Domain.Interfaces;

namespace Cyzor.Infrastructure.Services.Fake;

public class FakeEnvironmentProvisioner : IEnvironmentProvisioner
{
    public async Task CreateEnvironmentAsync(Guid instanceId)
    {
        Console.WriteLine($"[ENV] Environment created for {instanceId}");
        await Task.Delay(800);
    }
}
