using Relativa.Core.Application.DTOs.Role;

namespace Relativa.Core.Application.DTOs.OrgRole;

public sealed record OrgRoleDto(int Id, string Name, string DisplayName, bool IsSystem, int Priority, List<PermissionDto> Permissions);
