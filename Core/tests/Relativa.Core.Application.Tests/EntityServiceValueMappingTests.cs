using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Relativa.Core.Application.DTOs.Entity;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class EntityServiceValueMappingTests
{
    private readonly Mock<IEntityRepository> _entityRepo = new();
    private readonly Mock<IWorkspaceAccessEvaluator> _workspaceAccess = new();
    private readonly Mock<IUserRoleWorkspaceRepository> _memberRepo = new();
    private readonly Mock<IValidator<CreateEntityRequest>> _createValidator = new();
    private readonly Mock<IValidator<UpdateEntityRequest>> _updateValidator = new();
    private readonly EntityService _sut;

    private const int Ws = 1;
    private const int User = 2;

    public EntityServiceValueMappingTests()
    {
        _workspaceAccess
            .Setup(x => x.HasWorkspacePermissionAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _memberRepo
            .Setup(x => x.GetAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int u, int w, CancellationToken _) => new UserRoleWorkspace
            {
                UserId = u,
                WorkspaceId = w,
                Role = new WorkspaceRole { Priority = 4 }
            });

        _updateValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateEntityRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _sut = new EntityService(
            _entityRepo.Object,
            _workspaceAccess.Object,
            _memberRepo.Object,
            _createValidator.Object,
            _updateValidator.Object);
    }

    [Theory]
    [InlineData(PropertyDataType.Int)]
    [InlineData(PropertyDataType.Decimal)]
    [InlineData(PropertyDataType.Bool)]
    [InlineData(PropertyDataType.Date)]
    public async Task GetById_ResolvesTypedValue(PropertyDataType dataType)
    {
        var pv = new EntityPropertyValue
        {
            EntityId = 10,
            PropertyId = 1,
            Property = new Property { Id = 1, Name = "field", DataType = dataType },
            ValueInt = dataType == PropertyDataType.Int ? 5 : null,
            ValueDecimal = dataType == PropertyDataType.Decimal ? 9.9m : null,
            ValueBool = dataType == PropertyDataType.Bool ? true : null,
            ValueDate = dataType == PropertyDataType.Date ? new DateOnly(2026, 2, 1) : null,
        };
        var entity = new Entity
        {
            Id = 10,
            EntityTypeId = 7,
            CreatedByUserId = User,
            EntityType = new EntityType { Id = 7, Name = "deal" },
            EntityPropertyValues = [pv],
        };
        _entityRepo
            .Setup(r => r.GetByIdInWorkspaceAsync(10, Ws, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var detail = await _sut.GetByIdAsync(10, Ws, User);

        detail.PropertyValues.Should().ContainSingle().Which.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetById_DateValue_FormattedAsIsoString()
    {
        var pv = new EntityPropertyValue
        {
            EntityId = 11,
            PropertyId = 1,
            Property = new Property { Id = 1, Name = "closed_on", DataType = PropertyDataType.Date },
            ValueDate = new DateOnly(2026, 3, 9),
        };
        var entity = new Entity
        {
            Id = 11,
            EntityTypeId = 7,
            CreatedByUserId = User,
            EntityType = new EntityType { Id = 7, Name = "deal" },
            EntityPropertyValues = [pv],
        };
        _entityRepo
            .Setup(r => r.GetByIdInWorkspaceAsync(11, Ws, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var detail = await _sut.GetByIdAsync(11, Ws, User);

        detail.PropertyValues.Should().ContainSingle().Which.Value.Should().Be("2026-03-09");
    }

    [Fact]
    public async Task GetById_WithOutboundAndInboundRelationships_MapsRefsAndHumanizesDisplayNames()
    {
        EntityPropertyValue Pv(int entityId, PropertyDataType type) => new()
        {
            EntityId = entityId,
            PropertyId = 1,
            Property = new Property { Id = 1, Name = "deal_value", DataType = type },
            ValueDate = type == PropertyDataType.Date ? null : null,
            ValueString = type == PropertyDataType.String ? "v" : null,
        };

        Entity Related(int id) => new()
        {
            Id = id,
            EntityTypeId = 3,
            EntityType = new EntityType { Id = 3, Name = "related_type", DisplayName = null },
            EntityPropertyValues = [Pv(id, PropertyDataType.Date)],
        };

        var outbound = new EntityRelationship
        {
            Id = 71,
            RelationshipTypeId = 9,
            RelationshipType = new EntityRelationshipType { Id = 9, Name = "owns_deal", DisplayName = null },
            TargetEntityId = 20,
            TargetEntity = Related(20),
        };
        var inbound = new EntityRelationship
        {
            Id = 72,
            RelationshipTypeId = 10,
            RelationshipType = new EntityRelationshipType { Id = 10, Name = "managed_by", DisplayName = null },
            SourceEntityId = 30,
            SourceEntity = Related(30),
        };

        var entity = new Entity
        {
            Id = 12,
            EntityTypeId = 7,
            CreatedByUserId = User,
            EntityType = new EntityType { Id = 7, Name = "client" },
            SourceRelationships = [outbound],
            TargetRelationships = [inbound],
        };
        _entityRepo
            .Setup(r => r.GetByIdInWorkspaceAsync(12, Ws, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var detail = await _sut.GetByIdAsync(12, Ws, User);

        detail.OutboundRelationships.Should().ContainSingle()
            .Which.RelationshipDisplayName.Should().Be("Owns Deal");
        detail.InboundRelationships.Should().ContainSingle()
            .Which.RelationshipDisplayName.Should().Be("Managed By");
    }

    [Fact]
    public async Task Update_MergesExistingTypedValues_ViaValueToInputString()
    {
        EntityPropertyValue Existing(int propId, PropertyDataType type, object val) => new()
        {
            EntityId = 13,
            PropertyId = propId,
            Property = new Property { Id = propId, Name = "p" + propId, DataType = type },
            ValueInt = type == PropertyDataType.Int ? (int)val : null,
            ValueDecimal = type == PropertyDataType.Decimal ? (decimal)val : null,
            ValueBool = type == PropertyDataType.Bool ? (bool)val : null,
            ValueDate = type == PropertyDataType.Date ? (DateOnly)val : null,
        };

        var entity = new Entity
        {
            Id = 13,
            EntityTypeId = 7,
            CreatedByUserId = User,
            EntityType = new EntityType { Id = 7, Name = "deal" },
            EntityPropertyValues =
            [
                Existing(1, PropertyDataType.Int, 7),
                Existing(2, PropertyDataType.Decimal, 3.5m),
                Existing(3, PropertyDataType.Bool, true),
                Existing(4, PropertyDataType.Date, new DateOnly(2026, 5, 1)),
            ],
        };
        var typeProps = new List<EntityTypeProperty>
        {
            new() { PropertyId = 1, Property = new Property { Id = 1, Name = "p1", DataType = PropertyDataType.Int } },
            new() { PropertyId = 2, Property = new Property { Id = 2, Name = "p2", DataType = PropertyDataType.Decimal } },
            new() { PropertyId = 3, Property = new Property { Id = 3, Name = "p3", DataType = PropertyDataType.Bool } },
            new() { PropertyId = 4, Property = new Property { Id = 4, Name = "p4", DataType = PropertyDataType.Date } },
            new() { PropertyId = 5, Property = new Property { Id = 5, Name = "p5", DataType = PropertyDataType.String } },
        };
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(13, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        _entityRepo.Setup(r => r.GetTypePropertiesAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(typeProps);

        List<EntityPropertyValue>? written = null;
        _entityRepo
            .Setup(r => r.UpdateAsync(It.IsAny<Entity>(), It.IsAny<List<EntityPropertyValue>>(), It.IsAny<CancellationToken>()))
            .Callback((Entity _, List<EntityPropertyValue> vals, CancellationToken _) => written = vals)
            .Returns(Task.CompletedTask);

        await _sut.UpdateAsync(13, Ws, User, new UpdateEntityRequest([new PropertyValueInput(5, "hello")]));

        written.Should().NotBeNull();
        written!.Should().Contain(v => v.PropertyId == 1 && v.ValueInt == 7);
        written.Should().Contain(v => v.PropertyId == 2 && v.ValueDecimal == 3.5m);
        written.Should().Contain(v => v.PropertyId == 3 && v.ValueBool == true);
        written.Should().Contain(v => v.PropertyId == 4 && v.ValueDate == new DateOnly(2026, 5, 1));
    }
}
