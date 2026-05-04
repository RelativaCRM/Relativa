using Relativa.Authentication.Application.DTOs;
using Relativa.Core.Application.DTOs.Organization;

namespace Relativa.Core.Application.Interfaces;

public interface IOrganizationUserAdminService
{
    Task<RegisterResponseDto> CreateOrgUserAsync(int organizationId, int callerUserId, CreateOrgUserRequest request, CancellationToken ct = default);

    Task<UserProfileDto> UpdateOtherUserProfileAsync(int organizationId, int targetUserId, int callerUserId, UpdateOrgUserProfileRequest request, CancellationToken ct = default);

    Task DeleteOrgUserAsync(int organizationId, int targetUserId, int callerUserId, CancellationToken ct = default);
}
