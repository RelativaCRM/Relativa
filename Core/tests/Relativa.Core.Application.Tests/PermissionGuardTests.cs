using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Core.Application.DTOs.Invitation;
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

    private static WorkspaceService BuildWorkspaceService(
        Mock<IUserRoleWorkspaceRepository> memberRepo,
        Mock<IWorkspaceRepository> workspaceRepo,
        Mock<IValidator<UpdateWorkspaceRequest>> updateValidator) =>
        new(workspaceRepo.Object, memberRepo.Object,
            new Mock<IWorkspaceRoleRepository>().Object,
            new Mock<IUserRoleOrganizationRepository>().Object,
            new Mock<IValidator<CreateWorkspaceRequest>>().Object,
            updateValidator.Object);

    private static WorkspaceMemberService BuildMemberService(
        Mock<IUserRoleWorkspaceRepository> memberRepo) =>
        new(memberRepo.Object,
            new Mock<IWorkspaceRoleRepository>().Object,
            new Mock<IUserRoleOrganizationRepository>().Object,
            new Mock<IWorkspaceRepository>().Object);

    private static InvitationService BuildInvitationService(
        Mock<IUserRoleWorkspaceRepository> memberRepo,
        Mock<IWorkspaceRoleRepository> roleRepo,
        Mock<IWorkspaceInvitationRepository> invitationRepo,
        Mock<IValidator<InviteMemberRequest>> inviteValidator)
    {
        var workspaceRepo = new Mock<IWorkspaceRepository>();
        workspaceRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) =>
                new Workspace { Id = id, OrganizationId = id + 1000, Name = "Test WS" });

        return new(invitationRepo.Object, memberRepo.Object, roleRepo.Object,
            new Mock<IOrgInvitationRepository>().Object,
            workspaceRepo.Object,
            new Mock<IUserRoleOrganizationRepository>().Object,
            new Mock<IUserRepository>().Object,
            inviteValidator.Object,
            new Mock<IValidator<AcceptInvitationRequest>>().Object);
    }

    private static RoleService BuildRoleService(
        Mock<IUserRoleWorkspaceRepository> memberRepo,
        Mock<IPermissionRepository> permissionRepo,
        Mock<IWorkspaceRoleRepository> roleRepo,
        Mock<IValidator<CreateRoleRequest>> createValidator) =>
        new(roleRepo.Object, permissionRepo.Object, memberRepo.Object, createValidator.Object);

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
    [InlineData("ws_manager", "invite_to_workspace")]
    [InlineData("ws_analyst", "view_analytics")]
    [InlineData("ws_member",  "view_deals")]
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
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
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
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Theory]
    [InlineData("ws_admin")]
    [InlineData("ws_manager")]
    public async Task InviteToWorkspace_RoleWithPermission_Allowed(string roleName)
    {
        var memberRepo = new Mock<IUserRoleWorkspaceRepository>();
        var roleRepo = new Mock<IWorkspaceRoleRepository>();
        var invitationRepo = new Mock<IWorkspaceInvitationRepository>();
        var inviteValidator = new Mock<IValidator<InviteMemberRequest>>();
        var svc = BuildInvitationService(memberRepo, roleRepo, invitationRepo, inviteValidator);

        inviteValidator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<InviteMemberRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 5, roleName, "invite_to_workspace"));
        roleRepo.Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceRole { Id = 2, Name = "analyst", WorkspaceId = null });

        var act = () => svc.InviteAsync(5, 1, new InviteMemberRequest("test@relativa.io", 2));
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("ws_analyst", "view_analytics")]
    [InlineData("ws_member",  "view_deals")]
    public async Task InviteToWorkspace_RoleWithoutPermission_ThrowsUnauthorized(string roleName, string rolePermission)
    {
        var memberRepo = new Mock<IUserRoleWorkspaceRepository>();
        var inviteValidator = new Mock<IValidator<InviteMemberRequest>>();
        var svc = BuildInvitationService(memberRepo,
            new Mock<IWorkspaceRoleRepository>(),
            new Mock<IWorkspaceInvitationRepository>(),
            inviteValidator);

        inviteValidator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<InviteMemberRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 5, roleName, rolePermission));

        var act = () => svc.InviteAsync(5, 1, new InviteMemberRequest("test@relativa.io", 2));
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task InviteToWorkspace_NonMember_ThrowsUnauthorized()
    {
        var memberRepo = new Mock<IUserRoleWorkspaceRepository>();
        var inviteValidator = new Mock<IValidator<InviteMemberRequest>>();
        var svc = BuildInvitationService(memberRepo,
            new Mock<IWorkspaceRoleRepository>(),
            new Mock<IWorkspaceInvitationRepository>(),
            inviteValidator);

        inviteValidator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<InviteMemberRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        memberRepo.Setup(r => r.GetAsync(9, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleWorkspace?)null);

        var act = () => svc.InviteAsync(5, 9, new InviteMemberRequest("test@relativa.io", 2));
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
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
    [InlineData("ws_manager", "invite_to_workspace")]
    [InlineData("ws_analyst", "view_analytics")]
    [InlineData("ws_member",  "view_deals")]
    public async Task RemoveWsMembers_RoleWithoutPermission_ThrowsUnauthorized(string roleName, string rolePermission)
    {
        var memberRepo = new Mock<IUserRoleWorkspaceRepository>();
        var svc = BuildMemberService(memberRepo);

        memberRepo.Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 5, roleName, rolePermission));

        var act = () => svc.RemoveAsync(5, 99, 1);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RemoveWsMembers_NonMember_ThrowsUnauthorized()
    {
        var memberRepo = new Mock<IUserRoleWorkspaceRepository>();
        var svc = BuildMemberService(memberRepo);

        memberRepo.Setup(r => r.GetAsync(9, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleWorkspace?)null);

        var act = () => svc.RemoveAsync(5, 99, 9);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
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
            .ReturnsAsync([new Permission { Id = 1, Name = "view_deals" }]);

        var act = () => svc.CreateAsync(5, 1, new CreateRoleRequest("custom-role", [1]));
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("ws_manager", "invite_to_workspace")]
    [InlineData("ws_analyst", "view_analytics")]
    [InlineData("ws_member",  "view_deals")]
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
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
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
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
