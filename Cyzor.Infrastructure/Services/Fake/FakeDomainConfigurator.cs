using Cyzor.Core.Domain.Interfaces;

namespace Cyzor.Infrastructure.Services.Fake;

public class FakeDomainConfigurator : IDomainConfigurator
{
    public async Task<int?> ConfigureAsync(Guid instanceId, string domain)
    {
        Console.WriteLine($"[DOMAIN] Configured {domain}");
        await Task.Delay(800);
        return null;
    }
}
