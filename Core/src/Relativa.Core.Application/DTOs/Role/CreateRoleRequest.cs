namespace Relativa.Core.Application.DTOs.Role;

public sealed record CreateRoleRequest(string Name, List<int> PermissionIds);
