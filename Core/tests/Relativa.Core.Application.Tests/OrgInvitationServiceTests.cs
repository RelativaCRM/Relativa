using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Core.Application.DTOs.OrgInvitation;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class OrgInvitationServiceTests
{
    private readonly Mock<IOrgInvitationRepository> _invitationRepo = new();
    private readonly Mock<IUserRoleOrganizationRepository> _orgMemberRepo = new();
    private readonly Mock<IOrganizationRoleRepository> _orgRoleRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IValidator<InviteToOrgRequest>> _inviteValidator = new();
    private readonly OrgInvitationService _sut;

    public OrgInvitationServiceTests()
    {
        _inviteValidator
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<InviteToOrgRequest>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _sut = new OrgInvitationService(
            _invitationRepo.Object,
            _orgMemberRepo.Object,
            _orgRoleRepo.Object,
            _userRepo.Object,
            _inviteValidator.Object);
    }

    private static UserRoleOrganization OrgMemberWith(int userId, int orgId, params string[] permissions) =>
        new()
        {
            UserId = userId,
            OrganizationId = orgId,
            OrgRoleId = 10,
            Role = new OrganizationRole
            {
                Id = 10,
                Name = "org_admin",
                RolePermissions = permissions
                    .Select(p => new OrganizationRolePermission { Permission = new Permission { Name = p } })
                    .ToList()
            }
        };

    [Fact]
    public async Task InviteAsync_DefaultRole_AssignsSystemOrgMemberRole()
    {
        var caller = OrgMemberWith(1, 5, "invite_to_org");
        var memberRole = new OrganizationRole { Id = 20, Name = "org_member", OrganizationId = null };

        _orgMemberRepo
            .Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _orgRoleRepo
            .Setup(r => r.GetSystemRoleByNameAsync("org_member", It.IsAny<CancellationToken>()))
            .ReturnsAsync(memberRole);

        OrganizationInvitation? captured = null;
        _invitationRepo
            .Setup(r => r.AddAsync(It.IsAny<OrganizationInvitation>(), It.IsAny<CancellationToken>()))
            .Callback<OrganizationInvitation, CancellationToken>((i, _) => captured = i);

        var dto = await _sut.InviteAsync(5, 1, new InviteToOrgRequest("newuser@relativa.io"));

        dto.RoleName.Should().Be("org_member");
        captured!.OrgRoleId.Should().Be(20);
        captured.Status.Should().Be("Pending");
        captured.Email.Should().Be("newuser@relativa.io");
    }

    [Fact]
    public async Task InviteAsync_NonDefaultRole_WithoutAssignOrgRolesPermission_ThrowsUnauthorized()
    {
        var caller = OrgMemberWith(1, 5, "invite_to_org"); // missing assign_org_roles
        var adminRole = new OrganizationRole { Id = 30, Name = "org_admin", OrganizationId = null };

        _orgMemberRepo
            .Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _orgRoleRepo
            .Setup(r => r.GetByIdAsync(30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminRole);

        var act = () => _sut.InviteAsync(5, 1, new InviteToOrgRequest("x@relativa.io", 30));

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*assign_org_roles*");
        _invitationRepo.Verify(r => r.AddAsync(It.IsAny<OrganizationInvitation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InviteAsync_NonDefaultRole_WithAssignOrgRolesPermission_Succeeds()
    {
        var caller = OrgMemberWith(1, 5, "invite_to_org", "assign_org_roles");
        var adminRole = new OrganizationRole { Id = 30, Name = "org_admin", OrganizationId = null };

        _orgMemberRepo
            .Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _orgRoleRepo
            .Setup(r => r.GetByIdAsync(30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminRole);

        OrganizationInvitation? captured = null;
        _invitationRepo
            .Setup(r => r.AddAsync(It.IsAny<OrganizationInvitation>(), It.IsAny<CancellationToken>()))
            .Callback<OrganizationInvitation, CancellationToken>((i, _) => captured = i);

        var dto = await _sut.InviteAsync(5, 1, new InviteToOrgRequest("admin@relativa.io", 30));

        dto.RoleName.Should().Be("org_admin");
        captured!.OrgRoleId.Should().Be(30);
    }

    [Fact]
    public async Task InviteAsync_RoleBelongsToAnotherOrganization_ThrowsArgumentException()
    {
        var caller = OrgMemberWith(1, 5, "invite_to_org", "assign_org_roles");
        var otherOrgRole = new OrganizationRole { Id = 40, Name = "custom", OrganizationId = 99 };

        _orgMemberRepo
            .Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _orgRoleRepo
            .Setup(r => r.GetByIdAsync(40, It.IsAny<CancellationToken>()))
            .ReturnsAsync(otherOrgRole);

        var act = () => _sut.InviteAsync(5, 1, new InviteToOrgRequest("x@relativa.io", 40));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*does not belong to this organization*");
    }

    [Fact]
    public async Task InviteAsync_EmailAlreadyMember_ThrowsInvalidOperationException()
    {
        var caller = OrgMemberWith(1, 5, "invite_to_org");
        var memberRole = new OrganizationRole { Id = 20, Name = "org_member", OrganizationId = null };
        var invitee = new User { Id = 77, Email = "member@relativa.io" };

        _orgMemberRepo
            .Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _orgRoleRepo
            .Setup(r => r.GetSystemRoleByNameAsync("org_member", It.IsAny<CancellationToken>()))
            .ReturnsAsync(memberRole);
        _userRepo
            .Setup(r => r.GetByEmailAsync("member@relativa.io", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitee);
        _orgMemberRepo
            .Setup(r => r.GetAsync(77, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization { UserId = 77, OrganizationId = 5 });

        var act = () => _sut.InviteAsync(5, 1, new InviteToOrgRequest("member@relativa.io"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already a member*");
    }

    [Fact]
    public async Task InviteAsync_PendingInvitationForSameEmailExists_ThrowsInvalidOperationException()
    {
        var caller = OrgMemberWith(1, 5, "invite_to_org");
        var memberRole = new OrganizationRole { Id = 20, Name = "org_member", OrganizationId = null };

        _orgMemberRepo
            .Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _orgRoleRepo
            .Setup(r => r.GetSystemRoleByNameAsync("org_member", It.IsAny<CancellationToken>()))
            .ReturnsAsync(memberRole);
        _invitationRepo
            .Setup(r => r.GetPendingByOrgAndEmailAsync(5, "dup@relativa.io", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrganizationInvitation
            {
                Id = 100,
                OrganizationId = 5,
                Email = "dup@relativa.io",
                Status = "Pending",
                ExpiresAt = DateTime.UtcNow.AddDays(3)
            });

        var act = () => _sut.InviteAsync(5, 1, new InviteToOrgRequest("dup@relativa.io"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*pending invitation*");
    }

    [Fact]
    public async Task GetByOrganizationAsync_FiltersOutExpiredAndNonPending()
    {
        var caller = OrgMemberWith(1, 5, "invite_to_org");
        _orgMemberRepo
            .Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);

        var now = DateTime.UtcNow;
        _invitationRepo
            .Setup(r => r.GetByOrganizationIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new() {
                    Id = 1, Email = "a@r.io", Status = "Pending", ExpiresAt = now.AddDays(3),
                    Organization = new Organization { Name = "Acme" },
                    Role = new OrganizationRole { Name = "org_member" },
                },
                new() {
                    Id = 2, Email = "b@r.io", Status = "Pending", ExpiresAt = now.AddDays(-1),
                    Organization = new Organization { Name = "Acme" },
                    Role = new OrganizationRole { Name = "org_member" },
                },
                new() {
                    Id = 3, Email = "c@r.io", Status = "Accepted", ExpiresAt = now.AddDays(3),
                    Organization = new Organization { Name = "Acme" },
                    Role = new OrganizationRole { Name = "org_member" },
                }
            ]);

        var result = await _sut.GetByOrganizationAsync(5, 1);

        result.Should().ContainSingle().Which.Id.Should().Be(1);
    }

    [Fact]
    public async Task ResendAsync_PendingInvitation_RotatesTokenAndExtendsExpiry()
    {
        var caller = OrgMemberWith(1, 5, "invite_to_org");
        var existing = new OrganizationInvitation
        {
            Id = 42,
            OrganizationId = 5,
            Email = "pending@r.io",
            OrgRoleId = 20,
            Token = "old-token",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            Organization = new Organization { Name = "Acme" },
            Role = new OrganizationRole { Name = "org_member" }
        };

        _orgMemberRepo
            .Setup(r => r.GetAsync(1, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(caller);
        _invitationRepo
            .Setup(r => r.GetByIdAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var oldExpiry = existing.ExpiresAt;
        var dto = await _sut.ResendAsync(5, 42, 1);

        existing.Token.Should().NotBe("old-token");
        existing.ExpiresAt.Should().BeAfter(oldExpiry);
        dto.Token.Should().Be(existing.Token);
        _invitationRepo.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AcceptAsync_UsesRoleFromInvitation()
    {
        var invitation = new OrganizationInvitation
        {
            Id = 7,
            OrganizationId = 5,
            Email = "new@relativa.io",
            OrgRoleId = 30,
            Token = "token-1",
            Status = "Pending",
            ExpiresAt = DateTime.UtcNow.AddDays(5),
            Organization = new Organization { Name = "Acme" },
            Role = new OrganizationRole { Id = 30, Name = "org_admin" }
        };

        _invitationRepo
            .Setup(r => r.GetByTokenAsync("token-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        _orgMemberRepo
            .Setup(r => r.GetAsync(99, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleOrganization?)null);

        UserRoleOrganization? captured = null;
        _orgMemberRepo
            .Setup(r => r.AddAsync(It.IsAny<UserRoleOrganization>(), It.IsAny<CancellationToken>()))
            .Callback<UserRoleOrganization, CancellationToken>((m, _) => captured = m);

        await _sut.AcceptAsync(99, "new@relativa.io", "token-1");

        captured!.OrgRoleId.Should().Be(30);
        invitation.Status.Should().Be("Accepted");
    }

    [Fact]
    public async Task AcceptAsync_ExpiredInvitation_MarksExpiredAndThrows()
    {
        var invitation = new OrganizationInvitation
        {
            Id = 8,
            OrganizationId = 5,
            Email = "late@r.io",
            OrgRoleId = 20,
            Token = "late-token",
            Status = "Pending",
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            Organization = new Organization { Name = "Acme" },
            Role = new OrganizationRole { Name = "org_member" }
        };

        _invitationRepo
            .Setup(r => r.GetByTokenAsync("late-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var act = () => _sut.AcceptAsync(99, "late@r.io", "late-token");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*expired*");
        invitation.Status.Should().Be("Expired");
        _invitationRepo.Verify(r => r.UpdateAsync(invitation, It.IsAny<CancellationToken>()), Times.Once);
    }
}
