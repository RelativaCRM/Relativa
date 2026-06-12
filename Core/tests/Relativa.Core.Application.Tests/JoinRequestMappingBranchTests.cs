using FluentAssertions;
using Moq;
using Relativa.Core.Application.DTOs.JoinRequest;
using Relativa.Core.Application.Exceptions;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class JoinRequestMappingBranchTests
{
    private readonly Mock<IJoinRequestRepository> _joinRequestRepo = new();
    private readonly Mock<IUserRoleOrganizationRepository> _orgMemberRepo = new();
    private readonly Mock<IOrganizationRoleRepository> _orgRoleRepo = new();
    private readonly Mock<IOrganizationRepository> _orgRepo = new();
    private readonly Mock<IOrganizationSettingsRepository> _orgSettingsRepo = new();
    private readonly JoinRequestService _sut;

    private const int Org = 1, Caller = 7;

    public JoinRequestMappingBranchTests()
    {
        _sut = new JoinRequestService(
            _joinRequestRepo.Object, _orgMemberRepo.Object, _orgRoleRepo.Object,
            _orgRepo.Object, _orgSettingsRepo.Object);
    }

    private static UserRoleOrganization Member(string? permission) =>
        new()
        {
            UserId = Caller,
            OrganizationId = Org,
            Role = new OrganizationRole
            {
                Name = "r",
                RolePermissions = permission is null
                    ? []
                    : [new OrganizationRolePermission { Permission = new Permission { Name = permission } }]
            }
        };

    [Fact]
    public async Task Submit_InviteOnlyPolicy_Throws()
    {
        _orgRepo.Setup(r => r.GetByIdAsync(Org, It.IsAny<CancellationToken>())).ReturnsAsync(new Organization { Id = Org, Name = "Org" });
        _orgSettingsRepo.Setup(r => r.GetByOrganizationIdAsync(Org, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrganizationSettings { OrganizationId = Org, JoinPolicy = "invite_only" });

        await _sut.Invoking(s => s.SubmitAsync(Org, 20, new CreateJoinRequestRequest(null)))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "org_not_accepting_joins");
    }

    [Fact]
    public async Task GetByOrganization_MapsUserNamePresentAndAbsent()
    {
        _orgMemberRepo.Setup(r => r.GetAsync(Caller, Org, It.IsAny<CancellationToken>())).ReturnsAsync(Member("manage_join_requests"));
        _joinRequestRepo.Setup(r => r.GetByOrganizationIdAsync(Org, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new OrganizationJoinRequest { Id = 1, UserId = 20, OrganizationId = Org, Status = "Pending",
                User = new User { Id = 20, FirstName = "Jane", LastName = "Doe", Email = "jane@test.io" } },
            new OrganizationJoinRequest { Id = 2, UserId = 21, OrganizationId = Org, Status = "Pending", User = null },
        ]);

        var result = await _sut.GetByOrganizationAsync(Org, Caller);

        result.Should().HaveCount(2);
        result.Single(r => r.Id == 1).UserName.Should().Be("Jane Doe");
        result.Single(r => r.Id == 2).UserName.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByOrganization_MembershipRoleNull_DeniesPermission()
    {
        _orgMemberRepo.Setup(r => r.GetAsync(Caller, Org, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization { UserId = Caller, OrganizationId = Org, Role = null });

        await _sut.Invoking(s => s.GetByOrganizationAsync(Org, Caller))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "permission_denied");
    }

    [Fact]
    public async Task GetMyRequests_MapsUserNamePresentAndAbsent()
    {
        _joinRequestRepo.Setup(r => r.GetByUserIdAsync(20, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new OrganizationJoinRequest { Id = 1, UserId = 20, OrganizationId = Org, Status = "Pending",
                User = new User { Id = 20, FirstName = "Jane", LastName = "Doe", Email = "jane@test.io" },
                Organization = new Organization { Id = Org, Name = "Org" } },
            new OrganizationJoinRequest { Id = 2, UserId = 20, OrganizationId = Org, Status = "Cancelled", User = null },
        ]);

        var result = await _sut.GetMyRequestsAsync(20);

        result.Should().HaveCount(2);
        result.Single(r => r.Id == 1).UserName.Should().Be("Jane Doe");
        result.Single(r => r.Id == 2).UserName.Should().BeEmpty();
    }
}
