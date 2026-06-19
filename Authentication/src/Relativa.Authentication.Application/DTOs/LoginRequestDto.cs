namespace Relativa.Authentication.Application.DTOs;

public sealed record LoginRequestDto(string Email, string Password, string? TwoFactorCode = null);
