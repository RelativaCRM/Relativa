using FluentValidation;
using Relativa.Core.Application.DTOs.Organization;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Entities;

namespace Relativa.Core.Application.Services;

public sealed class OrganizationService(
    IOrganizationRepository organizationRepository,
    IUserRoleOrganizationRepository orgMemberRepository,
    IOrganizationRoleRepository orgRoleRepository,
    IValidator<CreateOrganizationRequest> createValidator,
    IValidator<UpdateOrganizationRequest> updateValidator) : IOrganizationService
{
    public async Task<OrganizationDto> CreateAsync(int userId, CreateOrganizationRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);

        var ownerRole = await orgRoleRepository.GetSystemRoleByNameAsync("org_owner", ct)
            ?? throw new InvalidOperationException("System org_owner role not found.");

        var organization = new Organization
        {
            Name = request.Name,
            IsArchived = false
        };

        await organizationRepository.AddAsync(organization, ct);

        var membership = new UserRoleOrganization
        {
            UserId = userId,
            OrganizationId = organization.Id,
            OrgRoleId = ownerRole.Id,
            JoinedAt = DateTime.UtcNow,
            IsArchived = false
        };

        await orgMemberRepository.AddAsync(membership, ct);

        return new OrganizationDto(organization.Id, organization.Name, 1, ownerRole.Name);
    }

    public async Task<List<OrganizationDto>> GetByUserAsync(int userId, CancellationToken ct = default)
    {
        var organizations = await organizationRepository.GetByUserIdAsync(userId, ct);
        var result = new List<OrganizationDto>();

        foreach (var org in organizations)
        {
            var membership = await orgMemberRepository.GetAsync(userId, org.Id, ct);
            var memberCount = (await orgMemberRepository.GetByOrganizationIdAsync(org.Id, ct)).Count;
            result.Add(new OrganizationDto(org.Id, org.Name, memberCount, membership?.Role?.Name));
        }

        return result;
    }

    public async Task<OrganizationDto> GetByIdAsync(int organizationId, int userId, CancellationToken ct = default)
    {
        await RequireOrgMembership(userId, organizationId, ct);

        var organization = await organizationRepository.GetByIdAsync(organizationId, ct)
            ?? throw new KeyNotFoundException("Organization not found.");

        var membership = await orgMemberRepository.GetAsync(userId, organizationId, ct);
        var members = await orgMemberRepository.GetByOrganizationIdAsync(organizationId, ct);

        return new OrganizationDto(organization.Id, organization.Name, members.Count, membership?.Role?.Name);
    }

    public async Task UpdateAsync(int organizationId, int userId, UpdateOrganizationRequest request, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);
        await RequireOrgPermission(userId, organizationId, "manage_org_settings", ct);

        var organization = await organizationRepository.GetByIdAsync(organizationId, ct)
            ?? throw new KeyNotFoundException("Organization not found.");

        organization.Name = request.Name;
        await organizationRepository.UpdateAsync(organization, ct);
    }

    public async Task<List<OrganizationSearchResultDto>> SearchAsync(string query, CancellationToken ct = default)
    {
        var organizations = await organizationRepository.SearchAsync(query, ct);
        return organizations
            .Select(o => new OrganizationSearchResultDto(o.Id, o.Name))
            .ToList();
    }

    public async Task<List<OrgMemberDto>> GetMembersAsync(int organizationId, int userId, CancellationToken ct = default)
    {
        await RequireOrgMembership(userId, organizationId, ct);

        var members = await orgMemberRepository.GetByOrganizationIdAsync(organizationId, ct);
        return members
            .Where(m => !m.IsArchived)
            .Select(m => new OrgMemberDto(
                m.UserId,
                m.User.FirstName,
                m.User.LastName,
                m.User.Email,
                m.Role.Name,
                m.JoinedAt))
            .ToList();
    }

    public async Task RemoveMemberAsync(int organizationId, int targetUserId, int callerUserId, CancellationToken ct = default)
    {
        if (targetUserId != callerUserId)
            await RequireOrgPermission(callerUserId, organizationId, "remove_org_members", ct);

        var member = await orgMemberRepository.GetAsync(targetUserId, organizationId, ct)
            ?? throw new KeyNotFoundException("Target user is not a member of this organization.");

        await orgMemberRepository.RemoveAsync(member, ct);
    }

    public async Task ChangeMemberRoleAsync(int organizationId, int targetUserId, int callerUserId, ChangeOrgMemberRoleRequest request, CancellationToken ct = default)
    {
        await RequireOrgPermission(callerUserId, organizationId, "assign_org_roles", ct);

        var targetMember = await orgMemberRepository.GetAsync(targetUserId, organizationId, ct)
            ?? throw new KeyNotFoundException("Target user is not a member of this organization.");

        var role = await orgRoleRepository.GetByIdAsync(request.RoleId, ct)
            ?? throw new ArgumentException("The specified role does not exist.");

        if (role.OrganizationId.HasValue && role.OrganizationId.Value != organizationId)
            throw new ArgumentException("The specified role does not belong to this organization.");

        targetMember.OrgRoleId = role.Id;
        await orgMemberRepository.UpdateAsync(targetMember, ct);
    }

    private async Task<UserRoleOrganization> RequireOrgMembership(int userId, int orgId, CancellationToken ct)
    {
        return await orgMemberRepository.GetAsync(userId, orgId, ct)
            ?? throw new UnauthorizedAccessException("You are not a member of this organization.");
    }

    private async Task RequireOrgPermission(int userId, int orgId, string permission, CancellationToken ct)
    {
        var membership = await RequireOrgMembership(userId, orgId, ct);
        var hasPermission = membership.Role?.RolePermissions
            .Any(rp => rp.Permission?.Name == permission) ?? false;
        if (!hasPermission)
            throw new UnauthorizedAccessException($"You do not have the '{permission}' permission in this organization.");
    }
}
