using FluentAssertions;
using FluentValidation;
using Moq;
using Relativa.Core.Application.DTOs.Organization;
using Relativa.Core.Application.Exceptions;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class OrganizationMemberBranchTests
{
    private readonly Mock<IOrganizationRepository> _orgRepo = new();
    private readonly Mock<IUserRoleOrganizationRepository> _members = new();
    private readonly Mock<IOrganizationRoleRepository> _roles = new();
    private readonly Mock<IOrganizationSettingsRepository> _settings = new();
    private readonly Mock<IOutboxWriter> _audit = new();
    private readonly OrganizationService _sut;

    private const int Org = 1, Caller = 7, Target = 20;

    public OrganizationMemberBranchTests()
    {
        _audit.Setup(w => w.EnqueueAuditAsync(It.IsAny<AuditEventContract>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _members.Setup(r => r.GetAsync(Caller, Org, It.IsAny<CancellationToken>())).ReturnsAsync(Member(Caller, priority: 1, "remove_org_members", "assign_org_roles"));
        _sut = new OrganizationService(_orgRepo.Object, _members.Object, _roles.Object, _settings.Object,
            Mock.Of<IValidator<CreateOrganizationRequest>>(), Mock.Of<IValidator<UpdateOrganizationRequest>>(), Mock.Of<IValidator<UpdateOrganizationSettingsRequest>>(), _audit.Object);
    }

    private static UserRoleOrganization Member(int userId, int priority, params string[] perms) =>
        new()
        {
            UserId = userId,
            OrganizationId = Org,
            Role = new OrganizationRole { Priority = priority, RolePermissions = perms.Select(p => new OrganizationRolePermission { Permission = new Permission { Name = p } }).ToList() },
        };

    [Fact]
    public async Task RemoveMember_SelfNotMember_Throws()
    {
        _members.Setup(r => r.GetAsync(Caller, Org, It.IsAny<CancellationToken>())).ReturnsAsync((UserRoleOrganization?)null);
        (await Assert.ThrowsAsync<AppException>(() => _sut.RemoveMemberAsync(Org, Caller, Caller))).Code.Should().Be("target_not_org_member");
    }

    [Fact]
    public async Task RemoveMember_SelfLeave_RemovesOwnMembership()
    {
        var self = Member(Caller, 1);
        _members.Setup(r => r.GetAsync(Caller, Org, It.IsAny<CancellationToken>())).ReturnsAsync(self);

        await _sut.RemoveMemberAsync(Org, Caller, Caller);

        _members.Verify(r => r.RemoveAsync(self, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveMember_TargetHasEqualOrHigherAuthority_Throws()
    {
        _members.Setup(r => r.GetAsync(Target, Org, It.IsAny<CancellationToken>())).ReturnsAsync(Member(Target, priority: 1));
        (await Assert.ThrowsAsync<AppException>(() => _sut.RemoveMemberAsync(Org, Target, Caller))).Code.Should().Be("insufficient_role_authority");
    }

    [Fact]
    public async Task RemoveMember_TargetNotMember_Throws()
    {
        _members.Setup(r => r.GetAsync(Target, Org, It.IsAny<CancellationToken>())).ReturnsAsync((UserRoleOrganization?)null);
        (await Assert.ThrowsAsync<AppException>(() => _sut.RemoveMemberAsync(Org, Target, Caller))).Code.Should().Be("target_not_org_member");
    }

    [Fact]
    public async Task RemoveMember_HigherAuthorityCaller_RemovesTarget()
    {
        var target = Member(Target, priority: 3);
        _members.Setup(r => r.GetAsync(Target, Org, It.IsAny<CancellationToken>())).ReturnsAsync(target);

        await _sut.RemoveMemberAsync(Org, Target, Caller);

        _members.Verify(r => r.RemoveAsync(target, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangeRole_TargetNotMember_Throws()
    {
        _members.Setup(r => r.GetAsync(Target, Org, It.IsAny<CancellationToken>())).ReturnsAsync((UserRoleOrganization?)null);
        (await Assert.ThrowsAsync<AppException>(() => _sut.ChangeMemberRoleAsync(Org, Target, Caller, new ChangeOrgMemberRoleRequest(5)))).Code.Should().Be("target_not_org_member");
    }

    [Fact]
    public async Task ChangeRole_RoleNotFound_Throws()
    {
        _members.Setup(r => r.GetAsync(Target, Org, It.IsAny<CancellationToken>())).ReturnsAsync(Member(Target, 3));
        _roles.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync((OrganizationRole?)null);
        (await Assert.ThrowsAsync<AppException>(() => _sut.ChangeMemberRoleAsync(Org, Target, Caller, new ChangeOrgMemberRoleRequest(5)))).Code.Should().Be("role_not_found");
    }

    [Fact]
    public async Task ChangeRole_RoleBelongsToDifferentOrg_Throws()
    {
        _members.Setup(r => r.GetAsync(Target, Org, It.IsAny<CancellationToken>())).ReturnsAsync(Member(Target, 3));
        _roles.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(new OrganizationRole { Id = 5, OrganizationId = 999 });
        (await Assert.ThrowsAsync<AppException>(() => _sut.ChangeMemberRoleAsync(Org, Target, Caller, new ChangeOrgMemberRoleRequest(5)))).Code.Should().Be("role_not_in_organization");
    }

    [Fact]
    public async Task ChangeRole_ValidRole_UpdatesTargetMembership()
    {
        var target = Member(Target, 3);
        _members.Setup(r => r.GetAsync(Target, Org, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        _roles.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(new OrganizationRole { Id = 5, OrganizationId = Org });

        await _sut.ChangeMemberRoleAsync(Org, Target, Caller, new ChangeOrgMemberRoleRequest(5));

        target.OrgRoleId.Should().Be(5);
        _members.Verify(r => r.UpdateAsync(target, It.IsAny<CancellationToken>()), Times.Once);
    }
}
