using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Relativa.Core.Application.DTOs.Workspace;
using Relativa.Core.Application.Exceptions;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class WorkspaceServiceTests
{
    private readonly Mock<IWorkspaceRepository> _workspaceRepo = new();
    private readonly Mock<IUserRoleWorkspaceRepository> _memberRepo = new();
    private readonly Mock<IWorkspaceRoleRepository> _roleRepo = new();
    private readonly Mock<IUserRoleOrganizationRepository> _orgMemberRepo = new();
    private readonly Mock<IValidator<CreateWorkspaceRequest>> _createValidator = new();
    private readonly Mock<IValidator<UpdateWorkspaceRequest>> _updateValidator = new();
    private readonly WorkspaceService _sut;

    public WorkspaceServiceTests()
    {
        _sut = new WorkspaceService(
            _workspaceRepo.Object,
            _memberRepo.Object,
            _roleRepo.Object,
            _orgMemberRepo.Object,
            _createValidator.Object,
            _updateValidator.Object
        );
    }

    private void SetupValidCreate() =>
        _createValidator
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<CreateWorkspaceRequest>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

    private void SetupValidUpdate() =>
        _updateValidator
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<UpdateWorkspaceRequest>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

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
                    new WorkspaceRolePermission
                    {
                        Permission = new Permission { Name = permission }
                    }
                ]
            }
        };

    private UserRoleWorkspace AdminMember(int userId, int workspaceId) =>
        new()
        {
            UserId = userId,
            WorkspaceId = workspaceId,
            Role = new WorkspaceRole { Name = "ws_admin", RolePermissions = [] }
        };

    private UserRoleOrganization OrgMemberWithPermission(int userId, int orgId, string permission) =>
        new()
        {
            UserId = userId,
            OrganizationId = orgId,
            Role = new OrganizationRole
            {
                Name = "org_admin",
                RolePermissions =
                [
                    new OrganizationRolePermission
                    {
                        Permission = new Permission { Name = permission }
                    }
                ]
            }
        };

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesWorkspaceAndAddsCreatorAsAdmin()
    {
        var request = new CreateWorkspaceRequest("Kyiv Sales Team", 10);
        var adminRole = new WorkspaceRole { Id = 1, Name = "ws_admin" };
        var orgMember = OrgMemberWithPermission(42, 10, "create_workspaces");

        SetupValidCreate();
        _orgMemberRepo
            .Setup(r => r.GetAsync(42, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orgMember);
        _roleRepo
            .Setup(r => r.GetSystemRoleByNameAsync("ws_admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminRole);

        Workspace? capturedWorkspace = null;
        _workspaceRepo
            .Setup(r => r.AddAsync(It.IsAny<Workspace>(), It.IsAny<CancellationToken>()))
            .Callback<Workspace, CancellationToken>((w, _) => capturedWorkspace = w);

        UserRoleWorkspace? capturedMember = null;
        _memberRepo
            .Setup(r => r.AddAsync(It.IsAny<UserRoleWorkspace>(), It.IsAny<CancellationToken>()))
            .Callback<UserRoleWorkspace, CancellationToken>((m, _) => capturedMember = m);

        var result = await _sut.CreateAsync(42, request);

        result.Name.Should().Be(request.Name);
        result.OrganizationId.Should().Be(10);
        result.UserRole.Should().Be("ws_admin");
        result.MemberCount.Should().Be(1);
        capturedWorkspace!.CreatedByUserId.Should().Be(42);
        capturedMember!.UserId.Should().Be(42);
        capturedMember.WsRoleId.Should().Be(adminRole.Id);
    }

    [Fact]
    public async Task CreateAsync_AdminRoleNotFound_ThrowsInvalidOperationException()
    {
        var request = new CreateWorkspaceRequest("Lviv Team", 1);
        var orgMember = OrgMemberWithPermission(1, 1, "create_workspaces");

        SetupValidCreate();
        _orgMemberRepo
            .Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orgMember);
        _roleRepo
            .Setup(r => r.GetSystemRoleByNameAsync("ws_admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkspaceRole?)null);

        var act = () => _sut.CreateAsync(1, request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("System ws_admin role not found.");
        _workspaceRepo.Verify(
            r => r.AddAsync(It.IsAny<Workspace>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_InvalidRequest_ThrowsValidationException()
    {
        var request = new CreateWorkspaceRequest("", 1);

        _createValidator
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<CreateWorkspaceRequest>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException(new[]
            {
                new ValidationFailure("Name", "Workspace name is required.")
            }));

        var act = () => _sut.CreateAsync(1, request);

        await act.Should().ThrowAsync<ValidationException>();
        _roleRepo.Verify(
            r => r.GetSystemRoleByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_UserNotMember_ThrowsUnauthorizedAccessException()
    {
        _memberRepo
            .Setup(r => r.GetAsync(99, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleWorkspace?)null);

        var act = () => _sut.GetByIdAsync(5, 99);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You are not a member of this workspace.");
    }

    [Fact]
    public async Task GetByIdAsync_WorkspaceNotFound_ThrowsKeyNotFoundException()
    {
        var member = AdminMember(1, 5);

        _memberRepo
            .Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);
        _workspaceRepo
            .Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Workspace?)null);
        _memberRepo
            .Setup(r => r.GetByWorkspaceIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var act = () => _sut.GetByIdAsync(5, 1);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Workspace not found.");
    }

    [Fact]
    public async Task UpdateAsync_UserLacksPermission_ThrowsUnauthorizedAccessException()
    {
        var request = new UpdateWorkspaceRequest("New Name");
        var member = new UserRoleWorkspace
        {
            UserId = 3,
            WorkspaceId = 10,
            Role = new WorkspaceRole { Name = "analyst", RolePermissions = [] }
        };

        SetupValidUpdate();
        _memberRepo
            .Setup(r => r.GetAsync(3, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var act = () => _sut.UpdateAsync(10, 3, request);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _workspaceRepo.Verify(
            r => r.UpdateAsync(It.IsAny<Workspace>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WorkspaceNotFound_ThrowsKeyNotFoundException()
    {
        var request = new UpdateWorkspaceRequest("Renamed");
        var member = MemberWithPermission(2, 7, "manage_ws_settings");

        SetupValidUpdate();
        _memberRepo
            .Setup(r => r.GetAsync(2, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);
        _workspaceRepo
            .Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Workspace?)null);

        var act = () => _sut.UpdateAsync(7, 2, request);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Workspace not found.");
    }

    [Fact]
    public async Task ArchiveAsync_AdminArchivesWorkspace_SetsIsArchivedTrue()
    {
        var member = AdminMember(1, 3);
        var workspace = new Workspace { Id = 3, Name = "Odesa Pipeline", IsArchived = false };

        _memberRepo
            .Setup(r => r.GetAsync(1, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);
        _workspaceRepo
            .Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);

        await _sut.ArchiveAsync(3, 1);

        workspace.IsArchived.Should().BeTrue();
        _workspaceRepo.Verify(r => r.UpdateAsync(workspace, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ArchiveAsync_NonAdminRole_ThrowsUnauthorizedAccessException()
    {
        var member = MemberWithPermission(5, 3, "manage_ws_settings");

        _memberRepo
            .Setup(r => r.GetAsync(5, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        var act = () => _sut.ArchiveAsync(3, 5);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Only workspace admins can archive a workspace.");
        _workspaceRepo.Verify(
            r => r.UpdateAsync(It.IsAny<Workspace>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetByUserAsync_WithOrganizationId_ReturnsOnlyWorkspacesInOrg()
    {
        var ws1 = new Workspace { Id = 1, Name = "A", OrganizationId = 10, IsArchived = false };
        var ws2 = new Workspace { Id = 2, Name = "B", OrganizationId = 99, IsArchived = false };

        _orgMemberRepo
            .Setup(r => r.GetAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrgMemberWithPermission(1, 10, "create_workspaces"));
        _workspaceRepo
            .Setup(r => r.GetByUserIdAndOrganizationIdAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ws1]);
        _memberRepo
            .Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AdminMember(1, 1));
        _memberRepo
            .Setup(r => r.GetByWorkspaceIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([AdminMember(1, 1)]);

        var result = await _sut.GetByUserAsync(1, 10);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(1);
        result[0].OrganizationId.Should().Be(10);
        _workspaceRepo.Verify(
            r => r.GetByUserIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetByUserAsync_WithOrganizationId_NotOrgMember_ThrowsForbiddenAccessException()
    {
        _orgMemberRepo
            .Setup(r => r.GetAsync(1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleOrganization?)null);

        var act = () => _sut.GetByUserAsync(1, 10);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("You are not a member of this organization.");
        _workspaceRepo.Verify(
            r => r.GetByUserIdAndOrganizationIdAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetByUserAsync_WithoutOrganizationId_ReturnsAllUserWorkspaces()
    {
        var ws1 = new Workspace { Id = 1, Name = "A", OrganizationId = 10, IsArchived = false };
        var ws2 = new Workspace { Id = 2, Name = "B", OrganizationId = 99, IsArchived = false };

        _workspaceRepo
            .Setup(r => r.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ws1, ws2]);
        _memberRepo
            .Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AdminMember(1, 1));
        _memberRepo
            .Setup(r => r.GetAsync(1, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AdminMember(1, 2));
        _memberRepo
            .Setup(r => r.GetByWorkspaceIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([AdminMember(1, 1)]);
        _memberRepo
            .Setup(r => r.GetByWorkspaceIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync([AdminMember(1, 2)]);

        var result = await _sut.GetByUserAsync(1, null);

        result.Should().HaveCount(2);
        result.Select(r => r.OrganizationId).Should().BeEquivalentTo([10, 99]);
    }
}
