using Relativa.Core.Domain.Interfaces;

namespace Relativa.Core.Infrastructure.Services;

public sealed class WorkspaceContext : IWorkspaceContext
{
    public int? WorkspaceId { get; set; }
    public int UserId { get; set; }
    public string? RoleName { get; set; }
    public IReadOnlyList<string> Permissions { get; set; } = [];

    public bool HasPermission(string permission) =>
        Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
}
