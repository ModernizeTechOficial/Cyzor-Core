namespace Cyzor.Core.Domain.Interfaces;

public interface IEnvironmentProvisioner
{
    Task CreateEnvironmentAsync(Guid instanceId);
}
