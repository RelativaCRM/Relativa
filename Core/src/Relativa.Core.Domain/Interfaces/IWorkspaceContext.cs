namespace Relativa.Core.Domain.Interfaces;

public interface IWorkspaceContext
{
    int? WorkspaceId { get; }
    int UserId { get; }
    string? RoleName { get; }
    IReadOnlyList<string> Permissions { get; }
    bool HasPermission(string permission);
}
