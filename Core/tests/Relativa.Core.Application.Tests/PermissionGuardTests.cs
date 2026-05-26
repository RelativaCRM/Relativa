using Relativa.Core.Application.Exceptions;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Core.Application.DTOs.Member;
using Relativa.Core.Application.DTOs.Role;
using Relativa.Core.Application.DTOs.Workspace;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class PermissionGuardTests
{
    private static UserRoleWorkspace Member(int userId, int workspaceId, string roleName, params string[] permissions) =>
        new()
        {
            UserId = userId,
            WorkspaceId = workspaceId,
            Role = new WorkspaceRole
            {
                Name = roleName,
                RolePermissions = permissions
                    .Select(p => new WorkspaceRolePermission { Permission = new Permission { Name = p } })
                    .ToList()
            }
        };

    private static UserRoleOrganization OrgMember(int userId, int organizationId, params string[] permissions) =>
        new()
        {
            UserId = userId,
            OrganizationId = organizationId,
            User = new User
            {
                Id = userId,
                FirstName = "Test",
                LastName = "User",
                Email = $"user{userId}@example.com"
            },
            Role = new OrganizationRole
            {
                Name = "org_custom",
                OrganizationId = organizationId,
                RolePermissions = permissions
                    .Select(p => new OrganizationRolePermission { Permission = new Permission { Name = p } })
                    .ToList()
            }
        };

    private static WorkspaceService BuildWorkspaceService(
        Mock<IUserRoleWorkspaceRepository> memberRepo,
        Mock<IWorkspaceRepository> workspaceRepo,
        Mock<IValidator<UpdateWorkspaceRequest>> updateValidator,
        Mock<IUserRoleOrganizationRepository>? orgRepo = null,
        Mock<IWorkspaceRoleRepository>? roleRepo = null)
    {
        var orgMemberRepo = orgRepo ?? new Mock<IUserRoleOrganizationRepository>();
        var wsRoleRepo = roleRepo ?? new Mock<IWorkspaceRoleRepository>();
        var access = new WorkspaceAccessEvaluator(
            memberRepo.Object,
            orgMemberRepo.Object,
            workspaceRepo.Object,
            wsRoleRepo.Object);

        return new WorkspaceService(
            workspaceRepo.Object,
            memberRepo.Object,
            wsRoleRepo.Object,
            orgMemberRepo.Object,
            access,
            new Mock<IOrganizationSettingsRepository>().Object,
            new Mock<IValidator<CreateWorkspaceRequest>>().Object,
            updateValidator.Object);
    }

    private static WorkspaceMemberService BuildMemberService(
        Mock<IUserRoleWorkspaceRepository> memberRepo,
        Mock<IWorkspaceRepository>? workspaceRepo = null,
        Mock<IUserRoleOrganizationRepository>? orgRepo = null,
        Mock<IWorkspaceRoleRepository>? roleRepo = null)
    {
        var wsRepo = workspaceRepo ?? new Mock<IWorkspaceRepository>();
        if (workspaceRepo is null)
        {
            wsRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((int id, CancellationToken _) =>
                    new Workspace { Id = id, OrganizationId = 10, Name = "Test WS", IsArchived = false });
        }

        var orgMemRepo = orgRepo ?? new Mock<IUserRoleOrganizationRepository>();
        if (orgRepo is null)
        {
            orgMemRepo.Setup(r => r.GetAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((UserRoleOrganization?)null);
        }

        var roleRepoImpl = roleRepo ?? new Mock<IWorkspaceRoleRepository>();

        var access = new WorkspaceAccessEvaluator(
            memberRepo.Object,
            orgMemRepo.Object,
            wsRepo.Object,
            roleRepoImpl.Object);

        return new WorkspaceMemberService(
            memberRepo.Object,
            roleRepoImpl.Object,
            orgMemRepo.Object,
            wsRepo.Object,
            access);
    }

    private static RoleService BuildRoleService(
        Mock<IUserRoleWorkspaceRepository> memberRepo,
        Mock<IPermissionRepository> permissionRepo,
        Mock<IWorkspaceRoleRepository> roleRepo,
        Mock<IValidator<CreateRoleRequest>> createValidator)
    {
        var orgMemRepo = new Mock<IUserRoleOrganizationRepository>();
        orgMemRepo.Setup(r => r.GetAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleOrganization?)null);

        var wsRepo = new Mock<IWorkspaceRepository>();
        wsRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) =>
                new Workspace { Id = id, OrganizationId = 10, Name = "Test WS", IsArchived = false });

        var access = new WorkspaceAccessEvaluator(
            memberRepo.Object,
            orgMemRepo.Object,
            wsRepo.Object,
            roleRepo.Object);

        return new RoleService(
            roleRepo.Object,
            permissionRepo.Object,
            access,
            createValidator.Object);
    }

    [Fact]
    public async Task ManageWsSettings_WsAdmin_Allowed()
    {
        var memberRepo = new Mock<IUserRoleWorkspaceRepository>();
        var workspaceRepo = new Mock<IWorkspaceRepository>();
        var updateValidator = new Mock<IValidator<UpdateWorkspaceRequest>>();
        var svc = BuildWorkspaceService(memberRepo, workspaceRepo, updateValidator);

        updateValidator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateWorkspaceRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 5, "ws_admin", "manage_ws_settings"));
        workspaceRepo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Workspace { Id = 5, Name = "Sales WS" });

        var act = () => svc.UpdateAsync(5, 1, new UpdateWorkspaceRequest("New Name"));
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("ws_manager", "add_ws_members")]
    [InlineData("ws_analyst", "view_analytics")]
    [InlineData("ws_member", "view_entities")]
    public async Task ManageWsSettings_RoleWithoutPermission_ThrowsUnauthorized(string roleName, string rolePermission)
    {
        var memberRepo = new Mock<IUserRoleWorkspaceRepository>();
        var updateValidator = new Mock<IValidator<UpdateWorkspaceRequest>>();
        var svc = BuildWorkspaceService(memberRepo, new Mock<IWorkspaceRepository>(), updateValidator);

        updateValidator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateWorkspaceRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 5, roleName, rolePermission));

        var act = () => svc.UpdateAsync(5, 1, new UpdateWorkspaceRequest("New Name"));
        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task ManageWsSettings_NonMember_ThrowsUnauthorized()
    {
        var memberRepo = new Mock<IUserRoleWorkspaceRepository>();
        var updateValidator = new Mock<IValidator<UpdateWorkspaceRequest>>();
        var svc = BuildWorkspaceService(memberRepo, new Mock<IWorkspaceRepository>(), updateValidator);

        updateValidator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateWorkspaceRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        memberRepo.Setup(r => r.GetAsync(9, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleWorkspace?)null);

        var act = () => svc.UpdateAsync(5, 9, new UpdateWorkspaceRequest("New Name"));
        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task AddWorkspaceMember_OrgManageWorkspaceMembers_NotWsMember_Allowed()
    {
        var memberRepo = new Mock<IUserRoleWorkspaceRepository>();
        var orgRepo = new Mock<IUserRoleOrganizationRepository>();
        var roleRepo = new Mock<IWorkspaceRoleRepository>();
        var workspaceRepo = new Mock<IWorkspaceRepository>();

        memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleWorkspace?)null);
        workspaceRepo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Workspace { Id = 5, OrganizationId = 100, Name = "WS", IsArchived = false });
        orgRepo.Setup(r => r.GetAsync(1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrgMember(1, 100, "manage_org_workspace_members"));
        orgRepo.Setup(r => r.GetAsync(99, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrgMember(99, 100, "org_member"));
        memberRepo.Setup(r => r.GetAsync(99, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleWorkspace?)null);
        roleRepo.Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceRole { Id = 2, Name = "ws_member", WorkspaceId = null });
        memberRepo.Setup(r => r.AddAsync(It.IsAny<UserRoleWorkspace>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = BuildMemberService(memberRepo, workspaceRepo, orgRepo, roleRepo);

        var act = () => svc.AddMemberAsync(5, 1, new AddWorkspaceMemberRequest(99, 2));
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RemoveWsMembers_WsAdmin_Allowed()
    {
        var memberRepo = new Mock<IUserRoleWorkspaceRepository>();
        var svc = BuildMemberService(memberRepo);

        memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 5, "ws_admin", "remove_ws_members"));
        memberRepo.Setup(r => r.GetAsync(99, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleWorkspace { UserId = 99, WorkspaceId = 5, Role = new WorkspaceRole { Name = "ws_member", RolePermissions = [] } });

        var act = () => svc.RemoveAsync(5, 99, 1);
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("ws_manager", "add_ws_members")]
    [InlineData("ws_analyst", "view_analytics")]
    [InlineData("ws_member", "view_entities")]
    public async Task RemoveWsMembers_RoleWithoutPermission_ThrowsUnauthorized(string roleName, string rolePermission)
    {
        var memberRepo = new Mock<IUserRoleWorkspaceRepository>();
        var svc = BuildMemberService(memberRepo);

        memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 5, roleName, rolePermission));

        var act = () => svc.RemoveAsync(5, 99, 1);
        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task RemoveWsMembers_NonMember_ThrowsUnauthorized()
    {
        var memberRepo = new Mock<IUserRoleWorkspaceRepository>();
        var svc = BuildMemberService(memberRepo);

        memberRepo.Setup(r => r.GetAsync(9, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleWorkspace?)null);

        var act = () => svc.RemoveAsync(5, 99, 9);
        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task ManageWsRoles_WsAdmin_Allowed()
    {
        var memberRepo = new Mock<IUserRoleWorkspaceRepository>();
        var permissionRepo = new Mock<IPermissionRepository>();
        var roleRepo = new Mock<IWorkspaceRoleRepository>();
        var createValidator = new Mock<IValidator<CreateRoleRequest>>();
        var svc = BuildRoleService(memberRepo, permissionRepo, roleRepo, createValidator);

        createValidator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateRoleRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 5, "ws_admin", "manage_ws_roles"));
        permissionRepo.Setup(r => r.GetByIdsAsync(It.IsAny<List<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Permission { Id = 1, Name = "view_entities" }]);

        var act = () => svc.CreateAsync(5, 1, new CreateRoleRequest("custom-role", [1]));
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("ws_manager", "add_ws_members")]
    [InlineData("ws_analyst", "view_analytics")]
    [InlineData("ws_member", "view_entities")]
    public async Task ManageWsRoles_RoleWithoutPermission_ThrowsUnauthorized(string roleName, string rolePermission)
    {
        var memberRepo = new Mock<IUserRoleWorkspaceRepository>();
        var createValidator = new Mock<IValidator<CreateRoleRequest>>();
        var svc = BuildRoleService(memberRepo, new Mock<IPermissionRepository>(), new Mock<IWorkspaceRoleRepository>(), createValidator);

        createValidator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateRoleRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 5, roleName, rolePermission));

        var act = () => svc.CreateAsync(5, 1, new CreateRoleRequest("custom-role", [1]));
        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task ManageWsRoles_NonMember_ThrowsUnauthorized()
    {
        var memberRepo = new Mock<IUserRoleWorkspaceRepository>();
        var createValidator = new Mock<IValidator<CreateRoleRequest>>();
        var svc = BuildRoleService(memberRepo, new Mock<IPermissionRepository>(), new Mock<IWorkspaceRoleRepository>(), createValidator);

        createValidator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateRoleRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        memberRepo.Setup(r => r.GetAsync(9, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleWorkspace?)null);

        var act = () => svc.CreateAsync(5, 9, new CreateRoleRequest("custom-role", [1]));
        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }
}
