namespace Relativa.Authentication.Application.DTOs;

public sealed record LoginResponseDto(string AccessToken, DateTime ExpiresAt);
