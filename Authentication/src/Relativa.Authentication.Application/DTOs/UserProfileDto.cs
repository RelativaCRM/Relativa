namespace Relativa.Authentication.Application.DTOs;

public sealed record UserProfileDto(
    int Id,
    string Email,
    string FirstName,
    string LastName);
