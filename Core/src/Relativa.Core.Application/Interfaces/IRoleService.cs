using Relativa.Core.Application.DTOs.Role;

namespace Relativa.Core.Application.Interfaces;

public interface IRoleService
{
    Task<List<RoleDto>> GetByWorkspaceAsync(int workspaceId, int userId, CancellationToken ct = default);
    Task<RoleDto> CreateAsync(int workspaceId, int userId, CreateRoleRequest request, CancellationToken ct = default);
    Task UpdateAsync(int workspaceId, int roleId, int userId, UpdateRoleRequest request, CancellationToken ct = default);
    Task ArchiveAsync(int workspaceId, int roleId, int userId, CancellationToken ct = default);
    Task<List<PermissionDto>> GetAllPermissionsAsync(CancellationToken ct = default);
}
