using Relativa.Persistence.Entities;

namespace Relativa.Core.Domain.Interfaces;

public interface IOrganizationSettingsRepository
{
    Task AddAsync(OrganizationSettings settings, CancellationToken ct = default);
}
