using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Relativa.Core.Application.DTOs.Organization;
using Relativa.Core.Application.Exceptions;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class OrganizationServiceMappingBranchTests
{
    private readonly Mock<IOrganizationRepository> _orgRepo = new();
    private readonly Mock<IUserRoleOrganizationRepository> _orgMemberRepo = new();
    private readonly Mock<IOrganizationRoleRepository> _orgRoleRepo = new();
    private readonly Mock<IOrganizationSettingsRepository> _orgSettingsRepo = new();
    private readonly Mock<IValidator<CreateOrganizationRequest>> _createValidator = new();
    private readonly Mock<IValidator<UpdateOrganizationRequest>> _updateValidator = new();
    private readonly Mock<IValidator<UpdateOrganizationSettingsRequest>> _updateSettingsValidator = new();
    private readonly Mock<IOutboxWriter> _auditOutboxWriter = new();
    private readonly OrganizationService _sut;

    public OrganizationServiceMappingBranchTests()
    {
        _updateValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateOrganizationRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _sut = new OrganizationService(
            _orgRepo.Object,
            _orgMemberRepo.Object,
            _orgRoleRepo.Object,
            _orgSettingsRepo.Object,
            _createValidator.Object,
            _updateValidator.Object,
            _updateSettingsValidator.Object,
            _auditOutboxWriter.Object);
    }

    [Fact]
    public async Task GetByUser_MembershipNull_MapsNullRoleAndEmptyPermissions()
    {
        var org = new Organization { Id = 1, Name = "Ghost Org" };
        _orgRepo.Setup(r => r.GetByUserIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync([org]);
        _orgMemberRepo.Setup(r => r.GetAsync(7, 1, It.IsAny<CancellationToken>())).ReturnsAsync((UserRoleOrganization?)null);
        _orgMemberRepo.Setup(r => r.GetByOrganizationIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await _sut.GetByUserAsync(7);

        var dto = result.Should().ContainSingle().Subject;
        dto.UserRole.Should().BeNull();
        dto.UserRoleDisplayName.Should().BeNull();
        dto.MyPermissions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByUser_PermissionWithBlankAndNullNames_AreFilteredOut()
    {
        var org = new Organization { Id = 1, Name = "Filter Org" };
        var membership = new UserRoleOrganization
        {
            UserId = 7,
            OrganizationId = 1,
            Role = new OrganizationRole
            {
                Name = "org_admin",
                RolePermissions =
                [
                    new OrganizationRolePermission { Permission = new Permission { Name = "manage_org_settings" } },
                    new OrganizationRolePermission { Permission = new Permission { Name = "   " } },
                    new OrganizationRolePermission { Permission = null },
                ]
            }
        };
        _orgRepo.Setup(r => r.GetByUserIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync([org]);
        _orgMemberRepo.Setup(r => r.GetAsync(7, 1, It.IsAny<CancellationToken>())).ReturnsAsync(membership);
        _orgMemberRepo.Setup(r => r.GetByOrganizationIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync([membership]);

        var result = await _sut.GetByUserAsync(7);

        result.Should().ContainSingle().Which.MyPermissions.Should().ContainSingle().Which.Should().Be("manage_org_settings");
    }

    [Fact]
    public async Task GetById_MembershipRoleNull_MapsNullRoleAndEmptyPermissions()
    {
        var membership = new UserRoleOrganization { UserId = 7, OrganizationId = 5, Role = null };
        _orgMemberRepo.Setup(r => r.GetAsync(7, 5, It.IsAny<CancellationToken>())).ReturnsAsync(membership);
        _orgRepo.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(new Organization { Id = 5, Name = "Roleless" });
        _orgMemberRepo.Setup(r => r.GetByOrganizationIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync([membership]);

        var dto = await _sut.GetByIdAsync(5, 7);

        dto.UserRole.Should().BeNull();
        dto.UserRoleDisplayName.Should().BeNull();
        dto.MyPermissions.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_MembershipRoleNull_DeniesPermission()
    {
        var membership = new UserRoleOrganization { UserId = 7, OrganizationId = 5, Role = null };
        _orgMemberRepo.Setup(r => r.GetAsync(7, 5, It.IsAny<CancellationToken>())).ReturnsAsync(membership);

        await _sut.Invoking(s => s.UpdateAsync(5, 7, new UpdateOrganizationRequest("New Name")))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "permission_denied");
    }
}
