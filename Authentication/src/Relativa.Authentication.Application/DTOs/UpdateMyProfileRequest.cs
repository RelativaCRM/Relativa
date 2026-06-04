namespace Relativa.Authentication.Application.DTOs;

public sealed record UpdateMyProfileRequest(
    string FirstName,
    string LastName,
    string? Phone = null,
    DateOnly? DateOfBirth = null);
