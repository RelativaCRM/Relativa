namespace Relativa.Core.Application.DTOs.OrgRole;

public sealed record CreateOrgRoleRequest(string Name, List<int> PermissionIds, int Priority);
