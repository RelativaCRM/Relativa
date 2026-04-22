using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Relativa.Core.Application.DTOs.Invitation;
using Relativa.Core.Application.DTOs.Role;
using Relativa.Core.Application.DTOs.Workspace;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class WorkspaceIsolationTests
{
    private readonly Mock<IUserRoleWorkspaceRepository> _memberRepo = new();

    private readonly WorkspaceService _workspaceSvc;
    private readonly WorkspaceMemberService _memberSvc;
    private readonly RoleService _roleSvc;
    private readonly InvitationService _invitationSvc;

    public WorkspaceIsolationTests()
    {
        var updateValidator = new Mock<IValidator<UpdateWorkspaceRequest>>();
        updateValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateWorkspaceRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var inviteValidator = new Mock<IValidator<InviteMemberRequest>>();
        inviteValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<InviteMemberRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var createRoleValidator = new Mock<IValidator<CreateRoleRequest>>();
        createRoleValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateRoleRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _workspaceSvc = new WorkspaceService(
            new Mock<IWorkspaceRepository>().Object,
            _memberRepo.Object,
            new Mock<IWorkspaceRoleRepository>().Object,
            new Mock<IUserRoleOrganizationRepository>().Object,
            new Mock<IValidator<CreateWorkspaceRequest>>().Object,
            updateValidator.Object);

        _memberSvc = new WorkspaceMemberService(
            _memberRepo.Object,
            new Mock<IWorkspaceRoleRepository>().Object,
            new Mock<IUserRoleOrganizationRepository>().Object,
            new Mock<IWorkspaceRepository>().Object);

        _roleSvc = new RoleService(
            new Mock<IWorkspaceRoleRepository>().Object,
            new Mock<IPermissionRepository>().Object,
            _memberRepo.Object,
            createRoleValidator.Object);

        _invitationSvc = new InvitationService(
            new Mock<IWorkspaceInvitationRepository>().Object,
            _memberRepo.Object,
            new Mock<IWorkspaceRoleRepository>().Object,
            new Mock<IOrgInvitationRepository>().Object,
            inviteValidator.Object,
            new Mock<IValidator<AcceptInvitationRequest>>().Object);
    }

    private void SetupMemberInWorkspace(int userId, int workspaceId) =>
        _memberRepo
            .Setup(r => r.GetAsync(userId, workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleWorkspace
            {
                UserId = userId,
                WorkspaceId = workspaceId,
                Role = new WorkspaceRole { Name = "ws_admin", RolePermissions = [] }
            });

    private void SetupNotMemberOfWorkspace(int userId, int workspaceId) =>
        _memberRepo
            .Setup(r => r.GetAsync(userId, workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleWorkspace?)null);

    [Fact]
    public async Task GetWorkspace_UserMemberOfWorkspaceA_BlockedForWorkspaceB()
    {
        SetupMemberInWorkspace(userId: 1, workspaceId: 1);
        SetupNotMemberOfWorkspace(userId: 1, workspaceId: 2);

        var act = () => _workspaceSvc.GetByIdAsync(workspaceId: 2, userId: 1);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You are not a member of this workspace.");
    }

    [Fact]
    public async Task UpdateWorkspace_UserMemberOfWorkspaceA_BlockedForWorkspaceB()
    {
        SetupMemberInWorkspace(userId: 1, workspaceId: 1);
        SetupNotMemberOfWorkspace(userId: 1, workspaceId: 2);

        var act = () => _workspaceSvc.UpdateAsync(workspaceId: 2, userId: 1, new UpdateWorkspaceRequest("Name"));

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You are not a member of this workspace.");
    }

    [Fact]
    public async Task GetMembers_UserMemberOfWorkspaceA_BlockedForWorkspaceB()
    {
        SetupMemberInWorkspace(userId: 1, workspaceId: 1);
        SetupNotMemberOfWorkspace(userId: 1, workspaceId: 2);

        var act = () => _memberSvc.GetMembersAsync(workspaceId: 2, userId: 1);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You are not a member of this workspace.");
    }

    [Fact]
    public async Task UpdateMemberRole_UserMemberOfWorkspaceA_BlockedForWorkspaceB()
    {
        SetupMemberInWorkspace(userId: 1, workspaceId: 1);
        SetupNotMemberOfWorkspace(userId: 1, workspaceId: 2);

        var act = () => _memberSvc.UpdateRoleAsync(2, 5, 1, new DTOs.Member.UpdateMemberRoleRequest(3));

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You are not a member of this workspace.");
    }

    [Fact]
    public async Task InviteToWorkspace_UserMemberOfWorkspaceA_BlockedForWorkspaceB()
    {
        SetupMemberInWorkspace(userId: 1, workspaceId: 1);
        SetupNotMemberOfWorkspace(userId: 1, workspaceId: 2);

        var act = () => _invitationSvc.InviteAsync(2, 1, new InviteMemberRequest("new@relativa.io", 2));

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You are not a member of this workspace.");
    }

    [Fact]
    public async Task GetRoles_UserMemberOfWorkspaceA_BlockedForWorkspaceB()
    {
        SetupMemberInWorkspace(userId: 1, workspaceId: 1);
        SetupNotMemberOfWorkspace(userId: 1, workspaceId: 2);

        var act = () => _roleSvc.GetByWorkspaceAsync(workspaceId: 2, userId: 1);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You are not a member of this workspace.");
    }
}
