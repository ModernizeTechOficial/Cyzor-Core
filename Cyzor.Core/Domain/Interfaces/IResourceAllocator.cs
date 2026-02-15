namespace Cyzor.Core.Domain.Interfaces;

public interface IResourceAllocator
{
    Task AllocateAsync(Guid instanceId);
}
