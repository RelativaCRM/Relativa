using Relativa.Persistence.Entities;

namespace Relativa.Core.Domain.Interfaces;

public interface IWorkspaceMemberRepository
{
    Task<WorkspaceMember?> GetAsync(int userId, int workspaceId, CancellationToken ct = default);
    Task<List<WorkspaceMember>> GetByWorkspaceIdAsync(int workspaceId, CancellationToken ct = default);
    Task AddAsync(WorkspaceMember member, CancellationToken ct = default);
    Task UpdateAsync(WorkspaceMember member, CancellationToken ct = default);
    Task RemoveAsync(WorkspaceMember member, CancellationToken ct = default);
}
