using Relativa.Persistence.Entities;

namespace Relativa.Core.Domain.Interfaces;

public interface IWorkspaceSettingsRepository
{
    Task AddAsync(WorkspaceSettings settings, CancellationToken ct = default);
    Task<WorkspaceSettings?> GetByWorkspaceIdAsync(int workspaceId, CancellationToken ct = default);
    Task UpdateAsync(WorkspaceSettings settings, CancellationToken ct = default);
}
