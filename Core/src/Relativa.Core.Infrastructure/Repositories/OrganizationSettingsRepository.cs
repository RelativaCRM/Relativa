using Microsoft.EntityFrameworkCore;
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

    public async Task<OrganizationSettings?> GetByOrganizationIdAsync(int organizationId, CancellationToken ct = default)
        => await db.OrganizationSettings
            .Include(s => s.DefaultOrgRole)
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);

    public async Task UpdateAsync(OrganizationSettings settings, CancellationToken ct = default)
    {
        db.OrganizationSettings.Update(settings);
        await db.SaveChangesAsync(ct);
    }
}
