using Microsoft.EntityFrameworkCore;
using Relativa.Core.Domain.Interfaces;
using Relativa.Core.Infrastructure.Data;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Infrastructure.Repositories;

public sealed class WorkspaceRoleRepository(RelativaDbContext db) : IWorkspaceRoleRepository
{
    public async Task<WorkspaceRole?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await db.WorkspaceRoles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsArchived, ct);
    }

    public async Task<List<WorkspaceRole>> GetByWorkspaceIdAsync(int workspaceId, CancellationToken ct = default)
    {
        return await db.WorkspaceRoles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .Where(r => (r.WorkspaceId == null || r.WorkspaceId == workspaceId) && !r.IsArchived)
            .ToListAsync(ct);
    }

    public async Task<WorkspaceRole?> GetSystemRoleByNameAsync(string name, CancellationToken ct = default)
    {
        return await db.WorkspaceRoles
            .FirstOrDefaultAsync(r => r.Name == name && r.WorkspaceId == null && !r.IsArchived, ct);
    }

    public async Task AddAsync(WorkspaceRole role, CancellationToken ct = default)
    {
        db.WorkspaceRoles.Add(role);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(WorkspaceRole role, CancellationToken ct = default)
    {
        db.WorkspaceRoles.Update(role);
        await db.SaveChangesAsync(ct);
    }
}
