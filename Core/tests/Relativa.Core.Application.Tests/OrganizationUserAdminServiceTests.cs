using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Relativa.Authentication.Application.DTOs;
using Relativa.Authentication.Application.Interfaces;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Core.Application.Authorization;
using Relativa.Core.Application.DTOs.Organization;
using Relativa.Core.Application.Exceptions;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
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

    [Fact]
    public async Task CreateOrgUserAsync_WithoutRole_UsesDefaultMemberRole()
    {
        const int orgId = 10;
        const int callerId = 5;
        var request = new CreateOrgUserRequest("A", "B", "user@example.com", "Password123", null);

        _orgMemberRepository
            .Setup(r => r.GetAsync(callerId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrgMember(callerId, orgId, OrganizationPermissions.CreateOrgUsers));
        _orgRoleRepository
            .Setup(r => r.GetSystemRoleByNameAsync("org_member", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrganizationRole { Id = 2, Name = "org_member", IsArchived = false });
        _userProvisioning
            .Setup(s => s.CreateUserAsync(It.IsAny<RegisterRequestDto>(), callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegisterResponseDto(100, "user@example.com", "A", "B"));

        await _sut.CreateOrgUserAsync(orgId, callerId, request);

        _orgMemberRepository.Verify(r =>
            r.AddAsync(It.Is<UserRoleOrganization>(m => m.OrganizationId == orgId && m.UserId == 100 && m.OrgRoleId == 2),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrgUserAsync_CustomRoleWithoutAssignPermission_ThrowsForbidden()
    {
        const int orgId = 10;
        const int callerId = 5;
        var request = new CreateOrgUserRequest("A", "B", "user@example.com", "Password123", 9);

        _orgMemberRepository
            .Setup(r => r.GetAsync(callerId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrgMember(callerId, orgId, OrganizationPermissions.CreateOrgUsers));
        _orgRoleRepository
            .Setup(r => r.GetSystemRoleByNameAsync("org_member", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrganizationRole { Id = 2, Name = "org_member", IsArchived = false });
        _userProvisioning
            .Setup(s => s.CreateUserAsync(It.IsAny<RegisterRequestDto>(), callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegisterResponseDto(100, "user@example.com", "A", "B"));

        var act = () => _sut.CreateOrgUserAsync(orgId, callerId, request);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*assign_org_roles*");
    }

    [Fact]
    public async Task DeleteOrgUserAsync_DomainMismatch_ThrowsForbidden()
    {
        const int orgId = 10;
        const int callerId = 5;
        const int targetId = 7;

        _orgMemberRepository
            .Setup(r => r.GetAsync(callerId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrgMember(callerId, orgId, OrganizationPermissions.DeleteOrgUsers));
        _orgMemberRepository
            .Setup(r => r.GetAsync(targetId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization { UserId = targetId, OrganizationId = orgId, IsArchived = false });
        _userRepository
            .Setup(r => r.GetByIdAsync(callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = callerId, Email = "admin@hjk.com" });
        _userRepository
            .Setup(r => r.GetByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = targetId, Email = "member@ghi.com" });

        var act = () => _sut.DeleteOrgUserAsync(orgId, targetId, callerId);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*same email domain*");
    }

    [Fact]
    public async Task DeleteOrgUserAsync_NoDeletePermission_ThrowsForbidden()
    {
        const int orgId = 10;
        const int callerId = 5;

        _orgMemberRepository
            .Setup(r => r.GetAsync(callerId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrgMember(callerId, orgId));

        var act = () => _sut.DeleteOrgUserAsync(orgId, 7, callerId);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*delete_org_users*");
    }

    private static UserRoleOrganization OrgMember(int userId, int organizationId, params string[] permissions) =>
        new()
        {
            UserId = userId,
            OrganizationId = organizationId,
            Role = new OrganizationRole
            {
                Name = "org_custom",
                RolePermissions = permissions
                    .Select(p => new OrganizationRolePermission
                    {
                        Permission = new Permission { Name = p }
                    })
                    .ToList()
            }
        };
}
