namespace Relativa.Core.Application.DTOs.WsJoinRequest;

public sealed record WsJoinRequestDto(
    int Id,
    int UserId,
    string UserName,
    string UserEmail,
    int WorkspaceId,
    string WorkspaceName,
    string? Message,
    string Status,
    DateTime CreatedAt,
    string? ReviewedByName,
    DateTime? ReviewedAt);
