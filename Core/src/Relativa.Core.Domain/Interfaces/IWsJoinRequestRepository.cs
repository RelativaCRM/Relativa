using Relativa.Persistence.Entities;

namespace Relativa.Core.Domain.Interfaces;

public interface IWsJoinRequestRepository
{
    Task<WorkspaceJoinRequest?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<WorkspaceJoinRequest>> GetByWorkspaceIdAsync(int workspaceId, CancellationToken ct = default);
    Task<List<WorkspaceJoinRequest>> GetByUserIdAsync(int userId, CancellationToken ct = default);
    Task<WorkspaceJoinRequest?> GetPendingAsync(int userId, int workspaceId, CancellationToken ct = default);
    Task AddAsync(WorkspaceJoinRequest request, CancellationToken ct = default);
    Task UpdateAsync(WorkspaceJoinRequest request, CancellationToken ct = default);
}
