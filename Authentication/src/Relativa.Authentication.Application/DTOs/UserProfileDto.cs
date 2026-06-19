namespace Relativa.Authentication.Application.DTOs;

public sealed record UserProfileDto(
    int Id,
    string Email,
    string FirstName,
    string LastName,
    bool TwoFactorEnabled = false,
    string? Phone = null,
    DateOnly? DateOfBirth = null,
    IReadOnlyList<string>? Providers = null,
    bool HasPassword = false);
