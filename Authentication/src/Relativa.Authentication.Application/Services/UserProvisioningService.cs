using FluentValidation;
using Relativa.Authentication.Application.DTOs;
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
            throw new InvalidOperationException("A user with this email already exists.");

        var user = new User
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = email,
            Password = passwordHasher.Hash(request.Password),
            CreatedAt = DateTime.UtcNow,
            IsArchived = false
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

        return new UserProfileDto(user.Id, user.Email, user.FirstName, user.LastName);
    }

    public async Task ArchiveUserAsync(int targetUserId, int auditActorUserId, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(targetUserId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        user.IsArchived = true;
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
                    Action: "user_archived",
                    FieldName: null,
                    EntityType: null,
                    OldValueJson: null,
                    NewValueJson: System.Text.Json.JsonSerializer.Serialize(new { user.Id, user.Email })),
                ct);
        }
    }
}
