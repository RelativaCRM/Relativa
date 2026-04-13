using FluentValidation;
using Relativa.Authentication.Application.DTOs;
using Relativa.Authentication.Application.Interfaces;
using Relativa.Persistence.Entities;
using Relativa.Authentication.Domain.Interfaces;

namespace Relativa.Authentication.Application.Services;

public sealed class AuthService(
    IUserRepository userRepository,
    ITokenService tokenService,
    IPasswordHasher passwordHasher,
    IValidator<LoginRequestDto> loginValidator,
    IValidator<RegisterRequestDto> registerValidator) : IAuthService
{
    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken ct = default)
    {
        await loginValidator.ValidateAndThrowAsync(request, ct);

        var user = await userRepository.GetByEmailAsync(request.Email, ct)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (!passwordHasher.Verify(request.Password, user.Password))
            throw new UnauthorizedAccessException("Invalid email or password.");

        var (token, expiresAt) = tokenService.GenerateAccessToken(user);

        return new LoginResponseDto(token, expiresAt);
    }

    public async Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken ct = default)
    {
        await registerValidator.ValidateAndThrowAsync(request, ct);

        if (await userRepository.ExistsAsync(request.Email, ct))
            throw new InvalidOperationException("A user with this email already exists.");

        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Password = passwordHasher.Hash(request.Password),
            RoleId = null,
            CreatedAt = DateTime.UtcNow,
            IsArchived = false
        };

        await userRepository.AddAsync(user, ct);

        return new RegisterResponseDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName);
    }
}
