namespace Relativa.Core.Application.DTOs.OrgRole;

public sealed record UpdateOrgRoleRequest(string? Name, List<int>? PermissionIds);
