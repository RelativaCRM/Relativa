using FluentAssertions;
using Moq;
using Relativa.Core.Application.Authorization;
using Relativa.Core.Application.Exceptions;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class WorkspaceAccessEvaluatorTests
{
    private readonly Mock<IUserRoleWorkspaceRepository> _members = new();
    private readonly Mock<IUserRoleOrganizationRepository> _orgMembers = new();
    private readonly Mock<IWorkspaceRepository> _workspaces = new();
    private readonly Mock<IPermissionRepository> _permissions = new();

    private const int UserId = 7;
    private const int WorkspaceId = 11;
    private const int OrgId = 3;

    private WorkspaceAccessEvaluator CreateSut(bool withPermissionRepository = true) =>
        new(_members.Object, _orgMembers.Object, _workspaces.Object, null,
            withPermissionRepository ? _permissions.Object : null);

    private void WorkspaceExists() =>
        _workspaces.Setup(r => r.GetByIdAsync(WorkspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Workspace { Id = WorkspaceId, OrganizationId = OrgId });

    private void SetWorkspaceMember(UserRoleWorkspace? member) =>
        _members.Setup(r => r.GetAsync(UserId, WorkspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

    private void SetOrgMember(UserRoleOrganization? member) =>
        _orgMembers.Setup(r => r.GetAsync(UserId, OrgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

    private static UserRoleOrganization OrgMember(string roleName, params string[] permissionNames) =>
        new()
        {
            UserId = UserId,
            OrganizationId = OrgId,
            Role = new OrganizationRole
            {
                Name = roleName,
                RolePermissions = permissionNames
                    .Select(n => new OrganizationRolePermission { Permission = new Permission { Name = n } })
                    .ToList()
            }
        };

    private static UserRoleWorkspace WorkspaceMember(WorkspaceRole? role) =>
        new() { UserId = UserId, WorkspaceId = WorkspaceId, Role = role! };

    private static WorkspaceRole RoleWithPermissions(params string?[] permissionNames) =>
        new()
        {
            Name = "member",
            RolePermissions = permissionNames
                .Select(n => new WorkspaceRolePermission { Permission = new Permission { Name = n! } })
                .ToList()
        };

    [Fact]
    public async Task IsOrgOwnerOfWorkspaceAsync_WorkspaceNotFound_ReturnsFalse()
    {
        _workspaces.Setup(r => r.GetByIdAsync(WorkspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Workspace?)null);

        var result = await CreateSut().IsOrgOwnerOfWorkspaceAsync(UserId, WorkspaceId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsOrgOwnerOfWorkspaceAsync_NoOrgMembership_ReturnsFalse()
    {
        WorkspaceExists();
        SetOrgMember(null);

        var result = await CreateSut().IsOrgOwnerOfWorkspaceAsync(UserId, WorkspaceId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsOrgOwnerOfWorkspaceAsync_OrgOwnerRole_ReturnsTrue()
    {
        WorkspaceExists();
        SetOrgMember(OrgMember(WorkspaceAccessEvaluator.OrgOwnerRoleName));

        var result = await CreateSut().IsOrgOwnerOfWorkspaceAsync(UserId, WorkspaceId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsOrgOwnerOfWorkspaceAsync_NonOwnerRoleWithAllActivePermissions_ReturnsTrue()
    {
        WorkspaceExists();
        SetOrgMember(OrgMember("custom_admin", OrganizationPermissions.ManageOrgSettings, OrganizationPermissions.InviteToOrg));
        _permissions.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new Permission { Name = OrganizationPermissions.ManageOrgSettings },
                new Permission { Name = OrganizationPermissions.InviteToOrg },
                new Permission { Name = "archived_permission", IsArchived = true },
                new Permission { Name = "   " }
            ]);

        var result = await CreateSut().IsOrgOwnerOfWorkspaceAsync(UserId, WorkspaceId);

        result.Should().BeTrue("archived and blank permissions are excluded, so the role holds every active permission");
    }

    [Fact]
    public async Task IsOrgOwnerOfWorkspaceAsync_NonOwnerRoleMissingPermission_ReturnsFalse()
    {
        WorkspaceExists();
        SetOrgMember(OrgMember("custom_admin", OrganizationPermissions.ManageOrgSettings));
        _permissions.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new Permission { Name = OrganizationPermissions.ManageOrgSettings },
                new Permission { Name = OrganizationPermissions.InviteToOrg }
            ]);

        var result = await CreateSut().IsOrgOwnerOfWorkspaceAsync(UserId, WorkspaceId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsOrgOwnerOfWorkspaceAsync_NoPermissionRepository_UsesFallbackPermissionSet()
    {
        WorkspaceExists();
        SetOrgMember(OrgMember("custom_admin", WorkspacePermissions.FullWorkspaceAuthority
            .Concat(new[]
            {
                OrganizationPermissions.ManageOrgSettings,
                OrganizationPermissions.InviteToOrg,
                OrganizationPermissions.ManageJoinRequests,
                OrganizationPermissions.RemoveOrgMembers,
                OrganizationPermissions.AssignOrgRoles,
                OrganizationPermissions.ManageOrgRoles,
                OrganizationPermissions.CreateWorkspaces,
                OrganizationPermissions.ManageOrgWorkspaceMembers,
                OrganizationPermissions.CreateOrgUsers,
                OrganizationPermissions.EditOtherOrgUsersProfile,
                OrganizationPermissions.DeleteOrgUsers
            }).ToArray()));

        var result = await CreateSut(withPermissionRepository: false).IsOrgOwnerOfWorkspaceAsync(UserId, WorkspaceId);

        result.Should().BeTrue("with no permission repository the evaluator falls back to the built-in full-authority set");
    }

    [Fact]
    public async Task EnsureCanAccessWorkspaceAsync_DirectMember_DoesNotThrow()
    {
        SetWorkspaceMember(WorkspaceMember(RoleWithPermissions()));

        var act = () => CreateSut().EnsureCanAccessWorkspaceAsync(UserId, WorkspaceId);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureCanAccessWorkspaceAsync_OrgOwnerButNotMember_DoesNotThrow()
    {
        SetWorkspaceMember(null);
        WorkspaceExists();
        SetOrgMember(OrgMember(WorkspaceAccessEvaluator.OrgOwnerRoleName));

        var act = () => CreateSut().EnsureCanAccessWorkspaceAsync(UserId, WorkspaceId);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureCanAccessWorkspaceAsync_NeitherMemberNorOrgOwner_ThrowsForbidden()
    {
        SetWorkspaceMember(null);
        WorkspaceExists();
        SetOrgMember(null);

        var act = () => CreateSut().EnsureCanAccessWorkspaceAsync(UserId, WorkspaceId);

        (await act.Should().ThrowAsync<AppException>()).Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task HasWorkspacePermissionAsync_OrgOwner_ReturnsTrueForAnyPermission()
    {
        SetWorkspaceMember(null);
        WorkspaceExists();
        SetOrgMember(OrgMember(WorkspaceAccessEvaluator.OrgOwnerRoleName));

        var result = await CreateSut().HasWorkspacePermissionAsync(UserId, WorkspaceId, "view_analytics");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasWorkspacePermissionAsync_MemberWithPermission_ReturnsTrue()
    {
        SetWorkspaceMember(WorkspaceMember(RoleWithPermissions("view_analytics")));
        WorkspaceExists();
        SetOrgMember(OrgMember("viewer"));
        _permissions.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await CreateSut().HasWorkspacePermissionAsync(UserId, WorkspaceId, "view_analytics");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasWorkspacePermissionAsync_MemberWithoutPermission_ReturnsFalse()
    {
        SetWorkspaceMember(WorkspaceMember(RoleWithPermissions("view_basic_stats")));
        WorkspaceExists();
        SetOrgMember(OrgMember("viewer"));
        _permissions.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await CreateSut().HasWorkspacePermissionAsync(UserId, WorkspaceId, "view_analytics");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetEffectiveWorkspacePermissionNamesAsync_OrgOwner_ReturnsFullAuthoritySorted()
    {
        SetWorkspaceMember(null);
        WorkspaceExists();
        SetOrgMember(OrgMember(WorkspaceAccessEvaluator.OrgOwnerRoleName));

        var result = await CreateSut().GetEffectiveWorkspacePermissionNamesAsync(UserId, WorkspaceId);

        result.Should().BeEquivalentTo(WorkspacePermissions.FullWorkspaceAuthority);
        result.Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    [Fact]
    public async Task GetEffectiveWorkspacePermissionNamesAsync_Member_ReturnsDistinctSortedNonEmptyNames()
    {
        SetWorkspaceMember(WorkspaceMember(RoleWithPermissions("view_analytics", "edit_entities", "view_analytics", "", null)));
        WorkspaceExists();
        SetOrgMember(OrgMember("viewer"));
        _permissions.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await CreateSut().GetEffectiveWorkspacePermissionNamesAsync(UserId, WorkspaceId);

        result.Should().Equal("edit_entities", "view_analytics");
    }

    [Fact]
    public async Task GetEffectiveWorkspacePermissionNamesAsync_MemberWithNullRole_ReturnsEmpty()
    {
        SetWorkspaceMember(WorkspaceMember(null));
        WorkspaceExists();
        SetOrgMember(OrgMember("viewer"));
        _permissions.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await CreateSut().GetEffectiveWorkspacePermissionNamesAsync(UserId, WorkspaceId);

        result.Should().BeEmpty();
    }
}
