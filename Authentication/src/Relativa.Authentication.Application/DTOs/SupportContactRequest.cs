namespace Relativa.Authentication.Application.DTOs;

public sealed record SupportContactRequest(
    string Name,
    string Email,
    string Subject,
    string Message,
    string? Locale = null);
