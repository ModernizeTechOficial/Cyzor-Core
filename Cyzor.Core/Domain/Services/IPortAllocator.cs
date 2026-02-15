namespace Cyzor.Core.Domain.Services;

public interface IPortAllocator
{
    int AllocatePort();
    void ReleasePort(int port);
}
