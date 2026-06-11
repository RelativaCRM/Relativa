using FluentAssertions;
using Moq;
using Relativa.Core.Application.Authorization;
using Relativa.Core.Application.DTOs.Member;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class WorkspaceMemberDemoteBranchTests
{
    private readonly Mock<IUserRoleWorkspaceRepository> _memberRepo = new();
    private readonly Mock<IWorkspaceRoleRepository> _roleRepo = new();
    private readonly Mock<IUserRoleOrganizationRepository> _orgMemberRepo = new();
    private readonly Mock<IWorkspaceRepository> _workspaceRepo = new();
    private readonly Mock<IOutboxWriter> _audit = new();
    private readonly WorkspaceMemberService _sut;

    private const int Ws = 5;

    public WorkspaceMemberDemoteBranchTests()
    {
        _workspaceRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => new Workspace { Id = id, OrganizationId = 1, Name = "WS" });
        _orgMemberRepo.Setup(r => r.GetAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleOrganization?)null);

        var evaluator = new WorkspaceAccessEvaluator(
            _memberRepo.Object, _orgMemberRepo.Object, _workspaceRepo.Object, _roleRepo.Object);

        _sut = new WorkspaceMemberService(
            _memberRepo.Object, _roleRepo.Object, _orgMemberRepo.Object,
            _workspaceRepo.Object, evaluator, _audit.Object);
    }

    private static WorkspaceRole FullAuthorityRole(string name = "owner") =>
        new()
        {
            Name = name,
            RolePermissions = WorkspacePermissions.FullWorkspaceAuthority
                .Select(p => new WorkspaceRolePermission { Permission = new Permission { Name = p } }).ToList()
        };

    private static WorkspaceRole NamedRole(string name, params string[] permissions) =>
        new()
        {
            Name = name,
            RolePermissions = permissions.Select(p => new WorkspaceRolePermission { Permission = new Permission { Name = p } }).ToList()
        };

    private static UserRoleWorkspace Member(int userId, WorkspaceRole role) =>
        new() { UserId = userId, WorkspaceId = Ws, Role = role };

    [Fact]
    public async Task Demote_FullAuthorityTarget_WithAnotherFullAuthorityMember_Succeeds()
    {
        _memberRepo.Setup(r => r.GetAsync(1, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(Member(1, FullAuthorityRole()));
        _memberRepo.Setup(r => r.GetAsync(2, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(Member(2, FullAuthorityRole()));
        _roleRepo.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(new WorkspaceRole { Id = 3, Name = "member", WorkspaceId = Ws });
        _memberRepo.Setup(r => r.GetByWorkspaceIdAsync(Ws, It.IsAny<CancellationToken>())).ReturnsAsync(
            [Member(1, FullAuthorityRole()), Member(2, FullAuthorityRole())]);

        await _sut.UpdateRoleAsync(Ws, targetUserId: 2, callerUserId: 1, new UpdateMemberRoleRequest(3));

        _memberRepo.Verify(r => r.UpdateAsync(It.Is<UserRoleWorkspace>(m => m.WsRoleId == 3), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Demote_WsAdminFallbackTarget_WithAnotherWsAdminMember_Succeeds()
    {
        _memberRepo.Setup(r => r.GetAsync(1, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(Member(1, NamedRole("ws_admin", "assign_ws_roles")));
        _memberRepo.Setup(r => r.GetAsync(2, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(Member(2, NamedRole("ws_admin")));
        _roleRepo.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(new WorkspaceRole { Id = 3, Name = "member", WorkspaceId = Ws });
        _memberRepo.Setup(r => r.GetByWorkspaceIdAsync(Ws, It.IsAny<CancellationToken>())).ReturnsAsync(
            [Member(1, NamedRole("ws_admin", "assign_ws_roles")), Member(2, NamedRole("ws_admin"))]);

        await _sut.UpdateRoleAsync(Ws, targetUserId: 2, callerUserId: 1, new UpdateMemberRoleRequest(3));

        _memberRepo.Verify(r => r.UpdateAsync(It.Is<UserRoleWorkspace>(m => m.WsRoleId == 3), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Demote_WsAdminFallbackTarget_AsLastAdmin_Throws()
    {
        _memberRepo.Setup(r => r.GetAsync(1, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(Member(1, NamedRole("ws_admin", "assign_ws_roles")));
        _memberRepo.Setup(r => r.GetAsync(2, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(Member(2, NamedRole("ws_admin")));
        _roleRepo.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(new WorkspaceRole { Id = 3, Name = "member", WorkspaceId = Ws });
        _memberRepo.Setup(r => r.GetByWorkspaceIdAsync(Ws, It.IsAny<CancellationToken>())).ReturnsAsync(
            [Member(2, NamedRole("ws_admin"))]);

        await _sut.Invoking(s => s.UpdateRoleAsync(Ws, targetUserId: 2, callerUserId: 1, new UpdateMemberRoleRequest(3)))
            .Should().ThrowAsync<Relativa.Core.Application.Exceptions.AppException>()
            .Where(e => e.Code == "cannot_demote_last_admin");
    }
}
