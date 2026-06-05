using Microsoft.EntityFrameworkCore;
using Relativa.Core.Domain.Interfaces;
using Relativa.Core.Infrastructure.Data;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Infrastructure.Repositories;

public sealed class OrganizationRepository(RelativaDbContext db) : IOrganizationRepository
{
    public async Task<Organization?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsArchived, ct);
    }

    public async Task<List<Organization>> GetByUserIdAsync(int userId, CancellationToken ct = default)
    {
        return await db.UserRoleOrganizations
            .Where(uro => uro.UserId == userId && !uro.IsArchived)
            .Select(uro => uro.Organization)
            .Where(o => !o.IsArchived)
            .ToListAsync(ct);
    }

    public async Task<List<OrganizationSearchHit>> SearchAsync(string query, CancellationToken ct = default)
    {
        var organizations = db.Organizations.Where(o => !o.IsArchived);

        var trimmed = query?.Trim();
        organizations = string.IsNullOrEmpty(trimmed)
            ? organizations.OrderByDescending(o => o.Members.Count(m => !m.IsArchived)).ThenBy(o => o.Name)
            : organizations.Where(o => EF.Functions.ILike(o.Name, $"%{trimmed}%")).OrderBy(o => o.Name);

        return await organizations
            .Take(20)
            .Select(o => new OrganizationSearchHit(
                o.Id,
                o.Name,
                o.Members.Count(m => !m.IsArchived),
                o.Settings != null ? o.Settings.JoinPolicy : "open"))
            .ToListAsync(ct);
    }

    public async Task AddAsync(Organization organization, CancellationToken ct = default)
    {
        db.Organizations.Add(organization);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Organization organization, CancellationToken ct = default)
    {
        db.Organizations.Update(organization);
        await db.SaveChangesAsync(ct);
    }
}
