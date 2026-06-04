using Relativa.Authentication.Application.DTOs;
using Relativa.Persistence.Entities;

namespace Relativa.Authentication.Application.Interfaces;

public interface IUserProvisioningService
{
    /// <param name="auditActorUserId">When null, the new user's id is used as actor (self-registration).</param>
    Task<RegisterResponseDto> CreateUserAsync(RegisterRequestDto request, int? auditActorUserId, CancellationToken ct);

    Task<User> CreateExternalUserAsync(ExternalIdentity identity, CancellationToken ct);

    Task<UserProfileDto> UpdateUserProfileAsync(int targetUserId, string firstName, string lastName, int auditActorUserId, CancellationToken ct);

    Task ArchiveUserAsync(int targetUserId, int auditActorUserId, CancellationToken ct);
}
