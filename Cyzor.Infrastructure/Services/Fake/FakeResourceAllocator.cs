using Cyzor.Core.Domain.Interfaces;

namespace Cyzor.Infrastructure.Services.Fake;

public class FakeResourceAllocator : IResourceAllocator
{
    public async Task AllocateAsync(Guid instanceId)
    {
        Console.WriteLine($"[ALLOC] Resources allocated for {instanceId}");
        await Task.Delay(800);
    }
}
