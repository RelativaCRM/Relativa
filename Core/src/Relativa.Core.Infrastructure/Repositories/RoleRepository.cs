using Microsoft.EntityFrameworkCore;
using Relativa.Core.Domain.Interfaces;
using Relativa.Core.Infrastructure.Data;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Infrastructure.Repositories;

public sealed class RoleRepository(RelativaDbContext db) : IRoleRepository
{
    public async Task<Role?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await db.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsArchived, ct);
    }

    public async Task<List<Role>> GetByWorkspaceIdAsync(int workspaceId, CancellationToken ct = default)
    {
        return await db.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .Where(r => (r.WorkspaceId == null || r.WorkspaceId == workspaceId) && !r.IsArchived)
            .ToListAsync(ct);
    }

    public async Task<Role?> GetSystemRoleByNameAsync(string name, CancellationToken ct = default)
    {
        return await db.Roles
            .FirstOrDefaultAsync(r => r.Name == name && r.WorkspaceId == null && !r.IsArchived, ct);
    }

    public async Task AddAsync(Role role, CancellationToken ct = default)
    {
        db.Roles.Add(role);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Role role, CancellationToken ct = default)
    {
        db.Roles.Update(role);
        await db.SaveChangesAsync(ct);
    }
}
