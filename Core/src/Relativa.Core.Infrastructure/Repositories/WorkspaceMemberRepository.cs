using Microsoft.EntityFrameworkCore;
using Relativa.Core.Domain.Interfaces;
using Relativa.Core.Infrastructure.Data;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Infrastructure.Repositories;

public sealed class WorkspaceMemberRepository(RelativaDbContext db) : IWorkspaceMemberRepository
{
    public async Task<WorkspaceMember?> GetAsync(int userId, int workspaceId, CancellationToken ct = default)
    {
        return await db.WorkspaceMembers
            .Include(wm => wm.User)
            .Include(wm => wm.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(wm => wm.UserId == userId && wm.WorkspaceId == workspaceId && !wm.IsArchived, ct);
    }

    public async Task<List<WorkspaceMember>> GetByWorkspaceIdAsync(int workspaceId, CancellationToken ct = default)
    {
        return await db.WorkspaceMembers
            .Include(wm => wm.User)
            .Include(wm => wm.Role)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .Where(wm => wm.WorkspaceId == workspaceId && !wm.IsArchived)
            .ToListAsync(ct);
    }

    public async Task AddAsync(WorkspaceMember member, CancellationToken ct = default)
    {
        db.WorkspaceMembers.Add(member);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(WorkspaceMember member, CancellationToken ct = default)
    {
        db.WorkspaceMembers.Update(member);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(WorkspaceMember member, CancellationToken ct = default)
    {
        db.WorkspaceMembers.Remove(member);
        await db.SaveChangesAsync(ct);
    }
}
