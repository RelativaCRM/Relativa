namespace Relativa.Core.Application.DTOs.Role;

public sealed record RoleDto(
    int Id,
    string Name,
    bool IsSystem,
    List<PermissionDto> Permissions);
