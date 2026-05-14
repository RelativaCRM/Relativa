using Relativa.Core.Application.DTOs.OrgRole;
using Relativa.Core.Application.DTOs.Role;

namespace Relativa.Core.Application.Interfaces;

public interface IOrgRoleService
{
    Task<List<OrgRoleDto>> GetByOrganizationAsync(int organizationId, int userId, CancellationToken ct = default);
    Task<OrgRoleDto> CreateAsync(int organizationId, int userId, CreateOrgRoleRequest request, CancellationToken ct = default);
    Task UpdateAsync(int organizationId, int roleId, int userId, UpdateOrgRoleRequest request, CancellationToken ct = default);
    Task ArchiveAsync(int organizationId, int roleId, int userId, CancellationToken ct = default);
}
