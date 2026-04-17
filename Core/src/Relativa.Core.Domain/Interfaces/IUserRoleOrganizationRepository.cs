using Relativa.Persistence.Entities;

namespace Relativa.Core.Domain.Interfaces;

public interface IUserRoleOrganizationRepository
{
    Task<UserRoleOrganization?> GetAsync(int userId, int organizationId, CancellationToken ct = default);
    Task<List<UserRoleOrganization>> GetByOrganizationIdAsync(int organizationId, CancellationToken ct = default);
    Task<List<UserRoleOrganization>> GetByUserIdAsync(int userId, CancellationToken ct = default);
    Task AddAsync(UserRoleOrganization member, CancellationToken ct = default);
    Task UpdateAsync(UserRoleOrganization member, CancellationToken ct = default);
    Task RemoveAsync(UserRoleOrganization member, CancellationToken ct = default);
}
