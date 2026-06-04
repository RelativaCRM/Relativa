using FluentValidation;
using Relativa.Authentication.Application.DTOs;
using Relativa.Authentication.Application.Exceptions;
using Relativa.Authentication.Application.Interfaces;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;

namespace Relativa.Authentication.Application.Services;

public sealed class UserProvisioningService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IValidator<RegisterRequestDto> registerValidator,
    IOutboxWriter? auditOutboxWriter = null) : IUserProvisioningService
{
    public async Task<RegisterResponseDto> CreateUserAsync(RegisterRequestDto request, int? auditActorUserId, CancellationToken ct)
    {
        await registerValidator.ValidateAndThrowAsync(request, ct);

        var email = EmailNormalizer.Normalize(request.Email);
        if (await userRepository.ExistsAsync(email, ct))
            throw new AuthException("email_already_exists", 409, "A user with this email already exists.");

        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = email,
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            DateOfBirth = request.DateOfBirth,
            Password = passwordHasher.Hash(request.Password),
            CreatedAt = DateTime.UtcNow,
            IsArchived = false,
            EmailVerified = auditActorUserId.HasValue,
            Settings = new UserSettings
            {
                Locale = string.IsNullOrWhiteSpace(request.Locale) ? "en" : request.Locale
            }
        };

        await userRepository.AddAsync(user, ct);

        var actorId = auditActorUserId ?? user.Id;
        var action = auditActorUserId.HasValue ? "user_provisioned" : "user_registered";

        if (auditOutboxWriter is not null)
        {
            await auditOutboxWriter.EnqueueAuditAsync(
                new AuditEventContract(
                    EventId: Guid.NewGuid(),
                    SchemaVersion: 1,
                    OccurredAtUtc: DateTimeOffset.UtcNow,
                    SourceService: "authentication",
                    ActorUserId: actorId,
                    AuditScope: AuditRouting.ScopeUser,
                    TargetId: user.Id,
                    Action: action,
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

    public async Task<User> CreateExternalUserAsync(ExternalIdentity identity, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var email = EmailNormalizer.Normalize(identity.Email);

        var localPart = email.Split('@')[0];
        var firstName = string.IsNullOrWhiteSpace(identity.FirstName)
            ? (string.IsNullOrEmpty(localPart) ? "User" : localPart)
            : identity.FirstName.Trim();
        var lastName = string.IsNullOrWhiteSpace(identity.LastName)
            ? string.Empty
            : identity.LastName.Trim();

        var user = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Password = null,
            CreatedAt = now,
            IsArchived = false,
            EmailVerified = true,
            Settings = new UserSettings(),
            ExternalLogins =
            {
                new UserExternalLogin
                {
                    Provider = identity.Provider,
                    Subject = identity.Subject,
                    CreatedAt = now
                }
            }
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
                        user.LastName,
                        identity.Provider
                    })),
                ct);
        }

        return user;
    }

    public async Task<UserProfileDto> UpdateUserProfileAsync(int targetUserId, string firstName, string lastName, int auditActorUserId, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(targetUserId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        var oldSnapshot = new { user.FirstName, user.LastName };
        user.FirstName = firstName;
        user.LastName = lastName;
        await userRepository.UpdateAsync(user, ct);

        if (auditOutboxWriter is not null)
        {
            await auditOutboxWriter.EnqueueAuditAsync(
                new AuditEventContract(
                    EventId: Guid.NewGuid(),
                    SchemaVersion: 1,
                    OccurredAtUtc: DateTimeOffset.UtcNow,
                    SourceService: "authentication",
                    ActorUserId: auditActorUserId,
                    AuditScope: AuditRouting.ScopeUser,
                    TargetId: targetUserId,
                    Action: "user_profile_updated",
                    FieldName: null,
                    EntityType: null,
                    OldValueJson: System.Text.Json.JsonSerializer.Serialize(oldSnapshot),
                    NewValueJson: System.Text.Json.JsonSerializer.Serialize(new { firstName, lastName })),
                ct);
        }

        return new UserProfileDto(
            user.Id, user.Email, user.FirstName, user.LastName, user.TwoFactorEnabled,
            user.Phone, user.DateOfBirth,
            user.ExternalLogins.Select(l => l.Provider).Distinct().ToList(),
            !string.IsNullOrEmpty(user.Password));
    }

    public async Task ArchiveUserAsync(int targetUserId, int auditActorUserId, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(targetUserId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        user.IsArchived = true;
        await userRepository.UpdateAsync(user, ct);
        await userRepository.ReleaseExternalIdentifiersAsync(targetUserId, ct);

        if (auditOutboxWriter is not null)
        {
            await auditOutboxWriter.EnqueueAuditAsync(
                new AuditEventContract(
                    EventId: Guid.NewGuid(),
                    SchemaVersion: 1,
                    OccurredAtUtc: DateTimeOffset.UtcNow,
                    SourceService: "authentication",
                    ActorUserId: auditActorUserId,
                    AuditScope: AuditRouting.ScopeUser,
                    TargetId: targetUserId,
                    Action: "user_archived",
                    FieldName: null,
                    EntityType: null,
                    OldValueJson: null,
                    NewValueJson: System.Text.Json.JsonSerializer.Serialize(new { user.Id, user.Email })),
                ct);
        }
    }
}
