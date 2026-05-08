using Relativa.Persistence.Entities;

namespace Relativa.Core.Domain.Interfaces;

public interface IWorkspaceRepository
{
    Task<Workspace?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<Workspace>> GetByUserIdAsync(int userId, CancellationToken ct = default);
    Task<List<Workspace>> GetByUserIdAndOrganizationIdAsync(int userId, int organizationId, CancellationToken ct = default);
    /// <summary>All non-archived workspaces in an organization.</summary>
    Task<List<Workspace>> GetByOrganizationIdAsync(int organizationId, CancellationToken ct = default);
    Task AddAsync(Workspace workspace, CancellationToken ct = default);
    Task UpdateAsync(Workspace workspace, CancellationToken ct = default);
}
