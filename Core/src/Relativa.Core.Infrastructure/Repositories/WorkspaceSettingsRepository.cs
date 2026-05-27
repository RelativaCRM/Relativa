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
}
