using FluentAssertions;
using Moq;
using Relativa.Core.Application.DTOs.Member;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class WorkspaceMemberServiceTests
{
    private readonly Mock<IUserRoleWorkspaceRepository> _memberRepo = new();
    private readonly Mock<IWorkspaceRoleRepository> _roleRepo = new();
    private readonly Mock<IUserRoleOrganizationRepository> _orgMemberRepo = new();
    private readonly Mock<IWorkspaceRepository> _workspaceRepo = new();
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

        _sut = new WorkspaceMemberService(
            _memberRepo.Object,
            _roleRepo.Object,
            _orgMemberRepo.Object,
            _workspaceRepo.Object
        );
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

    [Fact]
    public async Task GetMembersAsync_UserNotMember_ThrowsUnauthorizedAccessException()
    {
        _memberRepo
            .Setup(r => r.GetAsync(10, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleWorkspace?)null);

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
                UserId = 1,
                WorkspaceId = 2,
                IsArchived = false,
                JoinedAt = DateTime.UtcNow,
                User = new User { FirstName = "Andriy", LastName = "Savchenko", Email = "savchenko@relativa.io" },
                Role = new WorkspaceRole { Name = "ws_admin" }
            },
            new()
            {
                UserId = 2,
                WorkspaceId = 2,
                IsArchived = true,
                JoinedAt = DateTime.UtcNow,
                User = new User { FirstName = "Iryna", LastName = "Tkach", Email = "tkach@relativa.io" },
                Role = new WorkspaceRole { Name = "analyst" }
            }
        };

        _memberRepo
            .Setup(r => r.GetAsync(1, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _memberRepo
            .Setup(r => r.GetByWorkspaceIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members);

        var result = await _sut.GetMembersAsync(2, 1);

        result.Should().HaveCount(1);
        result[0].Email.Should().Be("savchenko@relativa.io");
    }

    [Fact]
    public async Task UpdateRoleAsync_UserLacksPermission_ThrowsUnauthorizedAccessException()
    {
        var caller = new UserRoleWorkspace
        {
            UserId = 5,
            WorkspaceId = 4,
            Role = new WorkspaceRole { Name = "analyst", RolePermissions = [] }
        };

        _memberRepo
            .Setup(r => r.GetAsync(5, 4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);

        var act = () => _sut.UpdateRoleAsync(4, 9, 5, new UpdateMemberRoleRequest(2));

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _roleRepo.Verify(
            r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateRoleAsync_TargetNotMember_ThrowsKeyNotFoundException()
    {
        var caller = MemberWithPermission(1, 4, "assign_ws_roles");

        _memberRepo
            .Setup(r => r.GetAsync(1, 4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _memberRepo
            .Setup(r => r.GetAsync(99, 4, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleWorkspace?)null);

        var act = () => _sut.UpdateRoleAsync(4, 99, 1, new UpdateMemberRoleRequest(3));

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Target user is not a member of this workspace.");
    }

    [Fact]
    public async Task UpdateRoleAsync_RoleFromAnotherWorkspace_ThrowsArgumentException()
    {
        var caller = MemberWithPermission(1, 4, "assign_ws_roles");
        var target = new UserRoleWorkspace { UserId = 2, WorkspaceId = 4, Role = new WorkspaceRole { Name = "analyst", RolePermissions = [] } };
        var foreignRole = new WorkspaceRole { Id = 7, Name = "custom", WorkspaceId = 99 };

        _memberRepo
            .Setup(r => r.GetAsync(1, 4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _memberRepo
            .Setup(r => r.GetAsync(2, 4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);
        _roleRepo
            .Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(foreignRole);

        var act = () => _sut.UpdateRoleAsync(4, 2, 1, new UpdateMemberRoleRequest(7));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("The specified role does not belong to this workspace.");
    }

    [Fact]
    public async Task RemoveAsync_SelfRemove_SkipsPermissionCheck()
    {
        var member = new UserRoleWorkspace { UserId = 3, WorkspaceId = 6, Role = new WorkspaceRole { Name = "analyst", RolePermissions = [] } };

        _memberRepo
            .Setup(r => r.GetAsync(3, 6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        await _sut.RemoveAsync(6, 3, 3);

        _memberRepo.Verify(r => r.RemoveAsync(member, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_RemoveOtherWithoutPermission_ThrowsUnauthorizedAccessException()
    {
        var caller = new UserRoleWorkspace
        {
            UserId = 1,
            WorkspaceId = 6,
            Role = new WorkspaceRole { Name = "analyst", RolePermissions = [] }
        };

        _memberRepo
            .Setup(r => r.GetAsync(1, 6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);

        var act = () => _sut.RemoveAsync(6, 8, 1);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _memberRepo.Verify(
            r => r.RemoveAsync(It.IsAny<UserRoleWorkspace>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RemoveAsync_TargetNotMember_ThrowsKeyNotFoundException()
    {
        var caller = MemberWithPermission(1, 6, "remove_ws_members");

        _memberRepo
            .Setup(r => r.GetAsync(1, 6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _memberRepo
            .Setup(r => r.GetAsync(77, 6, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleWorkspace?)null);

        var act = () => _sut.RemoveAsync(6, 77, 1);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Target user is not a member of this workspace.");
    }
}
