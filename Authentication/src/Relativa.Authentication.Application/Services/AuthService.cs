using FluentValidation;
using Relativa.Authentication.Application.DTOs;
using Relativa.Authentication.Application.Interfaces;
using Relativa.Persistence.Entities;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Persistence.Contracts;

namespace Relativa.Authentication.Application.Services;

public sealed class AuthService(
    IUserRepository userRepository,
    ITokenService tokenService,
    IPasswordHasher passwordHasher,
    IValidator<LoginRequestDto> loginValidator,
    IValidator<RegisterRequestDto> registerValidator,
    IOutboxWriter? auditOutboxWriter = null) : IAuthService
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
            CreatedAt = DateTime.UtcNow,
            IsArchived = false
        };

        await userRepository.AddAsync(user, ct);

        if (auditOutboxWriter is not null)
        {
            await auditOutboxWriter.EnqueueAuditAsync(
                new AuditEventContract(
                    EventId: Guid.NewGuid(),
                    SchemaVersion: 1,
                    OccurredAtUtc: DateTimeOffset.UtcNow,
                    SourceService: "authentication",
                    ActorUserId: user.Id,
                    AuditScope: AuditRouting.ScopeUser,
                    TargetId: user.Id,
                    Action: "user_registered",
                    FieldName: null,
                    EntityType: null,
                    OldValueJson: null,
                    NewValueJson: System.Text.Json.JsonSerializer.Serialize(new
                    {
                        user.Id,
                        user.Email,
                        user.FirstName,
                        user.LastName
                    })),
                ct);
        }

        return new RegisterResponseDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName);
    }

    public async Task<UserProfileDto> GetProfileAsync(int userId, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        return new UserProfileDto(user.Id, user.Email, user.FirstName, user.LastName);
    }
}
