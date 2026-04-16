namespace Relativa.Core.Application.DTOs.JoinRequest;

public sealed record JoinRequestDto(int Id, int UserId, string UserName, string UserEmail, string? Message, string Status, DateTime CreatedAt, string? ReviewedByName, DateTime? ReviewedAt);
