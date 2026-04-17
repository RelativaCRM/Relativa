using Relativa.Core.Application.DTOs.Organization;

namespace Relativa.Core.Application.Interfaces;

public interface IOrganizationService
{
    Task<OrganizationDto> CreateAsync(int userId, CreateOrganizationRequest request, CancellationToken ct = default);
    Task<List<OrganizationDto>> GetByUserAsync(int userId, CancellationToken ct = default);
    Task<OrganizationDto> GetByIdAsync(int organizationId, int userId, CancellationToken ct = default);
    Task UpdateAsync(int organizationId, int userId, UpdateOrganizationRequest request, CancellationToken ct = default);
    Task<List<OrganizationSearchResultDto>> SearchAsync(string query, CancellationToken ct = default);
    Task<List<OrgMemberDto>> GetMembersAsync(int organizationId, int userId, CancellationToken ct = default);
    Task RemoveMemberAsync(int organizationId, int targetUserId, int callerUserId, CancellationToken ct = default);
    Task ChangeMemberRoleAsync(int organizationId, int targetUserId, int callerUserId, ChangeOrgMemberRoleRequest request, CancellationToken ct = default);
}
