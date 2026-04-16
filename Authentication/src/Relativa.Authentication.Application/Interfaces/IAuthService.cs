using Relativa.Authentication.Application.DTOs;

namespace Relativa.Authentication.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken ct = default);
    Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken ct = default);
    Task<UserProfileDto> GetProfileAsync(int userId, CancellationToken ct = default);
}
