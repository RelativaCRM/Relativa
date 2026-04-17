using Relativa.Persistence.Entities;

namespace Relativa.Core.Domain.Interfaces;

public interface IJoinRequestRepository
{
    Task<OrganizationJoinRequest?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<OrganizationJoinRequest>> GetByOrganizationIdAsync(int organizationId, CancellationToken ct = default);
    Task<List<OrganizationJoinRequest>> GetByUserIdAsync(int userId, CancellationToken ct = default);
    Task<OrganizationJoinRequest?> GetPendingAsync(int userId, int organizationId, CancellationToken ct = default);
    Task AddAsync(OrganizationJoinRequest request, CancellationToken ct = default);
    Task UpdateAsync(OrganizationJoinRequest request, CancellationToken ct = default);
}
