using Microsoft.EntityFrameworkCore;
using Relativa.Core.Domain.Interfaces;
using Relativa.Core.Infrastructure.Data;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Infrastructure.Repositories;

public sealed class WorkspaceSettingsRepository(RelativaDbContext db) : IWorkspaceSettingsRepository
{
    public async Task AddAsync(WorkspaceSettings settings, CancellationToken ct = default)
    {
        db.WorkspaceSettings.Add(settings);
        await db.SaveChangesAsync(ct);
    }

    public async Task<WorkspaceSettings?> GetByWorkspaceIdAsync(int workspaceId, CancellationToken ct = default)
        => await db.WorkspaceSettings.FirstOrDefaultAsync(s => s.WorkspaceId == workspaceId, ct);

    public async Task UpdateAsync(WorkspaceSettings settings, CancellationToken ct = default)
    {
        db.WorkspaceSettings.Update(settings);
        await db.SaveChangesAsync(ct);
    }
}
