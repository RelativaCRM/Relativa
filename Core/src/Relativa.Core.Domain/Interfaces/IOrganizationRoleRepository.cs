using Relativa.Persistence.Entities;

namespace Relativa.Core.Domain.Interfaces;

public interface IOrganizationRoleRepository
{
    Task<OrganizationRole?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<OrganizationRole>> GetByOrganizationIdAsync(int organizationId, CancellationToken ct = default);
    Task<List<OrganizationRole>> GetSystemRolesAsync(CancellationToken ct = default);
    Task<OrganizationRole?> GetSystemRoleByNameAsync(string name, CancellationToken ct = default);
    Task AddAsync(OrganizationRole role, CancellationToken ct = default);
    Task UpdateAsync(OrganizationRole role, CancellationToken ct = default);
}
