using Relativa.Persistence.Entities;

namespace Relativa.Core.Domain.Interfaces;

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<Role>> GetByWorkspaceIdAsync(int workspaceId, CancellationToken ct = default);
    Task<Role?> GetSystemRoleByNameAsync(string name, CancellationToken ct = default);
    Task AddAsync(Role role, CancellationToken ct = default);
    Task UpdateAsync(Role role, CancellationToken ct = default);
}
