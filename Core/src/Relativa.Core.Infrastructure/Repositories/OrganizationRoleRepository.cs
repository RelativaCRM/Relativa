using Microsoft.EntityFrameworkCore;
using Relativa.Core.Domain.Interfaces;
using Relativa.Core.Infrastructure.Data;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Infrastructure.Repositories;

public sealed class OrganizationRoleRepository(RelativaDbContext db) : IOrganizationRoleRepository
{
    public async Task<OrganizationRole?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await db.OrganizationRoles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsArchived, ct);
    }

    public async Task<List<OrganizationRole>> GetByOrganizationIdAsync(int organizationId, CancellationToken ct = default)
    {
        return await db.OrganizationRoles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .Where(r => (r.OrganizationId == null || r.OrganizationId == organizationId) && !r.IsArchived)
            .ToListAsync(ct);
    }

    public async Task<OrganizationRole?> GetSystemRoleByNameAsync(string name, CancellationToken ct = default)
    {
        return await db.OrganizationRoles
            .FirstOrDefaultAsync(r => r.Name == name && r.OrganizationId == null && !r.IsArchived, ct);
    }

    public async Task AddAsync(OrganizationRole role, CancellationToken ct = default)
    {
        db.OrganizationRoles.Add(role);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(OrganizationRole role, CancellationToken ct = default)
    {
        db.OrganizationRoles.Update(role);
        await db.SaveChangesAsync(ct);
    }
}
