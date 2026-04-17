using Relativa.Persistence.Entities;

namespace Relativa.Core.Domain.Interfaces;

public interface IWorkspaceRoleRepository
{
    Task<WorkspaceRole?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<WorkspaceRole>> GetByWorkspaceIdAsync(int workspaceId, CancellationToken ct = default);
    Task<WorkspaceRole?> GetSystemRoleByNameAsync(string name, CancellationToken ct = default);
    Task AddAsync(WorkspaceRole role, CancellationToken ct = default);
    Task UpdateAsync(WorkspaceRole role, CancellationToken ct = default);
}
