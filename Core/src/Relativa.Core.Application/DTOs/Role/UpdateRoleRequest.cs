namespace Relativa.Core.Application.DTOs.Role;

public sealed record UpdateRoleRequest(string? Name, List<int>? PermissionIds);
