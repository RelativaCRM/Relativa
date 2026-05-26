using Relativa.Core.Application.Exceptions;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Relativa.Core.Application.DTOs.Entity;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class EntityServiceTests
{
    private readonly Mock<IEntityRepository> _entityRepo = new();
    private readonly Mock<IWorkspaceAccessEvaluator> _workspaceAccess = new();
    private readonly Mock<IUserRoleWorkspaceRepository> _memberRepo = new();
    private readonly Mock<IOutboxWriter> _auditOutboxWriter = new();
    private readonly Mock<IValidator<CreateEntityRequest>> _createValidator = new();
    private readonly Mock<IValidator<UpdateEntityRequest>> _updateValidator = new();
    private readonly EntityService _sut;

    public EntityServiceTests()
    {
        DefaultWorkspaceAccessMocks();
        _sut = new EntityService(
            _entityRepo.Object,
            _workspaceAccess.Object,
            _memberRepo.Object,
            _createValidator.Object,
            _updateValidator.Object,
            _auditOutboxWriter.Object);

        _entityRepo
            .Setup(r => r.GetEntityTypeByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) =>
                new EntityType { Id = id, Name = "test_type", IsStandalone = true });
        _entityRepo
            .Setup(r => r.GetOutgoingRelationshipTypesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _entityRepo
            .Setup(r => r.ArchiveAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _entityRepo
            .Setup(r => r.GetTypePropertiesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateEntityRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<CreateEntityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _updateValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateEntityRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _updateValidator
            .Setup(v => v.ValidateAsync(It.IsAny<UpdateEntityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
    }

    private void DefaultWorkspaceAccessMocks()
    {
        _workspaceAccess.Reset();
        _memberRepo.Reset();
        _workspaceAccess.Setup(x => x.EnsureCanAccessWorkspaceAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _workspaceAccess.Setup(x =>
                x.HasWorkspacePermissionAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _memberRepo.Setup(x => x.GetAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int userId, int workspaceId, CancellationToken _) => new UserRoleWorkspace
            {
                UserId = userId,
                WorkspaceId = workspaceId,
                Role = new WorkspaceRole { Priority = 4 }
            });
        _memberRepo.Setup(x => x.GetRolePrioritiesByUserIdsAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, IReadOnlyCollection<int> userIds, CancellationToken _) =>
            {
                var ids = userIds.ToList();
                if (ids.Count == 0)
                    return new Dictionary<int, int>();

                var result = new Dictionary<int, int> { [ids[0]] = 2 };
                for (var i = 1; i < ids.Count; i++)
                    result[ids[i]] = 6;
                return result;
            });
    }

    private static Property Prop(int id, string name, PropertyDataType type = PropertyDataType.String) =>
        new() { Id = id, Name = name, DataType = type };

    private static EntityTypeProperty TypeProp(int propertyId, string name, bool required = false,
        PropertyDataType type = PropertyDataType.String) =>
        new() { PropertyId = propertyId, IsRequired = required, Property = Prop(propertyId, name, type) };

    private static Entity BuildEntity(int id, int typeId, string typeName,
        IEnumerable<(int propId, string name, string value)> values, bool archived = false, int createdByUserId = 2) =>
        new()
        {
            Id = id,
            EntityTypeId = typeId,
            CreatedByUserId = createdByUserId,
            IsArchived = archived,
            EntityType = new EntityType { Id = typeId, Name = typeName },
            EntityPropertyValues = values.Select(v => new EntityPropertyValue
            {
                EntityId = id,
                PropertyId = v.propId,
                ValueString = v.value,
                Property = Prop(v.propId, v.name)
            }).ToList()
        };

    [Fact]
    public async Task GetByWorkspaceAsync_UserLacksViewPermission_ThrowsUnauthorized()
    {
        _workspaceAccess
            .Setup(x => x.HasWorkspacePermissionAsync(1, 1, "view_entities", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.Invoking(s => s.GetByWorkspaceAsync(1, 1, null, null, 0, 50))
            .Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*view_entities*");
    }

    [Fact]
    public async Task GetByWorkspaceAsync_ActiveEntity_MapsToDto()
    {
        var active = BuildEntity(1, 1, "client", [(1, "first_name", "Ivan")]);
        _entityRepo.Setup(r => r.GetByWorkspaceAsync(
                1, 1, It.IsAny<int>(), null, null,
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<IReadOnlyList<ResolvedFilterCondition>>(),
                It.IsAny<IReadOnlyList<EntitySortField>>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(([active], 1));

        var result = await _sut.GetByWorkspaceAsync(1, 1, null, null, 0, 50);

        result.Items.Should().HaveCount(1);
        result.Items[0].Id.Should().Be(1);
        result.Items[0].EntityTypeName.Should().Be("client");
        result.Total.Should().Be(1);
    }

    [Fact]
    public async Task GetByIdAsync_EntityFoundInWorkspace_ReturnsAllFields()
    {
        var entity = BuildEntity(5, 1, "client",
        [
            (1, "first_name", "Olena"),
            (3, "last_name", "Koval")
        ]);
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(5, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        _memberRepo
            .Setup(x => x.GetRolePrioritiesByUserIdsAsync(1, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int> { [1] = 4, [entity.CreatedByUserId] = 6 });

        var result = await _sut.GetByIdAsync(5, 1, 1);

        result.Id.Should().Be(5);
        result.EntityTypeName.Should().Be("client");
        result.PropertyValues.Should().HaveCount(2);
        result.PropertyValues.Should().Contain(p => p.PropertyName == "first_name" && (string?)p.Value == "Olena");
        result.PropertyValues.Should().Contain(p => p.PropertyName == "last_name" && (string?)p.Value == "Koval");
    }

    [Fact]
    public async Task GetByIdAsync_EqualPriorityNonOwner_ThrowsAccessDenied()
    {
        var entity = BuildEntity(5, 1, "client", [(1, "first_name", "Olena")], createdByUserId: 2);
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(5, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        _memberRepo
            .Setup(x => x.GetRolePrioritiesByUserIdsAsync(1, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int> { [1] = 4, [2] = 4 });

        await _sut.Invoking(s => s.GetByIdAsync(5, 1, 1))
            .Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("Access denied");
    }

    [Fact]
    public async Task GetByIdAsync_HigherPriorityNonOwner_CanAccess()
    {
        var entity = BuildEntity(5, 1, "client", [(1, "first_name", "Olena")], createdByUserId: 2);
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(5, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        _memberRepo
            .Setup(x => x.GetRolePrioritiesByUserIdsAsync(1, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int> { [1] = 2, [2] = 6 });

        var result = await _sut.GetByIdAsync(5, 1, 1);
        result.Id.Should().Be(5);
    }

    [Fact]
    public async Task GetByIdAsync_EntityNotInWorkspace_ThrowsEntityNotFoundException()
    {
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(99, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Entity?)null);

        await _sut.Invoking(s => s.GetByIdAsync(99, 1, 1))
            .Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*99*");
    }

    [Fact]
    public async Task GetByIdAsync_UserNotMemberOfWorkspace_ThrowsUnauthorized()
    {
        _workspaceAccess.Setup(x =>
                x.HasWorkspacePermissionAsync(2, 1, "view_entities", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ForbiddenAccessException("You are not a member of this workspace."));

        await _sut.Invoking(s => s.GetByIdAsync(5, 1, 2))
            .Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*not a member*");
    }


    [Fact]
    public async Task CreateAsync_UserLacksPermission_InvalidRequest_ThrowsUnauthorized_DoesNotValidate()
    {
        _workspaceAccess
            .Setup(x => x.HasWorkspacePermissionAsync(5, 2, "create_entities", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.Invoking(s => s.CreateAsync(2, 5, new CreateEntityRequest(0, [])))
            .Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*create_entities*");

        _createValidator.Verify(
            v => v.ValidateAsync(It.IsAny<CreateEntityRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_UnknownEntityType_ThrowsKeyNotFoundException()
    {
        _entityRepo.Setup(r => r.GetTypePropertiesAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _sut.Invoking(s => s.CreateAsync(3, 1, new CreateEntityRequest(99, [])))
            .Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*99*");
    }

    [Fact]
    public async Task CreateAsync_ClientEntity_CallsRepoWithWorkspaceId()
    {
        var typeProps = new List<EntityTypeProperty>
        {
            TypeProp(1, "first_name", required: true),
            TypeProp(3, "last_name",  required: true)
        };
        var created = BuildEntity(10, 1, "client", [(1, "first_name", "Ivan"), (3, "last_name", "Shevchenko")]);

        _entityRepo.Setup(r => r.GetTypePropertiesAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typeProps);
        _entityRepo.Setup(r => r.CreateAsync(It.IsAny<Entity>(), It.IsAny<List<EntityPropertyValue>>(), 1, It.IsAny<IReadOnlyList<EntityRelationship>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(10, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var request = new CreateEntityRequest(1,
        [
            new PropertyValueInput(1, "Ivan"),
            new PropertyValueInput(3, "Shevchenko")
        ]);
        await _sut.CreateAsync(1, 1, request);

        _entityRepo.Verify(r =>
            r.CreateAsync(It.IsAny<Entity>(), It.IsAny<List<EntityPropertyValue>>(), 1, It.IsAny<IReadOnlyList<EntityRelationship>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _entityRepo.Verify(r =>
            r.CreateAsync(
                It.Is<Entity>(e => e.CreatedByUserId == 1),
                It.IsAny<List<EntityPropertyValue>>(),
                1,
                It.IsAny<IReadOnlyList<EntityRelationship>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _auditOutboxWriter.Verify(x => x.EnqueueAuditAsync(It.IsAny<Relativa.Persistence.Contracts.AuditEventContract>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_DealEntity_DecimalAndDateProperties_BuildsCorrectTypedValues()
    {
        var typeProps = new List<EntityTypeProperty>
        {
            TypeProp(9,  "deal_value",     required: false, type: PropertyDataType.Decimal),
            TypeProp(10, "expected_close", required: false, type: PropertyDataType.Date)
        };
        Entity? capturedEntity = null;
        List<EntityPropertyValue>? capturedValues = null;
        var created = new Entity
        {
            Id = 20, EntityTypeId = 2, IsArchived = false,
            EntityType = new EntityType { Id = 2, Name = "deal" },
            EntityPropertyValues = []
        };

        _entityRepo.Setup(r => r.GetTypePropertiesAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typeProps);
        _entityRepo.Setup(r => r.CreateAsync(It.IsAny<Entity>(), It.IsAny<List<EntityPropertyValue>>(), 1, It.IsAny<IReadOnlyList<EntityRelationship>?>(), It.IsAny<CancellationToken>()))
            .Callback<Entity, List<EntityPropertyValue>, int, IReadOnlyList<EntityRelationship>?, CancellationToken>((e, pvs, _, _, _) =>
            {
                capturedEntity = e;
                capturedValues = pvs;
            })
            .ReturnsAsync(created);
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(20, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var request = new CreateEntityRequest(2,
        [
            new PropertyValueInput(9,  "15000.50"),
            new PropertyValueInput(10, "2026-12-31")
        ]);
        await _sut.CreateAsync(1, 1, request);

        capturedValues.Should().NotBeNull();
        capturedValues!.Should().HaveCount(2);
        capturedValues.Single(v => v.PropertyId == 9).ValueDecimal.Should().Be(15000.50m);
        capturedValues.Single(v => v.PropertyId == 10).ValueDate.Should().Be(new DateOnly(2026, 12, 31));
    }

    [Fact]
    public async Task CreateAsync_MissingRequiredProperty_ThrowsArgumentException()
    {
        var typeProps = new List<EntityTypeProperty>
        {
            TypeProp(1, "first_name", required: true),
            TypeProp(3, "last_name",  required: true)
        };
        _entityRepo.Setup(r => r.GetTypePropertiesAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typeProps);

        var request = new CreateEntityRequest(1, [new PropertyValueInput(1, "Ivan")]);

        await _sut.Invoking(s => s.CreateAsync(1, 1, request))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*last_name*");
    }

    [Fact]
    public async Task CreateAsync_UnknownPropertyId_ThrowsArgumentException()
    {
        var typeProps = new List<EntityTypeProperty>
        {
            TypeProp(1, "first_name", required: true),
            TypeProp(3, "last_name",  required: true)
        };
        _entityRepo.Setup(r => r.GetTypePropertiesAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typeProps);

        var request = new CreateEntityRequest(1,
        [
            new PropertyValueInput(1,  "Ivan"),
            new PropertyValueInput(3,  "Koval"),
            new PropertyValueInput(99, "unknown")
        ]);

        await _sut.Invoking(s => s.CreateAsync(1, 1, request))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*99*");
    }

    [Fact]
    public async Task CreateAsync_DuplicatePropertyIds_ThrowsArgumentException()
    {
        var typeProps = new List<EntityTypeProperty>
        {
            TypeProp(1, "first_name", required: true),
            TypeProp(3, "last_name",  required: true)
        };
        _entityRepo.Setup(r => r.GetTypePropertiesAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typeProps);

        var request = new CreateEntityRequest(1,
        [
            new PropertyValueInput(1, "Ivan"),
            new PropertyValueInput(1, "Duplicate"),
            new PropertyValueInput(3, "Koval")
        ]);

        await _sut.Invoking(s => s.CreateAsync(1, 1, request))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Duplicate*");
    }

    [Fact]
    public async Task CreateAsync_DecimalPropertyValueOverflowsDecimalMaxValue_ThrowsArgumentException()
    {
        var typeProps = new List<EntityTypeProperty>
        {
            TypeProp(9, "closure_score", required: false, type: PropertyDataType.Decimal)
        };
        _entityRepo.Setup(r => r.GetTypePropertiesAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typeProps);

        var request = new CreateEntityRequest(2,
        [
            new PropertyValueInput(9, "44444444444444450000000000000000000000000000000000000000")
        ]);

        await _sut.Invoking(s => s.CreateAsync(1, 1, request))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*closure_score*decimal*");
    }

    [Fact]
    public async Task UpdateAsync_UserLacksPermission_InvalidPayload_ThrowsUnauthorized_NotValidation()
    {
        _workspaceAccess
            .Setup(x => x.HasWorkspacePermissionAsync(1, 1, "edit_entities", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.Invoking(s => s.UpdateAsync(1, 1, 1, new UpdateEntityRequest(null!)))
            .Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*edit_entities*");

        _updateValidator.Verify(
            v => v.ValidateAsync(It.IsAny<UpdateEntityRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_OnlySendsChangedFields_MergesExistingRequiredValues()
    {
        var storedEntity = BuildEntity(1, 1, "client",
        [
            (1, "first_name", "Oleksiy"),
            (3, "last_name",  "Ivanenko")
        ]);
        var typeProps = new List<EntityTypeProperty>
        {
            TypeProp(1, "first_name", required: true),
            TypeProp(3, "last_name",  required: true)
        };
        var reloaded = BuildEntity(1, 1, "client",
        [
            (1, "first_name", "Jane"),
            (3, "last_name",  "Ivanenko")
        ]);

        _entityRepo.SetupSequence(r => r.GetByIdInWorkspaceAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedEntity)
            .ReturnsAsync(reloaded);
        _entityRepo.Setup(r => r.GetTypePropertiesAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typeProps);

        await _sut.UpdateAsync(1, 1, 1, new UpdateEntityRequest([new PropertyValueInput(1, "Jane")]));

        _entityRepo.Verify(r => r.UpdateAsync(
            storedEntity,
            It.Is<List<EntityPropertyValue>>(l =>
                l.Count == 2
                && l.Single(x => x.PropertyId == 1).ValueString == "Jane"
                && l.Single(x => x.PropertyId == 3).ValueString == "Ivanenko"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_EntityNotFound_ThrowsKeyNotFound()
    {
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(99, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Entity?)null);

        await _sut.Invoking(s => s.UpdateAsync(99, 1, 1, new UpdateEntityRequest([])))
            .Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*99*");
    }

    [Fact]
    public async Task UpdateAsync_ArchivedEntityWithoutSpecialPermission_ThrowsUnauthorized()
    {
        var archived = BuildEntity(1, 1, "client", [(1, "first_name", "Old")], archived: true);
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(archived);
        _workspaceAccess
            .Setup(x => x.HasWorkspacePermissionAsync(1, 1, "edit_archived_entities", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.Invoking(s => s.UpdateAsync(1, 1, 1, new UpdateEntityRequest([new PropertyValueInput(1, "New")])))
            .Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*edit_archived_entities*");
    }

    [Fact]
    public async Task UpdateAsync_ArchivedEntityWithSpecialPermission_UpdatesWithoutUnarchiving()
    {
        var archived = BuildEntity(1, 1, "client", [(1, "first_name", "Old")], archived: true);
        var typeProps = new List<EntityTypeProperty> { TypeProp(1, "first_name", required: true) };
        var reloaded = BuildEntity(1, 1, "client", [(1, "first_name", "New")], archived: true);

        _entityRepo.SetupSequence(r => r.GetByIdInWorkspaceAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(archived)
            .ReturnsAsync(reloaded);
        _entityRepo.Setup(r => r.GetTypePropertiesAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typeProps);

        await _sut.UpdateAsync(1, 1, 1, new UpdateEntityRequest([new PropertyValueInput(1, "New")]));

        _entityRepo.Verify(r => r.SetArchivedStateAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        _entityRepo.Verify(r => r.UpdateAsync(archived, It.IsAny<List<EntityPropertyValue>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_UnknownPropertyId_ThrowsArgumentException()
    {
        var entity = BuildEntity(1, 1, "client", [(1, "first_name", "Ivan")]);
        var typeProps = new List<EntityTypeProperty> { TypeProp(1, "first_name", required: true) };

        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        _entityRepo.Setup(r => r.GetTypePropertiesAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typeProps);

        await _sut.Invoking(s => s.UpdateAsync(1, 1, 1, new UpdateEntityRequest([new PropertyValueInput(99, "bad")])))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*99*");
    }


    [Fact]
    public async Task ArchiveAsync_EntityNotFound_ThrowsKeyNotFound()
    {
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(99, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Entity?)null);

        await _sut.Invoking(s => s.ArchiveAsync(99, 1, 1))
            .Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*99*");
    }

    [Fact]
    public async Task ArchiveAsync_UserLacksPermission_ThrowsUnauthorized()
    {
        _workspaceAccess
            .Setup(x => x.HasWorkspacePermissionAsync(1, 1, "delete_entities", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.Invoking(s => s.ArchiveAsync(1, 1, 1))
            .Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*delete_entities*");
    }

    [Fact]
    public async Task ArchiveAsync_ValidRequest_CallsRepositoryArchive()
    {
        var entity = BuildEntity(1, 1, "client", [(1, "first_name", "Ivan")]);
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        await _sut.ArchiveAsync(1, 1, 1);

        _entityRepo.Verify(r => r.ArchiveAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ArchiveAsync_EqualPriorityNonOwner_ThrowsAccessDenied()
    {
        var entity = BuildEntity(1, 1, "client", [(1, "first_name", "Ivan")], createdByUserId: 2);
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        _memberRepo
            .Setup(x => x.GetRolePrioritiesByUserIdsAsync(1, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int> { [1] = 4, [2] = 4 });

        await _sut.Invoking(s => s.ArchiveAsync(1, 1, 1))
            .Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("Access denied");
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_EnqueuesEntityUpdatedAuditEvent()
    {
        var storedEntity = BuildEntity(1, 1, "client", [(1, "first_name", "Olena")]);
        var typeProps = new List<EntityTypeProperty> { TypeProp(1, "first_name", required: true) };
        var reloaded = BuildEntity(1, 1, "client", [(1, "first_name", "Updated")]);

        _entityRepo.SetupSequence(r => r.GetByIdInWorkspaceAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedEntity)
            .ReturnsAsync(reloaded);
        _entityRepo.Setup(r => r.GetTypePropertiesAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typeProps);

        await _sut.UpdateAsync(1, 1, 5, new UpdateEntityRequest([new PropertyValueInput(1, "Updated")]));

        _auditOutboxWriter.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<Relativa.Persistence.Contracts.AuditEventContract>(e =>
                    e.AuditScope == Relativa.Persistence.Contracts.AuditRouting.ScopeEntity &&
                    e.Action == "entity_updated" &&
                    e.TargetId == 1 &&
                    e.ActorUserId == 5 &&
                    e.SourceService == "core"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ArchiveAsync_ValidRequest_EnqueuesEntityArchivedAuditEvent()
    {
        var entity = BuildEntity(1, 1, "client", [(1, "first_name", "Ivan")]);

        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        await _sut.ArchiveAsync(1, 1, 3);

        _auditOutboxWriter.Verify(
            x => x.EnqueueAuditAsync(
                It.Is<Relativa.Persistence.Contracts.AuditEventContract>(e =>
                    e.AuditScope == Relativa.Persistence.Contracts.AuditRouting.ScopeEntity &&
                    e.Action == "entity_archived" &&
                    e.TargetId == 1 &&
                    e.ActorUserId == 3),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithNullAuditWriter_CompletesWithoutError()
    {
        DefaultWorkspaceAccessMocks();
        var sut = new EntityService(
            _entityRepo.Object,
            _workspaceAccess.Object,
            _memberRepo.Object,
            _createValidator.Object,
            _updateValidator.Object,
            null);

        var typeProps = new List<EntityTypeProperty> { TypeProp(1, "first_name", required: true) };
        var created = BuildEntity(10, 1, "client", [(1, "first_name", "Ivan")]);

        _entityRepo.Setup(r => r.GetTypePropertiesAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typeProps);
        _entityRepo.Setup(r => r.CreateAsync(It.IsAny<Entity>(), It.IsAny<List<EntityPropertyValue>>(), 1, It.IsAny<IReadOnlyList<EntityRelationship>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(10, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var act = () => sut.CreateAsync(1, 1, new CreateEntityRequest(1, [new PropertyValueInput(1, "Ivan")]));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UpdateAsync_WithNullAuditWriter_CompletesWithoutError()
    {
        DefaultWorkspaceAccessMocks();
        var sut = new EntityService(
            _entityRepo.Object,
            _workspaceAccess.Object,
            _memberRepo.Object,
            _createValidator.Object,
            _updateValidator.Object,
            null);

        var storedEntity = BuildEntity(1, 1, "client", [(1, "first_name", "Olena")]);
        var typeProps = new List<EntityTypeProperty> { TypeProp(1, "first_name", required: true) };
        var reloaded = BuildEntity(1, 1, "client", [(1, "first_name", "Updated")]);

        _entityRepo.SetupSequence(r => r.GetByIdInWorkspaceAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedEntity)
            .ReturnsAsync(reloaded);
        _entityRepo.Setup(r => r.GetTypePropertiesAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typeProps);

        var act = () => sut.UpdateAsync(1, 1, 1, new UpdateEntityRequest([new PropertyValueInput(1, "Updated")]));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CreateAsync_PropertyDataType_Int_SetsIntValueAndResolvesCorrectly()
    {
        var typeProps = new List<EntityTypeProperty>
        {
            TypeProp(5, "age", required: false, type: PropertyDataType.Int)
        };
        List<EntityPropertyValue>? capturedValues = null;
        var created = new Entity
        {
            Id = 30, EntityTypeId = 3, IsArchived = false,
            EntityType = new EntityType { Id = 3, Name = "person" },
            EntityPropertyValues =
            [
                new EntityPropertyValue
                {
                    PropertyId = 5, ValueInt = 42,
                    Property = new Property { Name = "age", DataType = PropertyDataType.Int }
                }
            ]
        };

        _entityRepo.Setup(r => r.GetTypePropertiesAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typeProps);
        _entityRepo.Setup(r => r.CreateAsync(It.IsAny<Entity>(), It.IsAny<List<EntityPropertyValue>>(), 1, It.IsAny<IReadOnlyList<EntityRelationship>?>(), It.IsAny<CancellationToken>()))
            .Callback<Entity, List<EntityPropertyValue>, int, IReadOnlyList<EntityRelationship>?, CancellationToken>((_, pvs, _, _, _) => capturedValues = pvs)
            .ReturnsAsync(created);
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(30, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var result = await _sut.CreateAsync(1, 1, new CreateEntityRequest(3, [new PropertyValueInput(5, "42")]));

        capturedValues.Should().NotBeNull();
        capturedValues!.Single(v => v.PropertyId == 5).ValueInt.Should().Be(42);
        result.PropertyValues.Should().Contain(p => p.PropertyName == "age" && (int?)p.Value == 42);
    }

    [Fact]
    public async Task CreateAsync_PropertyDataType_Bool_SetsBoolValueAndResolvesCorrectly()
    {
        var typeProps = new List<EntityTypeProperty>
        {
            TypeProp(6, "is_active", required: false, type: PropertyDataType.Bool)
        };
        List<EntityPropertyValue>? capturedValues = null;
        var created = new Entity
        {
            Id = 31, EntityTypeId = 4, IsArchived = false,
            EntityType = new EntityType { Id = 4, Name = "subscription" },
            EntityPropertyValues =
            [
                new EntityPropertyValue
                {
                    PropertyId = 6, ValueBool = true,
                    Property = new Property { Name = "is_active", DataType = PropertyDataType.Bool }
                }
            ]
        };

        _entityRepo.Setup(r => r.GetTypePropertiesAsync(4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typeProps);
        _entityRepo.Setup(r => r.CreateAsync(It.IsAny<Entity>(), It.IsAny<List<EntityPropertyValue>>(), 1, It.IsAny<IReadOnlyList<EntityRelationship>?>(), It.IsAny<CancellationToken>()))
            .Callback<Entity, List<EntityPropertyValue>, int, IReadOnlyList<EntityRelationship>?, CancellationToken>((_, pvs, _, _, _) => capturedValues = pvs)
            .ReturnsAsync(created);
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(31, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var result = await _sut.CreateAsync(1, 1, new CreateEntityRequest(4, [new PropertyValueInput(6, "true")]));

        capturedValues.Should().NotBeNull();
        capturedValues!.Single(v => v.PropertyId == 6).ValueBool.Should().BeTrue();
        result.PropertyValues.Should().Contain(p => p.PropertyName == "is_active" && (bool?)p.Value == true);
    }

    // ------------------------------------------------------------------
    // Read-only property decoupling tests
    // ------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_EntityWithNonNullReadonlyProperty_SucceedsAndPreservesReadonlyValue()
    {
        var storedEntity = new Entity
        {
            Id = 1, EntityTypeId = 2, CreatedByUserId = 1, IsArchived = false,
            EntityType = new EntityType { Id = 2, Name = "deal" },
            EntityPropertyValues =
            [
                new EntityPropertyValue
                {
                    EntityId = 1, PropertyId = 7, ValueString = "opened",
                    Property = new Property { Id = 7, Name = "status", DataType = PropertyDataType.String }
                },
                new EntityPropertyValue
                {
                    EntityId = 1, PropertyId = 10, ValueDecimal = 0.75m,
                    Property = new Property { Id = 10, Name = "closure_score", DataType = PropertyDataType.Decimal, IsReadonly = true }
                }
            ]
        };
        var typeProps = new List<EntityTypeProperty>
        {
            new() { PropertyId = 7,  IsRequired = true,  Property = new Property { Id = 7,  Name = "status",        DataType = PropertyDataType.String,  IsReadonly = false } },
            new() { PropertyId = 10, IsRequired = false, Property = new Property { Id = 10, Name = "closure_score", DataType = PropertyDataType.Decimal, IsReadonly = true  } }
        };

        _entityRepo.SetupSequence(r => r.GetByIdInWorkspaceAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedEntity)
            .ReturnsAsync(storedEntity);
        _entityRepo.Setup(r => r.GetTypePropertiesAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typeProps);

        await _sut.UpdateAsync(1, 1, 1, new UpdateEntityRequest([new PropertyValueInput(7, "closed")]));

        _entityRepo.Verify(r => r.UpdateAsync(
            storedEntity,
            It.Is<List<EntityPropertyValue>>(l =>
                l.Any(x => x.PropertyId == 7  && x.ValueString == "closed") &&
                l.Any(x => x.PropertyId == 10 && x.ValueDecimal == 0.75m)),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_UserAttemptsToChangeReadonlyProperty_ThrowsArgumentException()
    {
        var storedEntity = new Entity
        {
            Id = 1, EntityTypeId = 2, CreatedByUserId = 1, IsArchived = false,
            EntityType = new EntityType { Id = 2, Name = "deal" },
            EntityPropertyValues =
            [
                new EntityPropertyValue
                {
                    EntityId = 1, PropertyId = 10, ValueDecimal = 0.75m,
                    Property = new Property { Id = 10, Name = "closure_score", DataType = PropertyDataType.Decimal, IsReadonly = true }
                }
            ]
        };
        var typeProps = new List<EntityTypeProperty>
        {
            new() { PropertyId = 10, IsRequired = false, Property = new Property { Id = 10, Name = "closure_score", DataType = PropertyDataType.Decimal, IsReadonly = true } }
        };

        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedEntity);
        _entityRepo.Setup(r => r.GetTypePropertiesAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typeProps);

        await _sut.Invoking(s => s.UpdateAsync(1, 1, 1, new UpdateEntityRequest([new PropertyValueInput(10, "0.99")])))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*closure_score*read-only*");
    }

    [Fact]
    public async Task CreateAsync_ExplicitlySubmittingReadonlyProperty_ThrowsArgumentException()
    {
        var typeProps = new List<EntityTypeProperty>
        {
            new() { PropertyId = 7,  IsRequired = true,  Property = new Property { Id = 7,  Name = "status",        DataType = PropertyDataType.String,  IsReadonly = false } },
            new() { PropertyId = 10, IsRequired = false, Property = new Property { Id = 10, Name = "closure_score", DataType = PropertyDataType.Decimal, IsReadonly = true  } }
        };
        _entityRepo.Setup(r => r.GetTypePropertiesAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(typeProps);

        await _sut.Invoking(s => s.CreateAsync(1, 1, new CreateEntityRequest(2,
        [
            new PropertyValueInput(7,  "opened"),
            new PropertyValueInput(10, "0.75")
        ])))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*closure_score*read-only*");
    }
}
