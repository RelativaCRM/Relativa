using Relativa.Persistence.Entities;

namespace Relativa.Core.Domain.Interfaces;

public interface IWorkspaceSettingsRepository
{
    Task AddAsync(WorkspaceSettings settings, CancellationToken ct = default);
}
