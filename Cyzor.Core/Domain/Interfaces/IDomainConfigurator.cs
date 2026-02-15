namespace Cyzor.Core.Domain.Interfaces;

public interface IDomainConfigurator
{
    Task<int?> ConfigureAsync(Guid instanceId, string domain);
}
