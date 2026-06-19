namespace Relativa.Authentication.Application.DTOs;

public sealed record ResendVerificationRequest(string Email, string? Channel = null);
