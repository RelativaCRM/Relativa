using Microsoft.EntityFrameworkCore;
using Relativa.Core.Domain.Interfaces;
using Relativa.Core.Infrastructure.Data;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Infrastructure.Repositories;

public sealed class WorkspaceInvitationRepository(RelativaDbContext db) : IWorkspaceInvitationRepository
{
    public async Task<WorkspaceInvitation?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await db.WorkspaceInvitations
            .Include(i => i.Role)
            .Include(i => i.Workspace)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<WorkspaceInvitation?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        return await db.WorkspaceInvitations
            .Include(i => i.Role)
            .Include(i => i.Workspace)
            .FirstOrDefaultAsync(i => i.Token == token, ct);
    }

    public async Task<List<WorkspaceInvitation>> GetByWorkspaceIdAsync(int workspaceId, CancellationToken ct = default)
    {
        return await db.WorkspaceInvitations
            .Include(i => i.Role)
            .Include(i => i.Workspace)
            .Where(i => i.WorkspaceId == workspaceId)
            .ToListAsync(ct);
    }

    public async Task<List<WorkspaceInvitation>> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return await db.WorkspaceInvitations
            .Include(i => i.Role)
            .Include(i => i.Workspace)
            .Where(i => i.Email == normalized && i.Status == "Pending")
            .ToListAsync(ct);
    }

    public async Task AddAsync(WorkspaceInvitation invitation, CancellationToken ct = default)
    {
        db.WorkspaceInvitations.Add(invitation);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(WorkspaceInvitation invitation, CancellationToken ct = default)
    {
        db.WorkspaceInvitations.Update(invitation);
        await db.SaveChangesAsync(ct);
    }
}
