using Microsoft.EntityFrameworkCore;
using Relativa.Core.Domain.Interfaces;
using Relativa.Core.Infrastructure.Data;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Infrastructure.Repositories;

public sealed class UserRoleWorkspaceRepository(RelativaDbContext db) : IUserRoleWorkspaceRepository
{
    public async Task<UserRoleWorkspace?> GetAsync(int userId, int workspaceId, CancellationToken ct = default)
    {
        return await db.UserRoleWorkspaces
            .Include(urw => urw.User)
            .Include(urw => urw.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(urw => urw.UserId == userId && urw.WorkspaceId == workspaceId && !urw.IsArchived, ct);
    }

    public async Task<Dictionary<int, int>> GetRolePrioritiesByUserIdsAsync(
        int workspaceId,
        IReadOnlyCollection<int> userIds,
        CancellationToken ct = default)
    {
        if (userIds.Count == 0)
            return new Dictionary<int, int>();

        return await db.UserRoleWorkspaces
            .AsNoTracking()
            .Where(urw => urw.WorkspaceId == workspaceId && !urw.IsArchived && userIds.Contains(urw.UserId))
            .Select(urw => new { urw.UserId, urw.Role.Priority })
            .ToDictionaryAsync(x => x.UserId, x => x.Priority, ct);
    }

    public async Task<List<UserRoleWorkspace>> GetByWorkspaceIdAsync(int workspaceId, CancellationToken ct = default)
    {
        return await db.UserRoleWorkspaces
            .Include(urw => urw.User)
            .Include(urw => urw.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .Where(urw => urw.WorkspaceId == workspaceId && !urw.IsArchived)
            .ToListAsync(ct);
    }

    public async Task AddAsync(UserRoleWorkspace member, CancellationToken ct = default)
    {
        db.UserRoleWorkspaces.Add(member);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(UserRoleWorkspace member, CancellationToken ct = default)
    {
        db.UserRoleWorkspaces.Update(member);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(UserRoleWorkspace member, CancellationToken ct = default)
    {
        db.UserRoleWorkspaces.Remove(member);
        await db.SaveChangesAsync(ct);
    }
}
