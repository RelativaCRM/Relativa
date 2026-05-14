using Relativa.Persistence.Entities;

namespace Relativa.Core.Domain.Interfaces;

public interface IPermissionRepository
{
    Task<List<Permission>> GetAllAsync(CancellationToken ct = default);
    Task<List<Permission>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default);
}
