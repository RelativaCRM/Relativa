using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Relativa.Core.Application.DTOs.WsJoinRequest;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class WsJoinRequestServiceTests
{
    private readonly Mock<IWsJoinRequestRepository> _joinRepo = new();
    private readonly Mock<IUserRoleWorkspaceRepository> _wsMemberRepo = new();
    private readonly Mock<IUserRoleOrganizationRepository> _orgMemberRepo = new();
    private readonly Mock<IWorkspaceRoleRepository> _wsRoleRepo = new();
    private readonly Mock<IWorkspaceRepository> _workspaceRepo = new();
    private readonly Mock<IValidator<CreateWsJoinRequestRequest>> _createValidator = new();
    private readonly Mock<IValidator<ReviewWsJoinRequestRequest>> _reviewValidator = new();
    private readonly WsJoinRequestService _sut;

    public WsJoinRequestServiceTests()
    {
        _createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateWsJoinRequestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _reviewValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<ReviewWsJoinRequestRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _sut = new WsJoinRequestService(
            _joinRepo.Object,
            _wsMemberRepo.Object,
            _orgMemberRepo.Object,
            _wsRoleRepo.Object,
            _workspaceRepo.Object,
            _createValidator.Object,
            _reviewValidator.Object);
    }

    [Fact]
    public async Task SubmitAsync_RequesterNotOrgMember_ThrowsUnauthorized()
    {
        var workspace = new Workspace { Id = 50, OrganizationId = 7, Name = "Ops" };
        _workspaceRepo.Setup(r => r.GetByIdAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);
        _orgMemberRepo.Setup(r => r.GetAsync(42, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleOrganization?)null);

        var act = () => _sut.SubmitAsync(50, 42, new CreateWsJoinRequestRequest("Hi"));

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*member of this workspace's organization*");
        _joinRepo.Verify(r => r.AddAsync(It.IsAny<WorkspaceJoinRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitAsync_AlreadyMember_ThrowsInvalidOperationException()
    {
        var workspace = new Workspace { Id = 50, OrganizationId = 7, Name = "Ops" };
        _workspaceRepo.Setup(r => r.GetByIdAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);
        _orgMemberRepo.Setup(r => r.GetAsync(42, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization { UserId = 42, OrganizationId = 7 });
        _wsMemberRepo.Setup(r => r.GetAsync(42, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleWorkspace { UserId = 42, WorkspaceId = 50 });

        var act = () => _sut.SubmitAsync(50, 42, new CreateWsJoinRequestRequest(null));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already a member*");
    }

    [Fact]
    public async Task SubmitAsync_DuplicatePendingRequest_ThrowsInvalidOperationException()
    {
        var workspace = new Workspace { Id = 50, OrganizationId = 7, Name = "Ops" };
        _workspaceRepo.Setup(r => r.GetByIdAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);
        _orgMemberRepo.Setup(r => r.GetAsync(42, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization { UserId = 42, OrganizationId = 7 });
        _joinRepo.Setup(r => r.GetPendingAsync(42, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceJoinRequest { Id = 1, UserId = 42, WorkspaceId = 50, Status = "Pending" });

        var act = () => _sut.SubmitAsync(50, 42, new CreateWsJoinRequestRequest(null));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*pending join request*");
    }

    [Fact]
    public async Task SubmitAsync_ValidRequest_CreatesPendingRequest()
    {
        var workspace = new Workspace { Id = 50, OrganizationId = 7, Name = "Ops" };
        _workspaceRepo.Setup(r => r.GetByIdAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);
        _orgMemberRepo.Setup(r => r.GetAsync(42, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization { UserId = 42, OrganizationId = 7 });

        WorkspaceJoinRequest? captured = null;
        _joinRepo
            .Setup(r => r.AddAsync(It.IsAny<WorkspaceJoinRequest>(), It.IsAny<CancellationToken>()))
            .Callback<WorkspaceJoinRequest, CancellationToken>((r, _) => captured = r);

        var dto = await _sut.SubmitAsync(50, 42, new CreateWsJoinRequestRequest("Please let me in"));

        dto.Status.Should().Be("Pending");
        captured!.WorkspaceId.Should().Be(50);
        captured.UserId.Should().Be(42);
        captured.Message.Should().Be("Please let me in");
    }

    [Fact]
    public async Task GetByWorkspaceAsync_WithoutPermission_ThrowsUnauthorized()
    {
        _wsMemberRepo.Setup(r => r.GetAsync(1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleWorkspace
            {
                UserId = 1,
                WorkspaceId = 50,
                Role = new WorkspaceRole { Name = "ws_member", RolePermissions = [] }
            });

        var act = () => _sut.GetByWorkspaceAsync(50, 1);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*manage_ws_join_requests*");
    }

    [Fact]
    public async Task ReviewAsync_ApprovedAddsWsMemberAndMarksAccepted()
    {
        var admin = new UserRoleWorkspace
        {
            UserId = 9,
            WorkspaceId = 50,
            Role = new WorkspaceRole
            {
                Name = "ws_admin",
                RolePermissions = [
                    new WorkspaceRolePermission { Permission = new Permission { Name = "manage_ws_join_requests" } }
                ]
            }
        };
        var workspace = new Workspace { Id = 50, OrganizationId = 7, Name = "Ops" };
        var joinRequest = new WorkspaceJoinRequest
        {
            Id = 1,
            UserId = 42,
            WorkspaceId = 50,
            Status = "Pending",
            Workspace = workspace,
        };
        var memberRole = new WorkspaceRole { Id = 3, Name = "ws_member" };

        _wsMemberRepo.Setup(r => r.GetAsync(9, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(admin);
        _joinRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(joinRequest);
        _orgMemberRepo.Setup(r => r.GetAsync(42, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleOrganization { UserId = 42, OrganizationId = 7 });
        _wsRoleRepo.Setup(r => r.GetSystemRoleByNameAsync("ws_member", It.IsAny<CancellationToken>()))
            .ReturnsAsync(memberRole);

        await _sut.ReviewAsync(50, 1, 9, new ReviewWsJoinRequestRequest("Approved"));

        joinRequest.Status.Should().Be("Approved");
        joinRequest.ReviewedByUserId.Should().Be(9);
        _wsMemberRepo.Verify(r => r.AddAsync(It.IsAny<UserRoleWorkspace>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewAsync_ApprovedButRequesterLostOrgMembership_RejectsAutomatically()
    {
        var admin = new UserRoleWorkspace
        {
            UserId = 9,
            WorkspaceId = 50,
            Role = new WorkspaceRole
            {
                Name = "ws_admin",
                RolePermissions = [
                    new WorkspaceRolePermission { Permission = new Permission { Name = "manage_ws_join_requests" } }
                ]
            }
        };
        var workspace = new Workspace { Id = 50, OrganizationId = 7, Name = "Ops" };
        var joinRequest = new WorkspaceJoinRequest
        {
            Id = 1,
            UserId = 42,
            WorkspaceId = 50,
            Status = "Pending",
            Workspace = workspace,
        };

        _wsMemberRepo.Setup(r => r.GetAsync(9, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(admin);
        _joinRepo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(joinRequest);
        _orgMemberRepo.Setup(r => r.GetAsync(42, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleOrganization?)null);

        var act = () => _sut.ReviewAsync(50, 1, 9, new ReviewWsJoinRequestRequest("Approved"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no longer a member*");
        joinRequest.Status.Should().Be("Rejected");
        _wsMemberRepo.Verify(r => r.AddAsync(It.IsAny<UserRoleWorkspace>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
