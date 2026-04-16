using Relativa.Persistence.Entities;

namespace Relativa.Core.Domain.Interfaces;

public interface IUserRoleWorkspaceRepository
{
    Task<UserRoleWorkspace?> GetAsync(int userId, int workspaceId, CancellationToken ct = default);
    Task<List<UserRoleWorkspace>> GetByWorkspaceIdAsync(int workspaceId, CancellationToken ct = default);
    Task AddAsync(UserRoleWorkspace member, CancellationToken ct = default);
    Task UpdateAsync(UserRoleWorkspace member, CancellationToken ct = default);
    Task RemoveAsync(UserRoleWorkspace member, CancellationToken ct = default);
}
