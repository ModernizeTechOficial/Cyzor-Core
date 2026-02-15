using Cyzor.Core.Configuration;
using Cyzor.Core.Domain.Interfaces;
using Cyzor.Core.Domain.Services;
using Microsoft.Extensions.Options;

namespace Cyzor.Infrastructure.Services.Real;

public class RealResourceAllocator : IResourceAllocator
{
    private readonly IPortAllocator _portAllocator;

    public RealResourceAllocator(IPortAllocator portAllocator)
    {
        _portAllocator = portAllocator;
    }

    public Task AllocateAsync(Guid instanceId)
    {
        var port = _portAllocator.AllocatePort();
        Console.WriteLine($"[ALLOC] Port {port} allocated for {instanceId}");
        return Task.CompletedTask;
    }
}
