using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Relativa.Core.Application.DTOs.Role;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class RoleServiceTests
{
    private readonly Mock<IWorkspaceRoleRepository> _roleRepo = new();
    private readonly Mock<IPermissionRepository> _permissionRepo = new();
    private readonly Mock<IUserRoleWorkspaceRepository> _memberRepo = new();
    private readonly Mock<IUserRoleOrganizationRepository> _orgMemberRepo = new();
    private readonly Mock<IWorkspaceRepository> _workspaceRepo = new();
    private readonly WorkspaceAccessEvaluator _workspaceAccessEvaluator;
    private readonly Mock<IValidator<CreateRoleRequest>> _createValidator = new();
    private readonly Mock<IOutboxWriter> _auditOutboxWriter = new();
    private readonly RoleService _sut;

    public RoleServiceTests()
    {
        _orgMemberRepo
            .Setup(r => r.GetAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleOrganization?)null);
        _workspaceRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) =>
                new Workspace { Id = id, OrganizationId = 1, Name = "Test WS", IsArchived = false });

        _workspaceAccessEvaluator = new WorkspaceAccessEvaluator(
            _memberRepo.Object,
            _orgMemberRepo.Object,
            _workspaceRepo.Object,
            _roleRepo.Object);

        _sut = new RoleService(
            _roleRepo.Object,
            _permissionRepo.Object,
            _workspaceAccessEvaluator,
            _createValidator.Object,
            _auditOutboxWriter.Object);
    }

    private void SetupValidCreate() =>
        _createValidator
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<CreateRoleRequest>>(),
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
                    new WorkspaceRolePermission { Permission = new Permission { Name = permission } }
                ]
            }
        };

    private UserRoleWorkspace MemberWithNoPermissions(int userId, int workspaceId) =>
        new()
        {
            UserId = userId,
            WorkspaceId = workspaceId,
            Role = new WorkspaceRole { Name = "viewer", RolePermissions = [] }
        };

    [Fact]
    public async Task GetByWorkspaceAsync_UserNotMember_ThrowsUnauthorizedAccessException()
    {
        _memberRepo
            .Setup(r => r.GetAsync(5, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleWorkspace?)null);

        var act = () => _sut.GetByWorkspaceAsync(2, 5);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You are not a member of this workspace.");
    }

    [Fact]
    public async Task GetByWorkspaceAsync_ValidMember_ReturnsOnlyActiveRoles()
    {
        var member = MemberWithPermission(1, 3, "can_view_analytics");
        var roles = new List<WorkspaceRole>
        {
            new() { Id = 1, Name = "ws_admin", WorkspaceId = null, IsArchived = false, RolePermissions = [] },
            new() { Id = 2, Name = "old-role", WorkspaceId = 3, IsArchived = true, RolePermissions = [] },
            new() { Id = 3, Name = "reviewer", WorkspaceId = 3, IsArchived = false, RolePermissions = [] }
        };

        _memberRepo.Setup(r => r.GetAsync(1, 3, It.IsAny<CancellationToken>())).ReturnsAsync(member);
        _roleRepo.Setup(r => r.GetByWorkspaceIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(roles);

        var result = await _sut.GetByWorkspaceAsync(3, 1);

        result.Should().HaveCount(2);
        result.Should().NotContain(r => r.Name == "old-role");
    }

    [Fact]
    public async Task CreateAsync_UserNotMember_ThrowsUnauthorizedAccessException()
    {
        SetupValidCreate();
        _memberRepo
            .Setup(r => r.GetAsync(7, 4, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleWorkspace?)null);

        var act = () => _sut.CreateAsync(4, 7, new CreateRoleRequest("analyst", [1]));

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You are not a member of this workspace.");
        _roleRepo.Verify(r => r.AddAsync(It.IsAny<WorkspaceRole>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_UserLacksPermission_ThrowsUnauthorizedAccessException()
    {
        SetupValidCreate();
        _memberRepo
            .Setup(r => r.GetAsync(1, 4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MemberWithNoPermissions(1, 4));

        var act = () => _sut.CreateAsync(4, 1, new CreateRoleRequest("analyst", [1]));

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*manage_ws_roles*");
        _roleRepo.Verify(r => r.AddAsync(It.IsAny<WorkspaceRole>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_InvalidPermissionIds_ThrowsArgumentException()
    {
        var request = new CreateRoleRequest("bad-role", [1, 999]);
        var caller = MemberWithPermission(1, 4, "manage_ws_roles");

        SetupValidCreate();
        _memberRepo.Setup(r => r.GetAsync(1, 4, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _permissionRepo
            .Setup(r => r.GetByIdsAsync(request.PermissionIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Permission { Id = 1, Name = "can_edit_deals" }]);

        var act = () => _sut.CreateAsync(4, 1, request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("One or more permission IDs are invalid.");
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesRoleWithPermissions()
    {
        var request = new CreateRoleRequest("pipeline-lead", [1, 2]);
        var caller = MemberWithPermission(1, 4, "manage_ws_roles");
        var permissions = new List<Permission>
        {
            new() { Id = 1, Name = "can_edit_deals" },
            new() { Id = 2, Name = "can_view_analytics" }
        };

        SetupValidCreate();
        _memberRepo.Setup(r => r.GetAsync(1, 4, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _permissionRepo.Setup(r => r.GetByIdsAsync(request.PermissionIds, It.IsAny<CancellationToken>())).ReturnsAsync(permissions);

        var result = await _sut.CreateAsync(4, 1, request);

        result.Name.Should().Be("pipeline-lead");
        result.IsSystem.Should().BeFalse();
        result.Permissions.Should().HaveCount(2);
        _roleRepo.Verify(r => r.AddAsync(It.IsAny<WorkspaceRole>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_EnqueuesWorkspaceRoleCreatedAuditEvent()
    {
        var request = new CreateRoleRequest("pipeline-lead", [1]);
        var caller = MemberWithPermission(3, 4, "manage_ws_roles");
        var permissions = new List<Permission> { new() { Id = 1, Name = "can_edit_deals" } };

        SetupValidCreate();
        _memberRepo.Setup(r => r.GetAsync(3, 4, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _permissionRepo.Setup(r => r.GetByIdsAsync(request.PermissionIds, It.IsAny<CancellationToken>())).ReturnsAsync(permissions);

        await _sut.CreateAsync(4, 3, request);

        _auditOutboxWriter.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<AuditEventContract>(e =>
                    e.AuditScope == AuditRouting.ScopeWorkspace &&
                    e.Action == "workspace_role_created" &&
                    e.TargetId == 4 &&
                    e.ActorUserId == 3 &&
                    e.SourceService == "core"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_UserNotMember_ThrowsUnauthorizedAccessException()
    {
        _memberRepo
            .Setup(r => r.GetAsync(9, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleWorkspace?)null);

        var act = () => _sut.UpdateAsync(5, 1, 9, new UpdateRoleRequest("renamed", null));

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You are not a member of this workspace.");
        _roleRepo.Verify(r => r.UpdateAsync(It.IsAny<WorkspaceRole>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_UserLacksPermission_ThrowsUnauthorizedAccessException()
    {
        _memberRepo
            .Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MemberWithNoPermissions(1, 5));

        var act = () => _sut.UpdateAsync(5, 1, 1, new UpdateRoleRequest("renamed", null));

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*manage_ws_roles*");
        _roleRepo.Verify(r => r.UpdateAsync(It.IsAny<WorkspaceRole>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_RoleNotFound_ThrowsKeyNotFoundException()
    {
        var caller = MemberWithPermission(1, 5, "manage_ws_roles");

        _memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _roleRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((WorkspaceRole?)null);

        var act = () => _sut.UpdateAsync(5, 99, 1, new UpdateRoleRequest("renamed", null));

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Role not found.");
    }

    [Fact]
    public async Task UpdateAsync_SystemRole_ThrowsInvalidOperationException()
    {
        var caller = MemberWithPermission(1, 5, "manage_ws_roles");
        var systemRole = new WorkspaceRole { Id = 1, Name = "ws_admin", WorkspaceId = null, IsArchived = false, RolePermissions = [] };

        _memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _roleRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(systemRole);

        var act = () => _sut.UpdateAsync(5, 1, 1, new UpdateRoleRequest("renamed", null));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("System roles cannot be modified.");
        _roleRepo.Verify(r => r.UpdateAsync(It.IsAny<WorkspaceRole>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_RoleFromAnotherWorkspace_ThrowsKeyNotFoundException()
    {
        var caller = MemberWithPermission(1, 5, "manage_ws_roles");
        var foreignRole = new WorkspaceRole { Id = 8, Name = "custom", WorkspaceId = 99, IsArchived = false, RolePermissions = [] };

        _memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _roleRepo.Setup(r => r.GetByIdAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(foreignRole);

        var act = () => _sut.UpdateAsync(5, 8, 1, new UpdateRoleRequest("renamed", null));

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Role not found in this workspace.");
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_UpdatesNameAndEnqueuesAuditEvent()
    {
        var caller = MemberWithPermission(1, 5, "manage_ws_roles");
        var role = new WorkspaceRole { Id = 9, Name = "old-name", WorkspaceId = 5, IsArchived = false, RolePermissions = [] };

        _memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _roleRepo.Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(role);

        await _sut.UpdateAsync(5, 9, 1, new UpdateRoleRequest("new-name", null));

        role.Name.Should().Be("new-name");
        _roleRepo.Verify(r => r.UpdateAsync(role, It.IsAny<CancellationToken>()), Times.Once);
        _auditOutboxWriter.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<AuditEventContract>(e =>
                    e.AuditScope == AuditRouting.ScopeWorkspace &&
                    e.Action == "workspace_role_updated" &&
                    e.TargetId == 5 &&
                    e.ActorUserId == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ArchiveAsync_UserLacksPermission_ThrowsUnauthorizedAccessException()
    {
        _memberRepo
            .Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MemberWithNoPermissions(1, 5));

        var act = () => _sut.ArchiveAsync(5, 2, 1);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _roleRepo.Verify(r => r.UpdateAsync(It.IsAny<WorkspaceRole>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ArchiveAsync_RoleNotFound_ThrowsKeyNotFoundException()
    {
        var caller = MemberWithPermission(1, 5, "manage_ws_roles");

        _memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _roleRepo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((WorkspaceRole?)null);

        var act = () => _sut.ArchiveAsync(5, 99, 1);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Role not found.");
    }

    [Fact]
    public async Task ArchiveAsync_SystemRole_ThrowsInvalidOperationException()
    {
        var caller = MemberWithPermission(1, 5, "manage_ws_roles");
        var systemRole = new WorkspaceRole { Id = 2, Name = "sales_manager", WorkspaceId = null, IsArchived = false };

        _memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _roleRepo.Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(systemRole);

        var act = () => _sut.ArchiveAsync(5, 2, 1);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("System roles cannot be deleted.");
    }

    [Fact]
    public async Task ArchiveAsync_RoleFromAnotherWorkspace_ThrowsKeyNotFoundException()
    {
        var caller = MemberWithPermission(1, 5, "manage_ws_roles");
        var foreignRole = new WorkspaceRole { Id = 8, Name = "analyst", WorkspaceId = 99, IsArchived = false };

        _memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _roleRepo.Setup(r => r.GetByIdAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(foreignRole);

        var act = () => _sut.ArchiveAsync(5, 8, 1);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Role not found in this workspace.");
    }

    [Fact]
    public async Task ArchiveAsync_ValidCustomRole_SetsIsArchivedTrue()
    {
        var caller = MemberWithPermission(1, 5, "manage_ws_roles");
        var customRole = new WorkspaceRole { Id = 9, Name = "pipeline-lead", WorkspaceId = 5, IsArchived = false };

        _memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _roleRepo.Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(customRole);

        await _sut.ArchiveAsync(5, 9, 1);

        customRole.IsArchived.Should().BeTrue();
        _roleRepo.Verify(r => r.UpdateAsync(customRole, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ArchiveAsync_ValidCustomRole_EnqueuesWorkspaceRoleArchivedAuditEvent()
    {
        var caller = MemberWithPermission(2, 5, "manage_ws_roles");
        var customRole = new WorkspaceRole { Id = 9, Name = "pipeline-lead", WorkspaceId = 5, IsArchived = false };

        _memberRepo.Setup(r => r.GetAsync(2, 5, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _roleRepo.Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(customRole);

        await _sut.ArchiveAsync(5, 9, 2);

        _auditOutboxWriter.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<AuditEventContract>(e =>
                    e.AuditScope == AuditRouting.ScopeWorkspace &&
                    e.Action == "workspace_role_archived" &&
                    e.TargetId == 5 &&
                    e.ActorUserId == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAllPermissionsAsync_ReturnsOnlyActivePermissions()
    {
        var permissions = new List<Permission>
        {
            new() { Id = 1, Name = "can_edit_deals", IsArchived = false },
            new() { Id = 2, Name = "can_view_analytics", IsArchived = false },
            new() { Id = 3, Name = "legacy_perm", IsArchived = true }
        };

        _permissionRepo
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions);

        var result = await _sut.GetAllPermissionsAsync();

        result.Should().HaveCount(2);
        result.Should().NotContain(p => p.Name == "legacy_perm");
    }

    [Fact]
    public async Task UpdateAsync_WithPermissionIds_ClearsAndRebuildsPermissions()
    {
        var caller = MemberWithPermission(1, 5, "manage_ws_roles");
        var role = new WorkspaceRole { Id = 9, Name = "analyst", WorkspaceId = 5, IsArchived = false, RolePermissions = [] };
        var permissions = new List<Permission>
        {
            new() { Id = 1, Name = "can_edit_deals" },
            new() { Id = 2, Name = "can_view_analytics" }
        };

        _memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _roleRepo.Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(role);
        _permissionRepo.Setup(r => r.GetByIdsAsync(It.IsAny<List<int>>(), It.IsAny<CancellationToken>())).ReturnsAsync(permissions);

        await _sut.UpdateAsync(5, 9, 1, new UpdateRoleRequest(null, [1, 2]));

        role.RolePermissions.Should().HaveCount(2);
        role.RolePermissions.Should().Contain(rp => rp.PermissionId == 1);
        role.RolePermissions.Should().Contain(rp => rp.PermissionId == 2);
        _permissionRepo.Verify(r => r.GetByIdsAsync(It.IsAny<List<int>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
