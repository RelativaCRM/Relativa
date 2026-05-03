using FluentValidation;
using Relativa.Authentication.Application.DTOs;
using Relativa.Authentication.Application.Interfaces;
using Relativa.Authentication.Domain.Interfaces;

namespace Relativa.Authentication.Application.Services;

public sealed class AuthService(
    IUserRepository userRepository,
    IUserProvisioningService userProvisioning,
    ITokenService tokenService,
    IPasswordHasher passwordHasher,
    IValidator<LoginRequestDto> loginValidator,
    IValidator<UpdateMyProfileRequest> updateProfileValidator) : IAuthService
{
    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken ct = default)
    {
        await loginValidator.ValidateAndThrowAsync(request, ct);

        var email = EmailNormalizer.Normalize(request.Email);
        var user = await userRepository.GetByEmailAsync(email, ct)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (!passwordHasher.Verify(request.Password, user.Password))
            throw new UnauthorizedAccessException("Invalid email or password.");

        var (token, expiresAt) = tokenService.GenerateAccessToken(user);

        return new LoginResponseDto(token, expiresAt);
    }

    public Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken ct = default)
        => userProvisioning.CreateUserAsync(request, auditActorUserId: null, ct);

    public async Task<UserProfileDto> GetProfileAsync(int userId, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        return new UserProfileDto(user.Id, user.Email, user.FirstName, user.LastName);
    }

    public async Task<UserProfileDto> UpdateMyProfileAsync(int userId, UpdateMyProfileRequest request, CancellationToken ct = default)
    {
        await updateProfileValidator.ValidateAndThrowAsync(request, ct);
        return await userProvisioning.UpdateUserProfileAsync(userId, request.FirstName, request.LastName, userId, ct);
    }

    public Task DeleteMyAccountAsync(int userId, CancellationToken ct = default)
        => userProvisioning.ArchiveUserAsync(userId, userId, ct);
}
