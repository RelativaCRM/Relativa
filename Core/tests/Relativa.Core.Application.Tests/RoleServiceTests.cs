using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Relativa.Core.Application.DTOs.Role;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class RoleServiceTests
{
    private readonly Mock<IRoleRepository> _roleRepo = new();
    private readonly Mock<IPermissionRepository> _permissionRepo = new();
    private readonly Mock<IWorkspaceMemberRepository> _memberRepo = new();
    private readonly Mock<IValidator<CreateRoleRequest>> _createValidator = new();
    private readonly RoleService _sut;

    public RoleServiceTests()
    {
        _sut = new RoleService(
            _roleRepo.Object,
            _permissionRepo.Object,
            _memberRepo.Object,
            _createValidator.Object
        );
    }

    private void SetupValidCreate() =>
        _createValidator
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<CreateRoleRequest>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

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
    public async Task GetByWorkspaceAsync_UserNotMember_ThrowsUnauthorizedAccessException()
    {
        _memberRepo
            .Setup(r => r.GetAsync(5, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkspaceMember?)null);

        var act = () => _sut.GetByWorkspaceAsync(2, 5);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You are not a member of this workspace.");
    }

    [Fact]
    public async Task GetByWorkspaceAsync_ValidMember_ReturnsOnlyActiveRoles()
    {
        var member = MemberWithPermission(1, 3, "can_view_analytics");
        var roles = new List<Role>
        {
            new() { Id = 1, Name = "admin", WorkspaceId = null, IsArchived = false, RolePermissions = [] },
            new() { Id = 2, Name = "old-role", WorkspaceId = 3, IsArchived = true, RolePermissions = [] },
            new() { Id = 3, Name = "reviewer", WorkspaceId = 3, IsArchived = false, RolePermissions = [] }
        };

        _memberRepo
            .Setup(r => r.GetAsync(1, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);
        _roleRepo
            .Setup(r => r.GetByWorkspaceIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roles);

        var result = await _sut.GetByWorkspaceAsync(3, 1);

        result.Should().HaveCount(2);
        result.Should().NotContain(r => r.Name == "old-role");
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesRoleWithPermissions()
    {
        var request = new CreateRoleRequest("pipeline-lead", [1, 2]);
        var caller = MemberWithPermission(1, 4, "can_manage_settings");
        var permissions = new List<Permission>
        {
            new() { Id = 1, Name = "can_edit_deals" },
            new() { Id = 2, Name = "can_view_analytics" }
        };

        SetupValidCreate();
        _memberRepo
            .Setup(r => r.GetAsync(1, 4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _permissionRepo
            .Setup(r => r.GetByIdsAsync(request.PermissionIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions);

        var result = await _sut.CreateAsync(4, 1, request);

        result.Name.Should().Be("pipeline-lead");
        result.IsSystem.Should().BeFalse();
        result.Permissions.Should().HaveCount(2);
        _roleRepo.Verify(r => r.AddAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_InvalidPermissionIds_ThrowsArgumentException()
    {
        var request = new CreateRoleRequest("bad-role", [1, 999]);
        var caller = MemberWithPermission(1, 4, "can_manage_settings");

        SetupValidCreate();
        _memberRepo
            .Setup(r => r.GetAsync(1, 4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _permissionRepo
            .Setup(r => r.GetByIdsAsync(request.PermissionIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Permission { Id = 1, Name = "can_edit_deals" }]);

        var act = () => _sut.CreateAsync(4, 1, request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("One or more permission IDs are invalid.");
    }

    [Fact]
    public async Task UpdateAsync_SystemRole_ThrowsInvalidOperationException()
    {
        var caller = MemberWithPermission(1, 5, "can_manage_settings");
        var systemRole = new Role { Id = 1, Name = "admin", WorkspaceId = null, IsArchived = false, RolePermissions = [] };

        _memberRepo
            .Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _roleRepo
            .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(systemRole);

        var act = () => _sut.UpdateAsync(5, 1, 1, new UpdateRoleRequest("renamed", null));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("System roles cannot be modified.");
        _roleRepo.Verify(r => r.UpdateAsync(It.IsAny<Role>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_RoleFromAnotherWorkspace_ThrowsKeyNotFoundException()
    {
        var caller = MemberWithPermission(1, 5, "can_manage_settings");
        var foreignRole = new Role { Id = 8, Name = "custom", WorkspaceId = 99, IsArchived = false, RolePermissions = [] };

        _memberRepo
            .Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _roleRepo
            .Setup(r => r.GetByIdAsync(8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(foreignRole);

        var act = () => _sut.UpdateAsync(5, 8, 1, new UpdateRoleRequest("renamed", null));

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Role not found in this workspace.");
    }

    [Fact]
    public async Task ArchiveAsync_SystemRole_ThrowsInvalidOperationException()
    {
        var caller = MemberWithPermission(1, 5, "can_manage_settings");
        var systemRole = new Role { Id = 2, Name = "sales_manager", WorkspaceId = null, IsArchived = false };

        _memberRepo
            .Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _roleRepo
            .Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(systemRole);

        var act = () => _sut.ArchiveAsync(5, 2, 1);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("System roles cannot be deleted.");
    }

    [Fact]
    public async Task ArchiveAsync_ValidCustomRole_SetsIsArchivedTrue()
    {
        var caller = MemberWithPermission(1, 5, "can_manage_settings");
        var customRole = new Role { Id = 9, Name = "pipeline-lead", WorkspaceId = 5, IsArchived = false };

        _memberRepo
            .Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _roleRepo
            .Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customRole);

        await _sut.ArchiveAsync(5, 9, 1);

        customRole.IsArchived.Should().BeTrue();
        _roleRepo.Verify(r => r.UpdateAsync(customRole, It.IsAny<CancellationToken>()), Times.Once);
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
}
