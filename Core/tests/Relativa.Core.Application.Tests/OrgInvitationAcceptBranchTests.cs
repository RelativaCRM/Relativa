using FluentAssertions;
using FluentValidation;
using Moq;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Core.Application.DTOs.OrgInvitation;
using Relativa.Core.Application.Exceptions;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class OrgInvitationAcceptBranchTests
{
    private readonly Mock<IOrgInvitationRepository> _invitationRepo = new();
    private readonly Mock<IUserRoleOrganizationRepository> _orgMemberRepo = new();
    private readonly Mock<IOrganizationRoleRepository> _orgRoleRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IValidator<InviteToOrgRequest>> _validator = new();
    private readonly Mock<IOutboxWriter> _audit = new();
    private readonly OrgInvitationService _sut;

    private const int UserId = 7, Org = 1, RoleId = 5;
    private const string Email = "user@test.com";
    private const string Token = "tok-123";

    public OrgInvitationAcceptBranchTests()
    {
        _audit.Setup(w => w.EnqueueAuditAsync(It.IsAny<AuditEventContract>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _invitationRepo.Setup(r => r.GetByTokenAsync(Token, It.IsAny<CancellationToken>())).ReturnsAsync(Pending());
        _orgMemberRepo.Setup(r => r.GetAsync(UserId, Org, It.IsAny<CancellationToken>())).ReturnsAsync((UserRoleOrganization?)null);
        _sut = new OrgInvitationService(_invitationRepo.Object, _orgMemberRepo.Object, _orgRoleRepo.Object, _userRepo.Object, _validator.Object, _audit.Object);
    }

    private static OrganizationInvitation Pending() =>
        new() { Id = 1, Email = Email, Status = "Pending", ExpiresAt = DateTime.UtcNow.AddDays(1), OrganizationId = Org, OrgRoleId = RoleId };

    private void InvitationIs(OrganizationInvitation inv) =>
        _invitationRepo.Setup(r => r.GetByTokenAsync(Token, It.IsAny<CancellationToken>())).ReturnsAsync(inv);

    private async Task<string> AcceptCode(string email = Email) =>
        (await Assert.ThrowsAsync<AppException>(() => _sut.AcceptAsync(UserId, email, Token))).Code;

    [Fact]
    public async Task Accept_TokenNotFound_Throws()
    {
        _invitationRepo.Setup(r => r.GetByTokenAsync(Token, It.IsAny<CancellationToken>())).ReturnsAsync((OrganizationInvitation?)null);
        (await AcceptCode()).Should().Be("invitation_not_found_or_expired");
    }

    [Fact]
    public async Task Accept_EmailMismatch_Throws() => (await AcceptCode("other@test.com")).Should().Be("invitation_email_mismatch");

    [Fact]
    public async Task Accept_NotPending_Throws()
    {
        InvitationIs(new OrganizationInvitation { Id = 1, Email = Email, Status = "Accepted", ExpiresAt = DateTime.UtcNow.AddDays(1), OrganizationId = Org, OrgRoleId = RoleId });
        (await AcceptCode()).Should().Be("invitation_not_pending");
    }

    [Fact]
    public async Task Accept_Expired_MarksExpiredAndThrows()
    {
        InvitationIs(new OrganizationInvitation { Id = 1, Email = Email, Status = "Pending", ExpiresAt = DateTime.UtcNow.AddDays(-1), OrganizationId = Org, OrgRoleId = RoleId });

        (await AcceptCode()).Should().Be("invitation_expired");
        _invitationRepo.Verify(r => r.UpdateAsync(It.Is<OrganizationInvitation>(i => i.Status == "Expired"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Accept_AlreadyMember_Throws()
    {
        _orgMemberRepo.Setup(r => r.GetAsync(UserId, Org, It.IsAny<CancellationToken>())).ReturnsAsync(new UserRoleOrganization { UserId = UserId, OrganizationId = Org });
        (await AcceptCode()).Should().Be("already_org_member");
    }

    [Fact]
    public async Task Accept_Valid_AddsMembershipWithInvitedRoleAndMarksAccepted()
    {
        await _sut.AcceptAsync(UserId, Email, Token);

        _orgMemberRepo.Verify(r => r.AddAsync(It.Is<UserRoleOrganization>(m => m.UserId == UserId && m.OrgRoleId == RoleId), It.IsAny<CancellationToken>()), Times.Once);
        _invitationRepo.Verify(r => r.UpdateAsync(It.Is<OrganizationInvitation>(i => i.Status == "Accepted"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Decline_EmailMismatch_Throws() =>
        (await Assert.ThrowsAsync<AppException>(() => _sut.DeclineAsync(UserId, "other@test.com", Token))).Code.Should().Be("invitation_email_mismatch");

    [Fact]
    public async Task Decline_NotPending_Throws()
    {
        InvitationIs(new OrganizationInvitation { Id = 1, Email = Email, Status = "Declined", ExpiresAt = DateTime.UtcNow.AddDays(1), OrganizationId = Org, OrgRoleId = RoleId });
        (await Assert.ThrowsAsync<AppException>(() => _sut.DeclineAsync(UserId, Email, Token))).Code.Should().Be("invitation_not_pending");
    }

    [Fact]
    public async Task Decline_Valid_MarksDeclinedWithoutAddingMember()
    {
        await _sut.DeclineAsync(UserId, Email, Token);

        _orgMemberRepo.Verify(r => r.AddAsync(It.IsAny<UserRoleOrganization>(), It.IsAny<CancellationToken>()), Times.Never);
        _invitationRepo.Verify(r => r.UpdateAsync(It.Is<OrganizationInvitation>(i => i.Status == "Declined"), It.IsAny<CancellationToken>()), Times.Once);
    }
}
