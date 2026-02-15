namespace Cyzor.Core.Domain.Services;

public class PortAllocatorService : IPortAllocator
{
    private readonly HashSet<int> _allocatedPorts = new();
    private const int StartPort = 6000;
    private const int MaxPort = 7000;

    public int AllocatePort()
    {
        lock (_allocatedPorts)
        {
            for (int port = StartPort; port <= MaxPort; port++)
            {
                if (!_allocatedPorts.Contains(port))
                {
                    _allocatedPorts.Add(port);
                    return port;
                }
            }

            throw new InvalidOperationException("No available ports");
        }
    }

    public void ReleasePort(int port)
    {
        lock (_allocatedPorts)
        {
            _allocatedPorts.Remove(port);
        }
    }
}
