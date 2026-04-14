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
    private readonly Mock<IWorkspaceMemberRepository> _memberRepo = new();
    private readonly Mock<IRoleRepository> _roleRepo = new();
    private readonly WorkspaceMemberService _sut;

    public WorkspaceMemberServiceTests()
    {
        _sut = new WorkspaceMemberService(
            _memberRepo.Object,
            _roleRepo.Object
        );
    }

    private WorkspaceMember MemberWithPermission(int userId, int workspaceId, string permission) =>
        new()
        {
            UserId = userId,
            WorkspaceId = workspaceId,
            Role = new Role
            {
                Name = "sales_manager",
                RolePermissions =
                [
                    new RolePermission { Permission = new Permission { Name = permission } }
                ]
            }
        };

    [Fact]
    public async Task GetMembersAsync_UserNotMember_ThrowsUnauthorizedAccessException()
    {
        _memberRepo
            .Setup(r => r.GetAsync(10, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkspaceMember?)null);

        var act = () => _sut.GetMembersAsync(3, 10);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You are not a member of this workspace.");
    }

    [Fact]
    public async Task GetMembersAsync_ValidMember_ReturnsOnlyActiveMembers()
    {
        var caller = MemberWithPermission(1, 2, "can_view_analytics");
        var members = new List<WorkspaceMember>
        {
            new()
            {
                UserId = 1,
                WorkspaceId = 2,
                IsArchived = false,
                JoinedAt = DateTime.UtcNow,
                User = new User { FirstName = "Andriy", LastName = "Savchenko", Email = "savchenko@relativa.io" },
                Role = new Role { Name = "admin" }
            },
            new()
            {
                UserId = 2,
                WorkspaceId = 2,
                IsArchived = true,
                JoinedAt = DateTime.UtcNow,
                User = new User { FirstName = "Iryna", LastName = "Tkach", Email = "tkach@relativa.io" },
                Role = new Role { Name = "analyst" }
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
        var caller = new WorkspaceMember
        {
            UserId = 5,
            WorkspaceId = 4,
            Role = new Role { Name = "analyst", RolePermissions = [] }
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
        var caller = MemberWithPermission(1, 4, "can_assign_roles");

        _memberRepo
            .Setup(r => r.GetAsync(1, 4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _memberRepo
            .Setup(r => r.GetAsync(99, 4, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkspaceMember?)null);

        var act = () => _sut.UpdateRoleAsync(4, 99, 1, new UpdateMemberRoleRequest(3));

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Target user is not a member of this workspace.");
    }

    [Fact]
    public async Task UpdateRoleAsync_RoleFromAnotherWorkspace_ThrowsArgumentException()
    {
        var caller = MemberWithPermission(1, 4, "can_assign_roles");
        var target = new WorkspaceMember { UserId = 2, WorkspaceId = 4, Role = new Role { Name = "analyst", RolePermissions = [] } };
        var foreignRole = new Role { Id = 7, Name = "custom", WorkspaceId = 99 };

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
        var member = new WorkspaceMember { UserId = 3, WorkspaceId = 6, Role = new Role { Name = "analyst", RolePermissions = [] } };

        _memberRepo
            .Setup(r => r.GetAsync(3, 6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        await _sut.RemoveAsync(6, 3, 3);

        _memberRepo.Verify(r => r.RemoveAsync(member, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_RemoveOtherWithoutPermission_ThrowsUnauthorizedAccessException()
    {
        var caller = new WorkspaceMember
        {
            UserId = 1,
            WorkspaceId = 6,
            Role = new Role { Name = "analyst", RolePermissions = [] }
        };

        _memberRepo
            .Setup(r => r.GetAsync(1, 6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);

        var act = () => _sut.RemoveAsync(6, 8, 1);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _memberRepo.Verify(
            r => r.RemoveAsync(It.IsAny<WorkspaceMember>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RemoveAsync_TargetNotMember_ThrowsKeyNotFoundException()
    {
        var caller = MemberWithPermission(1, 6, "can_assign_roles");

        _memberRepo
            .Setup(r => r.GetAsync(1, 6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _memberRepo
            .Setup(r => r.GetAsync(77, 6, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkspaceMember?)null);

        var act = () => _sut.RemoveAsync(6, 77, 1);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Target user is not a member of this workspace.");
    }
}
