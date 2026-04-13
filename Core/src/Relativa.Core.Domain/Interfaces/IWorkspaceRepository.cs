using Relativa.Persistence.Entities;

namespace Relativa.Core.Domain.Interfaces;

public interface IWorkspaceRepository
{
    Task<Workspace?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<Workspace>> GetByUserIdAsync(int userId, CancellationToken ct = default);
    Task AddAsync(Workspace workspace, CancellationToken ct = default);
    Task UpdateAsync(Workspace workspace, CancellationToken ct = default);
}
