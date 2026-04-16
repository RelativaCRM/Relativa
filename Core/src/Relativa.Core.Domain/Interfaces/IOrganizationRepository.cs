using Relativa.Persistence.Entities;

namespace Relativa.Core.Domain.Interfaces;

public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<Organization>> GetByUserIdAsync(int userId, CancellationToken ct = default);
    Task<List<Organization>> SearchAsync(string query, CancellationToken ct = default);
    Task AddAsync(Organization organization, CancellationToken ct = default);
    Task UpdateAsync(Organization organization, CancellationToken ct = default);
}
