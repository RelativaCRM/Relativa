using Relativa.Persistence.Entities;

namespace Relativa.Core.Domain.Interfaces;

public interface IOrganizationSettingsRepository
{
    Task AddAsync(OrganizationSettings settings, CancellationToken ct = default);
    Task<OrganizationSettings?> GetByOrganizationIdAsync(int organizationId, CancellationToken ct = default);
    Task UpdateAsync(OrganizationSettings settings, CancellationToken ct = default);
}
