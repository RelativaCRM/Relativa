using FluentAssertions;
using Moq;
using Relativa.Core.Application.DTOs.Member;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class WorkspaceMemberServiceTests
{
    private readonly Mock<IUserRoleWorkspaceRepository> _memberRepo = new();
    private readonly Mock<IWorkspaceRoleRepository> _roleRepo = new();
    private readonly Mock<IUserRoleOrganizationRepository> _orgMemberRepo = new();
    private readonly Mock<IWorkspaceRepository> _workspaceRepo = new();
    private readonly WorkspaceAccessEvaluator _workspaceAccessEvaluator;
    private readonly Mock<IOutboxWriter> _auditOutboxWriter = new();
    private readonly WorkspaceMemberService _sut;

    public WorkspaceMemberServiceTests()
    {
        _workspaceRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) =>
                new Workspace { Id = id, OrganizationId = 1, Name = "Test WS", IsArchived = false });
        _orgMemberRepo
            .Setup(r => r.GetAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleOrganization?)null);

        _workspaceAccessEvaluator = new WorkspaceAccessEvaluator(
            _memberRepo.Object,
            _orgMemberRepo.Object,
            _workspaceRepo.Object,
            _roleRepo.Object);

        _sut = new WorkspaceMemberService(
            _memberRepo.Object,
            _roleRepo.Object,
            _orgMemberRepo.Object,
            _workspaceRepo.Object,
            _workspaceAccessEvaluator,
            _auditOutboxWriter.Object);
    }

    private UserRoleWorkspace MemberWithPermission(int userId, int workspaceId, string permission) =>
        new()
        {
            UserId = userId,
            WorkspaceId = workspaceId,
            Role = new WorkspaceRole
            {
                Name = "sales_manager",
                RolePermissions =
                [
                    new WorkspaceRolePermission { Permission = new Permission { Name = permission } }
                ]
            }
        };

    private UserRoleOrganization OrgMember(int userId, int orgId) =>
        new() { UserId = userId, OrganizationId = orgId, User = new User { Id = userId, FirstName = "A", LastName = "B", Email = "a@b.io" } };

    [Fact]
    public async Task GetMembersAsync_UserNotMember_ThrowsUnauthorizedAccessException()
    {
        _memberRepo.Setup(r => r.GetAsync(10, 3, It.IsAny<CancellationToken>())).ReturnsAsync((UserRoleWorkspace?)null);

        var act = () => _sut.GetMembersAsync(3, 10);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You are not a member of this workspace.");
    }

    [Fact]
    public async Task GetMembersAsync_ValidMember_ReturnsOnlyActiveMembers()
    {
        var caller = MemberWithPermission(1, 2, "view_ws_members");
        var members = new List<UserRoleWorkspace>
        {
            new()
            {
                UserId = 1, WorkspaceId = 2, IsArchived = false, JoinedAt = DateTime.UtcNow,
                User = new User { FirstName = "Andriy", LastName = "S", Email = "andriy@r.io" },
                Role = new WorkspaceRole { Name = "ws_admin" }
            },
            new()
            {
                UserId = 2, WorkspaceId = 2, IsArchived = true, JoinedAt = DateTime.UtcNow,
                User = new User { FirstName = "Iryna", LastName = "T", Email = "iryna@r.io" },
                Role = new WorkspaceRole { Name = "analyst" }
            }
        };

        _memberRepo.Setup(r => r.GetAsync(1, 2, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _memberRepo.Setup(r => r.GetByWorkspaceIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(members);

        var result = await _sut.GetMembersAsync(2, 1);

        result.Should().HaveCount(1);
        result[0].Email.Should().Be("andriy@r.io");
    }

    [Fact]
    public async Task UpdateRoleAsync_UserLacksPermission_ThrowsUnauthorizedAccessException()
    {
        _memberRepo.Setup(r => r.GetAsync(5, 4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleWorkspace
            {
                UserId = 5, WorkspaceId = 4,
                Role = new WorkspaceRole { Name = "analyst", RolePermissions = [] }
            });

        var act = () => _sut.UpdateRoleAsync(4, 9, 5, new UpdateMemberRoleRequest(2));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _roleRepo.Verify(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRoleAsync_TargetNotMember_ThrowsKeyNotFoundException()
    {
        _memberRepo.Setup(r => r.GetAsync(1, 4, It.IsAny<CancellationToken>())).ReturnsAsync(MemberWithPermission(1, 4, "assign_ws_roles"));
        _memberRepo.Setup(r => r.GetAsync(99, 4, It.IsAny<CancellationToken>())).ReturnsAsync((UserRoleWorkspace?)null);

        var act = () => _sut.UpdateRoleAsync(4, 99, 1, new UpdateMemberRoleRequest(3));

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Target user is not a member of this workspace.");
    }

    [Fact]
    public async Task UpdateRoleAsync_RoleNotFound_ThrowsArgumentException()
    {
        var target = new UserRoleWorkspace { UserId = 2, WorkspaceId = 4, Role = new WorkspaceRole { Name = "analyst", RolePermissions = [] } };

        _memberRepo.Setup(r => r.GetAsync(1, 4, It.IsAny<CancellationToken>())).ReturnsAsync(MemberWithPermission(1, 4, "assign_ws_roles"));
        _memberRepo.Setup(r => r.GetAsync(2, 4, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        _roleRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((WorkspaceRole?)null);

        var act = () => _sut.UpdateRoleAsync(4, 2, 1, new UpdateMemberRoleRequest(99));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("The specified role does not exist.");
    }

    [Fact]
    public async Task UpdateRoleAsync_RoleFromAnotherWorkspace_ThrowsArgumentException()
    {
        var target = new UserRoleWorkspace { UserId = 2, WorkspaceId = 4, Role = new WorkspaceRole { Name = "analyst", RolePermissions = [] } };
        var foreignRole = new WorkspaceRole { Id = 7, Name = "custom", WorkspaceId = 99 };

        _memberRepo.Setup(r => r.GetAsync(1, 4, It.IsAny<CancellationToken>())).ReturnsAsync(MemberWithPermission(1, 4, "assign_ws_roles"));
        _memberRepo.Setup(r => r.GetAsync(2, 4, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        _roleRepo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(foreignRole);

        var act = () => _sut.UpdateRoleAsync(4, 2, 1, new UpdateMemberRoleRequest(7));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("The specified role does not belong to this workspace.");
    }

    [Fact]
    public async Task UpdateRoleAsync_ValidRequest_UpdatesRoleAndEnqueuesAuditEvent()
    {
        var target = new UserRoleWorkspace { UserId = 2, WorkspaceId = 4, WsRoleId = 1, Role = new WorkspaceRole { Name = "analyst", RolePermissions = [] } };
        var newRole = new WorkspaceRole { Id = 5, Name = "manager", WorkspaceId = 4 };

        _memberRepo.Setup(r => r.GetAsync(1, 4, It.IsAny<CancellationToken>())).ReturnsAsync(MemberWithPermission(1, 4, "assign_ws_roles"));
        _memberRepo.Setup(r => r.GetAsync(2, 4, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        _roleRepo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(newRole);

        await _sut.UpdateRoleAsync(4, 2, 1, new UpdateMemberRoleRequest(5));

        target.WsRoleId.Should().Be(5);
        _memberRepo.Verify(r => r.UpdateAsync(target, It.IsAny<CancellationToken>()), Times.Once);
        _auditOutboxWriter.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<AuditEventContract>(e =>
                    e.AuditScope == AuditRouting.ScopeWorkspace &&
                    e.Action == "workspace_member_role_changed" &&
                    e.TargetId == 4 &&
                    e.ActorUserId == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateRoleAsync_SoleWsAdmin_ThrowsInvalidOperationException()
    {
        var adminRole = new WorkspaceRole { Id = 1, Name = "ws_admin", WorkspaceId = 4 };
        var target = new UserRoleWorkspace { UserId = 2, WorkspaceId = 4, WsRoleId = 1, Role = adminRole };
        var analystRole = new WorkspaceRole { Id = 5, Name = "analyst", WorkspaceId = 4 };

        _memberRepo.Setup(r => r.GetAsync(1, 4, It.IsAny<CancellationToken>())).ReturnsAsync(MemberWithPermission(1, 4, "assign_ws_roles"));
        _memberRepo.Setup(r => r.GetAsync(2, 4, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        _memberRepo.Setup(r => r.GetByWorkspaceIdAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            MemberWithPermission(1, 4, "assign_ws_roles"),
            target
        ]);
        _roleRepo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(analystRole);

        var act = () => _sut.UpdateRoleAsync(4, 2, 1, new UpdateMemberRoleRequest(5));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot demote the last workspace admin.");
        _memberRepo.Verify(r => r.UpdateAsync(It.IsAny<UserRoleWorkspace>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateRoleAsync_SelfDemoteWsAdminWithOtherAdminPresent_Succeeds()
    {
        var adminRole = new WorkspaceRole
        {
            Id = 1, Name = "ws_admin", WorkspaceId = 4,
            RolePermissions = [new WorkspaceRolePermission { Permission = new Permission { Name = "assign_ws_roles" } }]
        };
        var target = new UserRoleWorkspace { UserId = 1, WorkspaceId = 4, WsRoleId = 1, Role = adminRole };
        var otherAdmin = new UserRoleWorkspace { UserId = 3, WorkspaceId = 4, Role = new WorkspaceRole { Name = "ws_admin" }, IsArchived = false };
        var analystRole = new WorkspaceRole { Id = 5, Name = "analyst", WorkspaceId = 4 };

        _memberRepo.Setup(r => r.GetAsync(1, 4, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        _memberRepo.Setup(r => r.GetByWorkspaceIdAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync([target, otherAdmin]);
        _roleRepo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(analystRole);

        await _sut.UpdateRoleAsync(4, 1, 1, new UpdateMemberRoleRequest(5));

        target.WsRoleId.Should().Be(5);
        _memberRepo.Verify(r => r.UpdateAsync(target, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_SelfRemove_SkipsPermissionCheck()
    {
        var member = new UserRoleWorkspace { UserId = 3, WorkspaceId = 6, Role = new WorkspaceRole { Name = "analyst", RolePermissions = [] } };
        _memberRepo.Setup(r => r.GetAsync(3, 6, It.IsAny<CancellationToken>())).ReturnsAsync(member);

        await _sut.RemoveAsync(6, 3, 3);

        _memberRepo.Verify(r => r.RemoveAsync(member, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_RemoveOtherWithoutPermission_ThrowsUnauthorizedAccessException()
    {
        _memberRepo.Setup(r => r.GetAsync(1, 6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleWorkspace
            {
                UserId = 1, WorkspaceId = 6,
                Role = new WorkspaceRole { Name = "analyst", RolePermissions = [] }
            });

        var act = () => _sut.RemoveAsync(6, 8, 1);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _memberRepo.Verify(r => r.RemoveAsync(It.IsAny<UserRoleWorkspace>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveAsync_TargetNotMember_ThrowsKeyNotFoundException()
    {
        _memberRepo.Setup(r => r.GetAsync(1, 6, It.IsAny<CancellationToken>())).ReturnsAsync(MemberWithPermission(1, 6, "remove_ws_members"));
        _memberRepo.Setup(r => r.GetAsync(77, 6, It.IsAny<CancellationToken>())).ReturnsAsync((UserRoleWorkspace?)null);

        var act = () => _sut.RemoveAsync(6, 77, 1);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Target user is not a member of this workspace.");
    }

    [Fact]
    public async Task RemoveAsync_ValidRequest_RemovesMemberAndEnqueuesAuditEvent()
    {
        var target = new UserRoleWorkspace { UserId = 8, WorkspaceId = 6, Role = new WorkspaceRole { Name = "analyst", RolePermissions = [] } };

        _memberRepo.Setup(r => r.GetAsync(1, 6, It.IsAny<CancellationToken>())).ReturnsAsync(MemberWithPermission(1, 6, "remove_ws_members"));
        _memberRepo.Setup(r => r.GetAsync(8, 6, It.IsAny<CancellationToken>())).ReturnsAsync(target);

        await _sut.RemoveAsync(6, 8, 1);

        _memberRepo.Verify(r => r.RemoveAsync(target, It.IsAny<CancellationToken>()), Times.Once);
        _auditOutboxWriter.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<AuditEventContract>(e =>
                    e.AuditScope == AuditRouting.ScopeWorkspace &&
                    e.Action == "workspace_member_removed" &&
                    e.TargetId == 6 &&
                    e.ActorUserId == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddMemberAsync_CallerLacksPermission_ThrowsUnauthorizedAccessException()
    {
        _memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleWorkspace
            {
                UserId = 1, WorkspaceId = 5,
                Role = new WorkspaceRole { Name = "analyst", RolePermissions = [] }
            });
        _orgMemberRepo
            .Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization
            {
                UserId = 1, OrganizationId = 1,
                Role = new OrganizationRole { Name = "org_viewer", RolePermissions = [] }
            });

        var act = () => _sut.AddMemberAsync(5, 1, new AddWorkspaceMemberRequest(10, 2));

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*add_ws_members*");
    }

    [Fact]
    public async Task AddMemberAsync_WorkspaceNotFound_ThrowsKeyNotFoundException()
    {
        _memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(MemberWithPermission(1, 5, "add_ws_members"));
        _workspaceRepo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync((Workspace?)null);

        var act = () => _sut.AddMemberAsync(5, 1, new AddWorkspaceMemberRequest(10, 2));

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Workspace not found.");
    }

    [Fact]
    public async Task AddMemberAsync_TargetUserNotInOrganization_ThrowsArgumentException()
    {
        var workspace = new Workspace { Id = 5, Name = "WS", OrganizationId = 3 };

        _memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(MemberWithPermission(1, 5, "add_ws_members"));
        _workspaceRepo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(workspace);
        _orgMemberRepo.Setup(r => r.GetAsync(10, 3, It.IsAny<CancellationToken>())).ReturnsAsync((UserRoleOrganization?)null);

        var act = () => _sut.AddMemberAsync(5, 1, new AddWorkspaceMemberRequest(10, 2));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*not a member of this organization*");
    }

    [Fact]
    public async Task AddMemberAsync_TargetAlreadyMember_ThrowsInvalidOperationException()
    {
        var workspace = new Workspace { Id = 5, Name = "WS", OrganizationId = 3 };
        var orgMembership = OrgMember(10, 3);

        _memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(MemberWithPermission(1, 5, "add_ws_members"));
        _workspaceRepo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(workspace);
        _orgMemberRepo.Setup(r => r.GetAsync(10, 3, It.IsAny<CancellationToken>())).ReturnsAsync(orgMembership);
        _memberRepo.Setup(r => r.GetAsync(10, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleWorkspace { UserId = 10, WorkspaceId = 5 });

        var act = () => _sut.AddMemberAsync(5, 1, new AddWorkspaceMemberRequest(10, 2));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already a member*");
    }

    [Fact]
    public async Task AddMemberAsync_RoleNotFound_ThrowsArgumentException()
    {
        var workspace = new Workspace { Id = 5, Name = "WS", OrganizationId = 3 };
        var orgMembership = OrgMember(10, 3);

        _memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(MemberWithPermission(1, 5, "add_ws_members"));
        _workspaceRepo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(workspace);
        _orgMemberRepo.Setup(r => r.GetAsync(10, 3, It.IsAny<CancellationToken>())).ReturnsAsync(orgMembership);
        _memberRepo.Setup(r => r.GetAsync(10, 5, It.IsAny<CancellationToken>())).ReturnsAsync((UserRoleWorkspace?)null);
        _roleRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((WorkspaceRole?)null);

        var act = () => _sut.AddMemberAsync(5, 1, new AddWorkspaceMemberRequest(10, 99));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("The specified role does not exist.");
    }

    [Fact]
    public async Task AddMemberAsync_RoleFromAnotherWorkspace_ThrowsArgumentException()
    {
        var workspace = new Workspace { Id = 5, Name = "WS", OrganizationId = 3 };
        var orgMembership = OrgMember(10, 3);
        var foreignRole = new WorkspaceRole { Id = 7, Name = "custom", WorkspaceId = 99 };

        _memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(MemberWithPermission(1, 5, "add_ws_members"));
        _workspaceRepo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(workspace);
        _orgMemberRepo.Setup(r => r.GetAsync(10, 3, It.IsAny<CancellationToken>())).ReturnsAsync(orgMembership);
        _memberRepo.Setup(r => r.GetAsync(10, 5, It.IsAny<CancellationToken>())).ReturnsAsync((UserRoleWorkspace?)null);
        _roleRepo.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(foreignRole);

        var act = () => _sut.AddMemberAsync(5, 1, new AddWorkspaceMemberRequest(10, 7));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("The specified role does not belong to this workspace.");
    }

    [Fact]
    public async Task AddMemberAsync_ValidRequest_AddsMemberAndEnqueuesAuditEvent()
    {
        var workspace = new Workspace { Id = 5, Name = "WS", OrganizationId = 3 };
        var orgMembership = OrgMember(10, 3);
        var role = new WorkspaceRole { Id = 2, Name = "analyst", WorkspaceId = 5 };

        _memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(MemberWithPermission(1, 5, "add_ws_members"));
        _workspaceRepo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(workspace);
        _orgMemberRepo.Setup(r => r.GetAsync(10, 3, It.IsAny<CancellationToken>())).ReturnsAsync(orgMembership);
        _memberRepo.Setup(r => r.GetAsync(10, 5, It.IsAny<CancellationToken>())).ReturnsAsync((UserRoleWorkspace?)null);
        _roleRepo.Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(role);

        var result = await _sut.AddMemberAsync(5, 1, new AddWorkspaceMemberRequest(10, 2));

        result.UserId.Should().Be(10);
        result.RoleName.Should().Be("analyst");
        _memberRepo.Verify(r => r.AddAsync(It.IsAny<UserRoleWorkspace>(), It.IsAny<CancellationToken>()), Times.Once);
        _auditOutboxWriter.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<AuditEventContract>(e =>
                    e.AuditScope == AuditRouting.ScopeWorkspace &&
                    e.Action == "workspace_member_added" &&
                    e.TargetId == 5 &&
                    e.ActorUserId == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddMemberAsync_CallerHasOrgLevelPermission_AddsMemberSuccessfully()
    {
        var callerWsMembership = new UserRoleWorkspace
        {
            UserId = 1, WorkspaceId = 5,
            Role = new WorkspaceRole { Name = "analyst", RolePermissions = [] }
        };
        var callerOrgMembership = new UserRoleOrganization
        {
            UserId = 1, OrganizationId = 1,
            Role = new OrganizationRole
            {
                Name = "org_admin",
                RolePermissions = [new OrganizationRolePermission { Permission = new Permission { Name = "manage_org_workspace_members" } }]
            }
        };
        var targetOrgMembership = OrgMember(10, 1);
        var role = new WorkspaceRole { Id = 2, Name = "analyst", WorkspaceId = 5 };

        _memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(callerWsMembership);
        _orgMemberRepo.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(callerOrgMembership);
        _orgMemberRepo.Setup(r => r.GetAsync(10, 1, It.IsAny<CancellationToken>())).ReturnsAsync(targetOrgMembership);
        _memberRepo.Setup(r => r.GetAsync(10, 5, It.IsAny<CancellationToken>())).ReturnsAsync((UserRoleWorkspace?)null);
        _roleRepo.Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(role);

        var result = await _sut.AddMemberAsync(5, 1, new AddWorkspaceMemberRequest(10, 2));

        result.UserId.Should().Be(10);
        _memberRepo.Verify(r => r.AddAsync(It.IsAny<UserRoleWorkspace>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_CallerHasOrgLevelPermission_RemovesMemberSuccessfully()
    {
        var callerWsMembership = new UserRoleWorkspace
        {
            UserId = 1, WorkspaceId = 6,
            Role = new WorkspaceRole { Name = "analyst", RolePermissions = [] }
        };
        var callerOrgMembership = new UserRoleOrganization
        {
            UserId = 1, OrganizationId = 1,
            Role = new OrganizationRole
            {
                Name = "org_admin",
                RolePermissions = [new OrganizationRolePermission { Permission = new Permission { Name = "manage_org_workspace_members" } }]
            }
        };
        var target = new UserRoleWorkspace { UserId = 8, WorkspaceId = 6, Role = new WorkspaceRole { Name = "analyst", RolePermissions = [] } };

        _memberRepo.Setup(r => r.GetAsync(1, 6, It.IsAny<CancellationToken>())).ReturnsAsync(callerWsMembership);
        _orgMemberRepo.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(callerOrgMembership);
        _memberRepo.Setup(r => r.GetAsync(8, 6, It.IsAny<CancellationToken>())).ReturnsAsync(target);

        await _sut.RemoveAsync(6, 8, 1);

        _memberRepo.Verify(r => r.RemoveAsync(target, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_OrgMemberWithoutManageOrgWorkspaceMembersPermission_ThrowsUnauthorizedAccessException()
    {
        var callerWsMembership = new UserRoleWorkspace
        {
            UserId = 1, WorkspaceId = 6,
            Role = new WorkspaceRole { Name = "analyst", RolePermissions = [] }
        };
        _memberRepo.Setup(r => r.GetAsync(1, 6, It.IsAny<CancellationToken>())).ReturnsAsync(callerWsMembership);
        _orgMemberRepo
            .Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization
            {
                UserId = 1, OrganizationId = 1,
                Role = new OrganizationRole { Name = "org_viewer", RolePermissions = [] }
            });

        var act = () => _sut.RemoveAsync(6, 8, 1);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*remove_ws_members*");
        _memberRepo.Verify(r => r.RemoveAsync(It.IsAny<UserRoleWorkspace>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
