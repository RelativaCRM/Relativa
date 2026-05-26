using Relativa.Core.Domain.Interfaces;
using Relativa.Core.Infrastructure.Data;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Infrastructure.Repositories;

public sealed class OrganizationSettingsRepository(RelativaDbContext db) : IOrganizationSettingsRepository
{
    public async Task AddAsync(OrganizationSettings settings, CancellationToken ct = default)
    {
        db.OrganizationSettings.Add(settings);
        await db.SaveChangesAsync(ct);
    }
}
