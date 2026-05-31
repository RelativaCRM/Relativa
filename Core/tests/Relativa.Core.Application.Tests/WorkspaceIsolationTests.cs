using Relativa.Core.Application.Exceptions;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Relativa.Authentication.Domain.Interfaces;
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
    private readonly Mock<IUserRoleOrganizationRepository> _orgRepo = new();
    private readonly Mock<IWorkspaceRepository> _workspaceRepo = new();
    private readonly Mock<IWorkspaceRoleRepository> _workspaceRoleRepo = new();
    private readonly WorkspaceAccessEvaluator _workspaceAccessEvaluator;

    private readonly WorkspaceService _workspaceSvc;
    private readonly WorkspaceMemberService _memberSvc;
    private readonly RoleService _roleSvc;

    public WorkspaceIsolationTests()
    {
        var updateValidator = new Mock<IValidator<UpdateWorkspaceRequest>>();
        updateValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateWorkspaceRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var createRoleValidator = new Mock<IValidator<CreateRoleRequest>>();
        createRoleValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateRoleRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _orgRepo
            .Setup(r => r.GetAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleOrganization?)null);
        _workspaceRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) =>
                new Workspace { Id = id, OrganizationId = id + 1000, Name = "Test WS", IsArchived = false });

        _workspaceAccessEvaluator = new WorkspaceAccessEvaluator(
            _memberRepo.Object,
            _orgRepo.Object,
            _workspaceRepo.Object,
            _workspaceRoleRepo.Object);

        _workspaceSvc = new WorkspaceService(
            _workspaceRepo.Object,
            _memberRepo.Object,
            _workspaceRoleRepo.Object,
            _orgRepo.Object,
            _workspaceAccessEvaluator,
            new Mock<IWorkspaceSettingsRepository>().Object,
            new Mock<IValidator<CreateWorkspaceRequest>>().Object,
            updateValidator.Object,
            new Mock<IValidator<UpdateWorkspaceSettingsRequest>>().Object);

        _memberSvc = new WorkspaceMemberService(
            _memberRepo.Object,
            _workspaceRoleRepo.Object,
            _orgRepo.Object,
            _workspaceRepo.Object,
            _workspaceAccessEvaluator);

        _roleSvc = new RoleService(
            _workspaceRoleRepo.Object,
            new Mock<IPermissionRepository>().Object,
            _workspaceAccessEvaluator,
            createRoleValidator.Object);
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

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("You are not a member of this workspace.");
    }

    [Fact]
    public async Task UpdateWorkspace_UserMemberOfWorkspaceA_BlockedForWorkspaceB()
    {
        SetupMemberInWorkspace(userId: 1, workspaceId: 1);
        SetupNotMemberOfWorkspace(userId: 1, workspaceId: 2);

        var act = () => _workspaceSvc.UpdateAsync(workspaceId: 2, userId: 1, new UpdateWorkspaceRequest("Name"));

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("You are not a member of this workspace.");
    }

    [Fact]
    public async Task GetMembers_UserMemberOfWorkspaceA_BlockedForWorkspaceB()
    {
        SetupMemberInWorkspace(userId: 1, workspaceId: 1);
        SetupNotMemberOfWorkspace(userId: 1, workspaceId: 2);

        var act = () => _memberSvc.GetMembersAsync(workspaceId: 2, userId: 1);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("You are not a member of this workspace.");
    }

    [Fact]
    public async Task UpdateMemberRole_UserMemberOfWorkspaceA_BlockedForWorkspaceB()
    {
        SetupMemberInWorkspace(userId: 1, workspaceId: 1);
        SetupNotMemberOfWorkspace(userId: 1, workspaceId: 2);

        var act = () => _memberSvc.UpdateRoleAsync(2, 5, 1, new DTOs.Member.UpdateMemberRoleRequest(3));

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("You are not a member of this workspace.");
    }

    [Fact]
    public async Task GetRoles_UserMemberOfWorkspaceA_BlockedForWorkspaceB()
    {
        SetupMemberInWorkspace(userId: 1, workspaceId: 1);
        SetupNotMemberOfWorkspace(userId: 1, workspaceId: 2);

        var act = () => _roleSvc.GetByWorkspaceAsync(workspaceId: 2, userId: 1);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("You are not a member of this workspace.");
    }
}
