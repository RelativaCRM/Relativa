using Microsoft.EntityFrameworkCore;
using Relativa.Core.Domain.Interfaces;
using Relativa.Core.Infrastructure.Data;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Infrastructure.Repositories;

public sealed class WorkspaceRepository(RelativaDbContext db) : IWorkspaceRepository
{
    public async Task<Workspace?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await db.Workspaces
            .FirstOrDefaultAsync(w => w.Id == id && !w.IsArchived, ct);
    }

    public async Task<List<Workspace>> GetByUserIdAsync(int userId, CancellationToken ct = default)
    {
        var viaMembership = await db.UserRoleWorkspaces
            .Where(urw => urw.UserId == userId && !urw.IsArchived)
            .Select(urw => urw.Workspace)
            .Where(w => !w.IsArchived)
            .ToListAsync(ct);

        var totalPermissionCount = await db.Permissions
            .AsNoTracking()
            .CountAsync(p => !p.IsArchived, ct);

        var orgOwnerOrgIds = await db.UserRoleOrganizations
            .AsNoTracking()
            .Where(uro => uro.UserId == userId && !uro.IsArchived)
            .Where(uro => uro.Role != null &&
                          uro.Role.RolePermissions.Count(rp => !rp.Permission.IsArchived) >= totalPermissionCount)
            .Select(uro => uro.OrganizationId)
            .Distinct()
            .ToListAsync(ct);

        if (orgOwnerOrgIds.Count == 0)
            return viaMembership;

        var ownedOrgWorkspaces = await db.Workspaces
            .AsNoTracking()
            .Where(w => orgOwnerOrgIds.Contains(w.OrganizationId) && !w.IsArchived)
            .ToListAsync(ct);

        return viaMembership
            .Concat(ownedOrgWorkspaces)
            .GroupBy(w => w.Id)
            .Select(g => g.First())
            .ToList();
    }

    public async Task<List<Workspace>> GetByOrganizationIdAsync(int organizationId, CancellationToken ct = default)
    {
        return await db.Workspaces
            .Where(w => w.OrganizationId == organizationId && !w.IsArchived)
            .ToListAsync(ct);
    }

    public async Task<List<Workspace>> GetByUserIdAndOrganizationIdAsync(
        int userId,
        int organizationId,
        CancellationToken ct = default)
    {
        var viaMembership = await db.UserRoleWorkspaces
            .Where(urw => urw.UserId == userId && !urw.IsArchived)
            .Select(urw => urw.Workspace)
            .Where(w => !w.IsArchived && w.OrganizationId == organizationId)
            .ToListAsync(ct);

        var totalPermissionCount = await db.Permissions
            .AsNoTracking()
            .CountAsync(p => !p.IsArchived, ct);

        var isOrgOwner = await db.UserRoleOrganizations
            .AsNoTracking()
            .Where(uro => uro.UserId == userId && uro.OrganizationId == organizationId && !uro.IsArchived)
            .AnyAsync(uro => uro.Role != null &&
                             uro.Role.RolePermissions.Count(rp => !rp.Permission.IsArchived) >= totalPermissionCount,
                ct);

        if (!isOrgOwner)
            return viaMembership;

        var allOrg = await GetByOrganizationIdAsync(organizationId, ct);

        return viaMembership
            .Concat(allOrg)
            .GroupBy(w => w.Id)
            .Select(g => g.First())
            .ToList();
    }

    public async Task AddAsync(Workspace workspace, CancellationToken ct = default)
    {
        db.Workspaces.Add(workspace);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Workspace workspace, CancellationToken ct = default)
    {
        db.Workspaces.Update(workspace);
        await db.SaveChangesAsync(ct);
    }
}
