using Relativa.Core.Application.DTOs.Role;

namespace Relativa.Core.Application.DTOs.OrgRole;

public sealed record OrgRoleDto(int Id, string Name, bool IsSystem, int Priority, List<PermissionDto> Permissions);
