using Relativa.Persistence.Entities;

namespace Relativa.Core.Domain.Interfaces;

public interface IWorkspaceInvitationRepository
{
    Task<WorkspaceInvitation?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<WorkspaceInvitation?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task<List<WorkspaceInvitation>> GetByWorkspaceIdAsync(int workspaceId, CancellationToken ct = default);
    Task<List<WorkspaceInvitation>> GetByEmailAsync(string email, CancellationToken ct = default);
    Task AddAsync(WorkspaceInvitation invitation, CancellationToken ct = default);
    Task UpdateAsync(WorkspaceInvitation invitation, CancellationToken ct = default);
}
