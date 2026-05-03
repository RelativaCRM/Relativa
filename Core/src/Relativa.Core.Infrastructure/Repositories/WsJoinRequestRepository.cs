using Microsoft.EntityFrameworkCore;
using Relativa.Core.Domain.Interfaces;
using Relativa.Core.Infrastructure.Data;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Infrastructure.Repositories;

public sealed class WsJoinRequestRepository(RelativaDbContext db) : IWsJoinRequestRepository
{
    public async Task<WorkspaceJoinRequest?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await db.WorkspaceJoinRequests
            .Include(r => r.User)
            .Include(r => r.Workspace)
            .Include(r => r.ReviewedBy)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<List<WorkspaceJoinRequest>> GetByWorkspaceIdAsync(int workspaceId, CancellationToken ct = default)
    {
        return await db.WorkspaceJoinRequests
            .Include(r => r.User)
            .Include(r => r.Workspace)
            .Include(r => r.ReviewedBy)
            .Where(r => r.WorkspaceId == workspaceId && r.Status == "Pending")
            .ToListAsync(ct);
    }

    public async Task<List<WorkspaceJoinRequest>> GetByUserIdAsync(int userId, CancellationToken ct = default)
    {
        return await db.WorkspaceJoinRequests
            .Include(r => r.User)
            .Include(r => r.Workspace)
            .Include(r => r.ReviewedBy)
            .Where(r => r.UserId == userId)
            .ToListAsync(ct);
    }

    public async Task<WorkspaceJoinRequest?> GetPendingAsync(int userId, int workspaceId, CancellationToken ct = default)
    {
        return await db.WorkspaceJoinRequests
            .FirstOrDefaultAsync(
                r => r.UserId == userId && r.WorkspaceId == workspaceId && r.Status == "Pending",
                ct);
    }

    public async Task AddAsync(WorkspaceJoinRequest request, CancellationToken ct = default)
    {
        db.WorkspaceJoinRequests.Add(request);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(WorkspaceJoinRequest request, CancellationToken ct = default)
    {
        db.WorkspaceJoinRequests.Update(request);
        await db.SaveChangesAsync(ct);
    }
}
