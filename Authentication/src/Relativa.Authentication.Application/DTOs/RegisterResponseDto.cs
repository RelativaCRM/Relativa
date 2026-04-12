namespace Relativa.Authentication.Application.DTOs;

public sealed record RegisterResponseDto(
    int Id,
    string Email,
    string FirstName,
    string LastName,
    string RoleName);
