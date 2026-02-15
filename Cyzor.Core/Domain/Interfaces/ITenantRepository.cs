using Cyzor.Core.Domain.Entities;

namespace Cyzor.Core.Domain.Interfaces;

public interface ITenantRepository
{
    Task CreateAsync(TenantRecord tenant);
    Task UpdateStateAsync(Guid id, string state);
    Task SetPortAsync(Guid id, int port);
    Task<TenantRecord?> GetByIdAsync(Guid id);
    Task<TenantRecord?> GetByDomainAsync(string domain);
}
