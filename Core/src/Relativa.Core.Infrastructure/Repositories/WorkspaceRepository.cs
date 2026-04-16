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
        return await db.UserRoleWorkspaces
            .Where(urw => urw.UserId == userId && !urw.IsArchived)
            .Select(urw => urw.Workspace)
            .Where(w => !w.IsArchived)
            .ToListAsync(ct);
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
