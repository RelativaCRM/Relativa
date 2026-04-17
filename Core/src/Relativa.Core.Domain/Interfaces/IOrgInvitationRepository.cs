using Relativa.Persistence.Entities;

namespace Relativa.Core.Domain.Interfaces;

public interface IOrgInvitationRepository
{
    Task<OrganizationInvitation?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<OrganizationInvitation?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task<List<OrganizationInvitation>> GetByOrganizationIdAsync(int organizationId, CancellationToken ct = default);
    Task<List<OrganizationInvitation>> GetByEmailAsync(string email, CancellationToken ct = default);
    Task AddAsync(OrganizationInvitation invitation, CancellationToken ct = default);
    Task UpdateAsync(OrganizationInvitation invitation, CancellationToken ct = default);
}
