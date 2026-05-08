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
    private readonly Mock<IUserRoleWorkspaceRepository> _memberRepo = new();
    private readonly Mock<IOutboxWriter> _auditOutboxWriter = new();
    private readonly Mock<IValidator<CreateEntityRequest>> _createValidator = new();
    private readonly Mock<IValidator<UpdateEntityRequest>> _updateValidator = new();
    private readonly EntityService _sut;

    public EntityServiceTests()
    {
        _sut = new EntityService(
            _entityRepo.Object,
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

    private static UserRoleWorkspace Member(int userId, int workspaceId, params string[] permissions) =>
        new()
        {
            UserId = userId,
            WorkspaceId = workspaceId,
            Role = new WorkspaceRole
            {
                Name = "test",
                RolePermissions = permissions
                    .Select(p => new WorkspaceRolePermission { Permission = new Permission { Name = p } })
                    .ToList()
            }
        };

    private static Property Prop(int id, string name, PropertyDataType type = PropertyDataType.String) =>
        new() { Id = id, Name = name, DataType = type };

    private static EntityTypeProperty TypeProp(int propertyId, string name, bool required = false,
        PropertyDataType type = PropertyDataType.String) =>
        new() { PropertyId = propertyId, IsRequired = required, Property = Prop(propertyId, name, type) };

    private static Entity BuildEntity(int id, int typeId, string typeName,
        IEnumerable<(int propId, string name, string value)> values, bool archived = false) =>
        new()
        {
            Id = id,
            EntityTypeId = typeId,
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
        _memberRepo.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 1, "create_entities", "edit_entities", "delete_entities"));

        await _sut.Invoking(s => s.GetByWorkspaceAsync(1, 1, null, null, 500))
            .Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*view_entities*");
    }

    [Fact]
    public async Task GetByWorkspaceAsync_ReturnsOnlyNonArchivedEntities()
    {
        var active = BuildEntity(1, 1, "client", [(1, "first_name", "Ivan")]);
        _memberRepo.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 1, "view_entities"));
        _entityRepo.Setup(r => r.GetByWorkspaceAsync(1, null, null, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([active]);

        var result = await _sut.GetByWorkspaceAsync(1, 1, null, null, 500);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(1);
        result[0].EntityTypeName.Should().Be("client");
    }

    [Fact]
    public async Task GetByIdAsync_EntityFoundInWorkspace_ReturnsAllFields()
    {
        var entity = BuildEntity(5, 1, "client",
        [
            (1, "first_name", "Olena"),
            (3, "last_name", "Koval")
        ]);
        _memberRepo.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 1, "view_entities"));
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(5, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await _sut.GetByIdAsync(5, 1, 1);

        result.Id.Should().Be(5);
        result.EntityTypeName.Should().Be("client");
        result.PropertyValues.Should().HaveCount(2);
        result.PropertyValues.Should().Contain(p => p.PropertyName == "first_name" && (string?)p.Value == "Olena");
        result.PropertyValues.Should().Contain(p => p.PropertyName == "last_name" && (string?)p.Value == "Koval");
    }

    [Fact]
    public async Task GetByIdAsync_EntityNotInWorkspace_ThrowsKeyNotFound()
    {
        _memberRepo.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 1, "view_entities"));
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(99, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Entity?)null);

        await _sut.Invoking(s => s.GetByIdAsync(99, 1, 1))
            .Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*99*");
    }

    [Fact]
    public async Task GetByIdAsync_UserNotMemberOfWorkspace_ThrowsUnauthorized()
    {
        _memberRepo.Setup(r => r.GetAsync(2, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserRoleWorkspace?)null);

        await _sut.Invoking(s => s.GetByIdAsync(5, 1, 2))
            .Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*not a member*");
    }


    [Fact]
    public async Task CreateAsync_UserLacksPermission_InvalidRequest_ThrowsUnauthorized_DoesNotValidate()
    {
        _memberRepo.Setup(r => r.GetAsync(5, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(5, 2, "view_entities"));

        await _sut.Invoking(s => s.CreateAsync(2, 5, new CreateEntityRequest(0, [])))
            .Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*create_entities*");

        _createValidator.Verify(
            v => v.ValidateAsync(It.IsAny<CreateEntityRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_UnknownEntityType_ThrowsKeyNotFoundException()
    {
        _memberRepo.Setup(r => r.GetAsync(1, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 3, "create_entities", "edit_entities", "delete_entities"));
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

        _memberRepo.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 1, "create_entities", "edit_entities", "delete_entities"));
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

        _memberRepo.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 1, "create_entities", "edit_entities", "delete_entities"));
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
        _memberRepo.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 1, "create_entities", "edit_entities", "delete_entities"));
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
        _memberRepo.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 1, "create_entities", "edit_entities", "delete_entities"));
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
        _memberRepo.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 1, "create_entities", "edit_entities", "delete_entities"));
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
        _memberRepo.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 1, "create_entities", "edit_entities", "delete_entities"));
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
        _memberRepo.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 1, "view_entities"));

        await _sut.Invoking(s => s.UpdateAsync(1, 1, 1, new UpdateEntityRequest(null!)))
            .Should().ThrowAsync<UnauthorizedAccessException>()
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

        _memberRepo.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 1, "create_entities", "edit_entities", "delete_entities"));
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
        _memberRepo.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 1, "create_entities", "edit_entities", "delete_entities"));
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(99, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Entity?)null);

        await _sut.Invoking(s => s.UpdateAsync(99, 1, 1, new UpdateEntityRequest([])))
            .Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*99*");
    }

    [Fact]
    public async Task UpdateAsync_ArchivedEntity_ThrowsArgumentException()
    {
        var archived = BuildEntity(1, 1, "client", [(1, "first_name", "Old")], archived: true);
        _memberRepo.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 1, "create_entities", "edit_entities", "delete_entities"));
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(archived);

        await _sut.Invoking(s => s.UpdateAsync(1, 1, 1, new UpdateEntityRequest([new PropertyValueInput(1, "New")])))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*archived*");
    }

    [Fact]
    public async Task UpdateAsync_UnknownPropertyId_ThrowsArgumentException()
    {
        var entity = BuildEntity(1, 1, "client", [(1, "first_name", "Ivan")]);
        var typeProps = new List<EntityTypeProperty> { TypeProp(1, "first_name", required: true) };

        _memberRepo.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 1, "create_entities", "edit_entities", "delete_entities"));
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
        _memberRepo.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 1, "create_entities", "edit_entities", "delete_entities"));
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(99, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Entity?)null);

        await _sut.Invoking(s => s.ArchiveAsync(99, 1, 1))
            .Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*99*");
    }

    [Fact]
    public async Task ArchiveAsync_UserLacksPermission_ThrowsUnauthorized()
    {
        _memberRepo.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 1, "view_entities"));

        await _sut.Invoking(s => s.ArchiveAsync(1, 1, 1))
            .Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*delete_entities*");
    }

    [Fact]
    public async Task ArchiveAsync_ValidRequest_CallsRepositoryArchive()
    {
        var entity = BuildEntity(1, 1, "client", [(1, "first_name", "Ivan")]);
        _memberRepo.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 1, "create_entities", "edit_entities", "delete_entities"));
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        await _sut.ArchiveAsync(1, 1, 1);

        _entityRepo.Verify(r => r.ArchiveAsync(entity, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ValidRequest_EnqueuesEntityUpdatedAuditEvent()
    {
        var storedEntity = BuildEntity(1, 1, "client", [(1, "first_name", "Olena")]);
        var typeProps = new List<EntityTypeProperty> { TypeProp(1, "first_name", required: true) };
        var reloaded = BuildEntity(1, 1, "client", [(1, "first_name", "Updated")]);

        _memberRepo.Setup(r => r.GetAsync(5, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(5, 1, "create_entities", "edit_entities", "delete_entities"));
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

        _memberRepo.Setup(r => r.GetAsync(3, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(3, 1, "create_entities", "edit_entities", "delete_entities"));
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
        var sut = new EntityService(
            _entityRepo.Object,
            _memberRepo.Object,
            _createValidator.Object,
            _updateValidator.Object,
            null);

        var typeProps = new List<EntityTypeProperty> { TypeProp(1, "first_name", required: true) };
        var created = BuildEntity(10, 1, "client", [(1, "first_name", "Ivan")]);

        _memberRepo.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 1, "create_entities", "edit_entities", "delete_entities"));
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
        var sut = new EntityService(
            _entityRepo.Object,
            _memberRepo.Object,
            _createValidator.Object,
            _updateValidator.Object,
            null);

        var storedEntity = BuildEntity(1, 1, "client", [(1, "first_name", "Olena")]);
        var typeProps = new List<EntityTypeProperty> { TypeProp(1, "first_name", required: true) };
        var reloaded = BuildEntity(1, 1, "client", [(1, "first_name", "Updated")]);

        _memberRepo.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 1, "create_entities", "edit_entities", "delete_entities"));
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

        _memberRepo.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 1, "create_entities", "edit_entities", "delete_entities"));
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

        _memberRepo.Setup(r => r.GetAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Member(1, 1, "create_entities", "edit_entities", "delete_entities"));
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
}
