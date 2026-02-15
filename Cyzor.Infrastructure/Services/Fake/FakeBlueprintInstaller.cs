using Cyzor.Core.Domain.Interfaces;

namespace Cyzor.Infrastructure.Services.Fake;

public class FakeBlueprintInstaller : IBlueprintInstaller
{
    public async Task InstallAsync(Guid instanceId)
    {
        Console.WriteLine($"[INSTALL] Blueprint installed for {instanceId}");
        await Task.Delay(800);
    }
}
