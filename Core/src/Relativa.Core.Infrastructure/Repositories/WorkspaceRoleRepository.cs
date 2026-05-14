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
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Name == name && r.WorkspaceId == null && !r.IsArchived, ct);
    }

    public async Task<WorkspaceRole?> GetSystemRoleWithPermissionsSupersetAsync(
        IReadOnlyCollection<string> requiredPermissionNames,
        CancellationToken ct = default)
    {
        if (requiredPermissionNames.Count == 0)
            return null;

        var required = requiredPermissionNames.ToHashSet(StringComparer.Ordinal);
        var systemRoles = await db.WorkspaceRoles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .Where(r => r.WorkspaceId == null && !r.IsArchived)
            .OrderBy(r => r.Id)
            .ToListAsync(ct);

        return systemRoles.FirstOrDefault(role =>
        {
            var granted = role.RolePermissions
                .Select(rp => rp.Permission?.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .ToHashSet(StringComparer.Ordinal);
            return required.All(granted.Contains);
        });
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
