using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Relativa.Authentication.Application.DTOs;
using Relativa.Authentication.Application.Interfaces;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Core.Application;
using Relativa.Core.Application.Authorization;
using Relativa.Core.Application.DTOs.Organization;
using Relativa.Core.Application.Exceptions;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class OrganizationUserAdminServiceTests
{
    private readonly Mock<IUserProvisioningService> _userProvisioning = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserRoleOrganizationRepository> _orgMemberRepository = new();
    private readonly Mock<IOrganizationRoleRepository> _orgRoleRepository = new();
    private readonly Mock<IValidator<CreateOrgUserRequest>> _createValidator = new();
    private readonly Mock<IValidator<UpdateOrgUserProfileRequest>> _updateValidator = new();
    private readonly OrganizationUserAdminService _sut;

    public OrganizationUserAdminServiceTests()
    {
        _createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateOrgUserRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _updateValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateOrgUserProfileRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _sut = new OrganizationUserAdminService(
            _userProvisioning.Object,
            _userRepository.Object,
            _orgMemberRepository.Object,
            _orgRoleRepository.Object,
            _createValidator.Object,
            _updateValidator.Object);
    }

    private static CreateOrgUserRequest ValidCreateRequest(int? orgRoleId = null) =>
        new("Alice", "Smith", "alice@company.com", "Password123", orgRoleId);

    private static OrganizationRole DefaultMemberRole() =>
        new() { Id = 2, Name = "org_member", IsArchived = false, Priority = OrganizationRolePriorityTiers.Member };

    private static UserRoleOrganization OrgMember(int userId, int organizationId, params string[] permissions) =>
        new()
        {
            UserId = userId,
            OrganizationId = organizationId,
            Role = new OrganizationRole
            {
                Name = "org_custom",
                Priority = OrganizationRolePriorityTiers.Owner,
                RolePermissions = permissions
                    .Select(p => new OrganizationRolePermission
                    {
                        Permission = new Permission { Name = p }
                    })
                    .ToList()
            }
        };

    private void SetupCallerMembership(int callerId, int orgId, params string[] permissions) =>
        _orgMemberRepository
            .Setup(r => r.GetAsync(callerId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrgMember(callerId, orgId, permissions));

    private void SetupDefaultRole() =>
        _orgRoleRepository
            .Setup(r => r.GetSystemRoleByNameAsync("org_member", It.IsAny<CancellationToken>()))
            .ReturnsAsync(DefaultMemberRole());

    private void SetupProvisioning(int newUserId = 100) =>
        _userProvisioning
            .Setup(s => s.CreateUserAsync(It.IsAny<RegisterRequestDto>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegisterResponseDto(newUserId, "alice@company.com", "Alice", "Smith"));

    [Fact]
    public async Task CreateOrgUserAsync_CallerNotMember_ThrowsUnauthorized()
    {
        _orgMemberRepository
            .Setup(r => r.GetAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleOrganization?)null);

        var act = () => _sut.CreateOrgUserAsync(10, callerUserId: 5, ValidCreateRequest());

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*not a member*");
    }

    [Fact]
    public async Task CreateOrgUserAsync_NoCreatePermission_ThrowsForbidden()
    {
        SetupCallerMembership(5, 10);

        var act = () => _sut.CreateOrgUserAsync(10, callerUserId: 5, ValidCreateRequest());

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage($"*{OrganizationPermissions.CreateOrgUsers}*");
    }

    [Fact]
    public async Task CreateOrgUserAsync_WithoutRole_UsesDefaultMemberRole()
    {
        SetupCallerMembership(5, 10, OrganizationPermissions.CreateOrgUsers);
        SetupDefaultRole();
        SetupProvisioning(newUserId: 100);

        await _sut.CreateOrgUserAsync(10, 5, ValidCreateRequest());

        _orgMemberRepository.Verify(r =>
            r.AddAsync(It.Is<UserRoleOrganization>(m => m.OrgRoleId == 2 && m.UserId == 100 && m.OrganizationId == 10),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrgUserAsync_CustomRoleWithoutAssignPermission_ThrowsForbidden()
    {
        SetupCallerMembership(5, 10, OrganizationPermissions.CreateOrgUsers);
        SetupDefaultRole();
        SetupProvisioning();

        var act = () => _sut.CreateOrgUserAsync(10, 5, ValidCreateRequest(orgRoleId: 9));

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage($"*{OrganizationPermissions.AssignOrgRoles}*");
    }

    [Fact]
    public async Task CreateOrgUserAsync_WithCustomRoleAndAssignPermission_AssignsCustomRole()
    {
        SetupCallerMembership(5, 10,
            OrganizationPermissions.CreateOrgUsers,
            OrganizationPermissions.AssignOrgRoles);
        SetupDefaultRole();
        SetupProvisioning(newUserId: 100);
        _orgRoleRepository
            .Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrganizationRole { Id = 9, Name = "manager", IsArchived = false, OrganizationId = 10 });

        await _sut.CreateOrgUserAsync(10, 5, ValidCreateRequest(orgRoleId: 9));

        _orgMemberRepository.Verify(r =>
            r.AddAsync(It.Is<UserRoleOrganization>(m => m.OrgRoleId == 9),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrgUserAsync_ArchivedCustomRole_ThrowsArgumentException()
    {
        SetupCallerMembership(5, 10,
            OrganizationPermissions.CreateOrgUsers,
            OrganizationPermissions.AssignOrgRoles);
        SetupDefaultRole();
        SetupProvisioning();
        _orgRoleRepository
            .Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrganizationRole { Id = 9, Name = "archived_role", IsArchived = true, OrganizationId = 10 });

        var act = () => _sut.CreateOrgUserAsync(10, 5, ValidCreateRequest(orgRoleId: 9));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*archived*");
    }

    [Fact]
    public async Task CreateOrgUserAsync_CustomRoleFromDifferentOrg_ThrowsArgumentException()
    {
        SetupCallerMembership(5, 10,
            OrganizationPermissions.CreateOrgUsers,
            OrganizationPermissions.AssignOrgRoles);
        SetupDefaultRole();
        SetupProvisioning();
        _orgRoleRepository
            .Setup(r => r.GetByIdAsync(9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrganizationRole { Id = 9, Name = "foreign", IsArchived = false, OrganizationId = 99 });

        var act = () => _sut.CreateOrgUserAsync(10, 5, ValidCreateRequest(orgRoleId: 9));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*does not belong to this organization*");
    }

    [Fact]
    public async Task CreateOrgUserAsync_ValidRequest_EnqueuesAuditEvent()
    {
        var auditWriter = new Mock<IOutboxWriter>();
        var sut = new OrganizationUserAdminService(
            _userProvisioning.Object,
            _userRepository.Object,
            _orgMemberRepository.Object,
            _orgRoleRepository.Object,
            _createValidator.Object,
            _updateValidator.Object,
            auditWriter.Object);

        SetupCallerMembership(5, 10, OrganizationPermissions.CreateOrgUsers);
        SetupDefaultRole();
        SetupProvisioning(newUserId: 100);

        await sut.CreateOrgUserAsync(10, 5, ValidCreateRequest());

        auditWriter.Verify(w =>
            w.EnqueueAuditAsync(
                It.Is<AuditEventContract>(e =>
                    e.ActorUserId == 5 &&
                    e.TargetId == 10 &&
                    e.Action == "organization_member_added_via_user_provisioning" &&
                    e.SourceService == "core"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateOtherUserProfileAsync_NoPermission_ThrowsForbidden()
    {
        SetupCallerMembership(5, 10);

        var act = () => _sut.UpdateOtherUserProfileAsync(10, targetUserId: 7, callerUserId: 5,
            new UpdateOrgUserProfileRequest("Jane", "Doe"));

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage($"*{OrganizationPermissions.EditOtherOrgUsersProfile}*");
    }

    [Fact]
    public async Task UpdateOtherUserProfileAsync_SelfEdit_ThrowsUnauthorized()
    {
        SetupCallerMembership(5, 10, OrganizationPermissions.EditOtherOrgUsersProfile);

        var act = () => _sut.UpdateOtherUserProfileAsync(10, targetUserId: 5, callerUserId: 5,
            new UpdateOrgUserProfileRequest("Jane", "Doe"));

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*own profile*");
    }

    [Fact]
    public async Task UpdateOtherUserProfileAsync_TargetNotMember_ThrowsKeyNotFound()
    {
        SetupCallerMembership(5, 10, OrganizationPermissions.EditOtherOrgUsersProfile);
        _orgMemberRepository
            .Setup(r => r.GetAsync(7, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleOrganization?)null);

        var act = () => _sut.UpdateOtherUserProfileAsync(10, targetUserId: 7, callerUserId: 5,
            new UpdateOrgUserProfileRequest("Jane", "Doe"));

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*not a member*");
    }

    [Fact]
    public async Task UpdateOtherUserProfileAsync_Valid_ReturnsUpdatedProfile()
    {
        SetupCallerMembership(5, 10, OrganizationPermissions.EditOtherOrgUsersProfile);
        _orgMemberRepository
            .Setup(r => r.GetAsync(7, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization
            {
                UserId = 7,
                OrganizationId = 10,
                OrgRoleId = 2,
                Role = new OrganizationRole { Name = "org_member", Priority = OrganizationRolePriorityTiers.Member }
            });
        var expected = new UserProfileDto(7, "jane@co.com", "Jane", "Doe");
        _userProvisioning
            .Setup(s => s.UpdateUserProfileAsync(7, "Jane", "Doe", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.UpdateOtherUserProfileAsync(10, 7, 5, new UpdateOrgUserProfileRequest("Jane", "Doe"));

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task DeleteOrgUserAsync_NoDeletePermission_ThrowsForbidden()
    {
        SetupCallerMembership(5, 10);

        var act = () => _sut.DeleteOrgUserAsync(10, targetUserId: 7, callerUserId: 5);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage($"*{OrganizationPermissions.DeleteOrgUsers}*");
    }

    [Fact]
    public async Task DeleteOrgUserAsync_TargetNotMember_ThrowsKeyNotFound()
    {
        SetupCallerMembership(5, 10, OrganizationPermissions.DeleteOrgUsers);
        _orgMemberRepository
            .Setup(r => r.GetAsync(7, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleOrganization?)null);

        var act = () => _sut.DeleteOrgUserAsync(10, targetUserId: 7, callerUserId: 5);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*not a member*");
    }

    [Fact]
    public async Task DeleteOrgUserAsync_CallerNotFound_ThrowsKeyNotFound()
    {
        SetupCallerMembership(5, 10, OrganizationPermissions.DeleteOrgUsers);
        _orgMemberRepository
            .Setup(r => r.GetAsync(7, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization
            {
                UserId = 7,
                OrganizationId = 10,
                OrgRoleId = 2,
                Role = new OrganizationRole { Name = "org_member", Priority = OrganizationRolePriorityTiers.Member }
            });
        _userRepository
            .Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var act = () => _sut.DeleteOrgUserAsync(10, targetUserId: 7, callerUserId: 5);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Caller*");
    }

    [Fact]
    public async Task DeleteOrgUserAsync_TargetUserNotFound_ThrowsKeyNotFound()
    {
        SetupCallerMembership(5, 10, OrganizationPermissions.DeleteOrgUsers);
        _orgMemberRepository
            .Setup(r => r.GetAsync(7, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization
            {
                UserId = 7,
                OrganizationId = 10,
                OrgRoleId = 2,
                Role = new OrganizationRole { Name = "org_member", Priority = OrganizationRolePriorityTiers.Member }
            });
        _userRepository
            .Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 5, Email = "admin@corp.com" });
        _userRepository
            .Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var act = () => _sut.DeleteOrgUserAsync(10, targetUserId: 7, callerUserId: 5);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*Target*");
    }

    [Fact]
    public async Task DeleteOrgUserAsync_DomainMismatch_ThrowsForbidden()
    {
        SetupCallerMembership(5, 10, OrganizationPermissions.DeleteOrgUsers);
        _orgMemberRepository
            .Setup(r => r.GetAsync(7, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization { UserId = 7, OrganizationId = 10, IsArchived = false });
        _userRepository.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 5, Email = "admin@hjk.com" });
        _userRepository.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 7, Email = "member@ghi.com" });

        var act = () => _sut.DeleteOrgUserAsync(10, 7, 5);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*same email domain*");
    }

    [Fact]
    public async Task DeleteOrgUserAsync_SelfTarget_ThrowsForbiddenAccessException()
    {
        SetupCallerMembership(5, 10, OrganizationPermissions.DeleteOrgUsers);

        var act = () => _sut.DeleteOrgUserAsync(10, targetUserId: 5, callerUserId: 5);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*account settings*");
    }

    [Fact]
    public async Task DeleteOrgUserAsync_EqualRolePriority_ThrowsForbiddenAccessException()
    {
        _orgMemberRepository
            .Setup(r => r.GetAsync(5, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization
            {
                UserId = 5,
                OrganizationId = 10,
                Role = new OrganizationRole
                {
                    Name = "org_admin",
                    Priority = OrganizationRolePriorityTiers.Admin,
                    RolePermissions =
                    [
                        new OrganizationRolePermission
                        {
                            Permission = new Permission { Name = OrganizationPermissions.DeleteOrgUsers }
                        }
                    ]
                }
            });
        _orgMemberRepository
            .Setup(r => r.GetAsync(7, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization
            {
                UserId = 7,
                OrganizationId = 10,
                OrgRoleId = 2,
                Role = new OrganizationRole { Name = "org_admin", Priority = OrganizationRolePriorityTiers.Admin }
            });
        _userRepository.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 5, Email = "a@corp.com" });
        _userRepository.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 7, Email = "b@corp.com" });

        var act = () => _sut.DeleteOrgUserAsync(10, 7, 5);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*equal or higher authority*");
    }

    [Fact]
    public async Task DeleteOrgUserAsync_Valid_ArchivesUserAndEnqueuesOrgAudit()
    {
        var auditWriter = new Mock<IOutboxWriter>();
        var sut = new OrganizationUserAdminService(
            _userProvisioning.Object,
            _userRepository.Object,
            _orgMemberRepository.Object,
            _orgRoleRepository.Object,
            _createValidator.Object,
            _updateValidator.Object,
            auditWriter.Object);

        SetupCallerMembership(5, 10, OrganizationPermissions.DeleteOrgUsers);
        _orgMemberRepository
            .Setup(r => r.GetAsync(7, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization
            {
                UserId = 7,
                OrganizationId = 10,
                OrgRoleId = 2,
                Role = new OrganizationRole { Name = "org_member", Priority = OrganizationRolePriorityTiers.Member }
            });
        _userRepository.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 5, Email = "admin@corp.com" });
        _userRepository.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 7, Email = "member@corp.com" });

        await sut.DeleteOrgUserAsync(10, 7, 5);

        _userProvisioning.Verify(s => s.ArchiveUserAsync(7, 5, It.IsAny<CancellationToken>()), Times.Once);
        auditWriter.Verify(
            w => w.EnqueueAuditAsync(
                It.Is<AuditEventContract>(e =>
                    e.Action == "organization_member_account_archived" &&
                    e.AuditScope == AuditRouting.ScopeOrganization &&
                    e.TargetId == 10 &&
                    e.ActorUserId == 5),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteOrgUserAsync_CallerEmailHasNoAtSign_ThrowsForbiddenAccessException()
    {
        SetupCallerMembership(5, 10, OrganizationPermissions.DeleteOrgUsers);
        _orgMemberRepository
            .Setup(r => r.GetAsync(7, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization
            {
                UserId = 7,
                OrganizationId = 10,
                OrgRoleId = 2,
                Role = new OrganizationRole { Name = "org_member", Priority = OrganizationRolePriorityTiers.Member }
            });
        _userRepository.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 5, Email = "adminnoemail" });
        _userRepository.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 7, Email = "member@corp.com" });

        var act = () => _sut.DeleteOrgUserAsync(10, 7, 5);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*same email domain*");
    }

    [Fact]
    public async Task DeleteOrgUserAsync_CallerEmailEndsWithAtSign_ThrowsForbiddenAccessException()
    {
        SetupCallerMembership(5, 10, OrganizationPermissions.DeleteOrgUsers);
        _orgMemberRepository
            .Setup(r => r.GetAsync(7, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization
            {
                UserId = 7,
                OrganizationId = 10,
                OrgRoleId = 2,
                Role = new OrganizationRole { Name = "org_member", Priority = OrganizationRolePriorityTiers.Member }
            });
        _userRepository.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 5, Email = "admin@" });
        _userRepository.Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 7, Email = "member@corp.com" });

        var act = () => _sut.DeleteOrgUserAsync(10, 7, 5);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*same email domain*");
    }
}
