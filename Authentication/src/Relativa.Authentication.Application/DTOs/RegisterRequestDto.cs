namespace Relativa.Authentication.Application.DTOs;

public sealed record RegisterRequestDto(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    int? RoleId = null);
