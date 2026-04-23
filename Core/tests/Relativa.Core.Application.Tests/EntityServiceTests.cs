using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Relativa.Core.Application.DTOs.Entity;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class EntityServiceTests
{
    private readonly Mock<IEntityRepository> _entityRepo = new();
    private readonly Mock<IUserRoleWorkspaceRepository> _memberRepo = new();
    private readonly Mock<IValidator<CreateEntityRequest>> _createValidator = new();
    private readonly Mock<IValidator<UpdateEntityRequest>> _updateValidator = new();
    private readonly EntityService _sut;

    public EntityServiceTests()
    {
        _sut = new EntityService(
            _entityRepo.Object,
            _memberRepo.Object,
            _createValidator.Object,
            _updateValidator.Object);
    }

    private static UserRoleWorkspace MemberWithPermission(int userId, int workspaceId, string permission) =>
        new()
        {
            UserId = userId,
            WorkspaceId = workspaceId,
            Role = new WorkspaceRole
            {
                Name = "test",
                RolePermissions =
                [
                    new WorkspaceRolePermission { Permission = new Permission { Name = permission } }
                ]
            }
        };

    [Fact]
    public async Task CreateAsync_UserLacksPermission_InvalidRequest_ThrowsUnauthorized_DoesNotValidate()
    {
        var request = new CreateEntityRequest(0, []);
        _memberRepo
            .Setup(r => r.GetAsync(5, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MemberWithPermission(5, 2, "view_entities"));

        var act = () => _sut.CreateAsync(2, 5, request);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You do not have the 'manage_entities' permission in this workspace.");
        _createValidator.Verify(
            v => v.ValidateAsync(It.IsAny<CreateEntityRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_UnknownEntityType_ThrowsKeyNotFoundException()
    {
        const int entityTypeId = 99;
        var request = new CreateEntityRequest(entityTypeId, []);
        _memberRepo
            .Setup(r => r.GetAsync(1, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MemberWithPermission(1, 3, "manage_entities"));
        _createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<CreateEntityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _entityRepo
            .Setup(r => r.GetTypePropertiesAsync(entityTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var act = () => _sut.CreateAsync(3, 1, request);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"Entity type {entityTypeId} does not exist or has no properties defined.");
    }
}
