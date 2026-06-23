using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class EntityTypeServiceTests
{
    private readonly Mock<IEntityTypeRepository> _repo = new();
    private readonly EntityTypeService _sut;

    public EntityTypeServiceTests()
    {
        _sut = new EntityTypeService(_repo.Object, new MemoryCache(new MemoryCacheOptions()));
    }

    [Fact]
    public async Task GetAllAsync_EmptyRepository_ReturnsEmptyList()
    {
        _repo.Setup(r => r.GetAllWithPropertiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.GetAllAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_MapsIdAndName()
    {
        _repo.Setup(r => r.GetAllWithPropertiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new EntityType { Id = 7, Name = "Client", EntityTypeProperties = [] }
            ]);

        var result = await _sut.GetAllAsync();

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(7);
        result[0].Name.Should().Be("Client");
        result[0].IsStandalone.Should().BeTrue();
        result[0].OutgoingRelationships.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_MapsPropertyFields()
    {
        var property = new Property { Id = 1, Name = "Phone", DataType = PropertyDataType.String };
        var entityType = new EntityType
        {
            Id = 1,
            Name = "Contact",
            EntityTypeProperties =
            [
                new EntityTypeProperty { PropertyId = 1, Property = property, IsRequired = true }
            ]
        };
        _repo.Setup(r => r.GetAllWithPropertiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([entityType]);

        var result = await _sut.GetAllAsync();

        var prop = result[0].Properties[0];
        prop.PropertyId.Should().Be(1);
        prop.Name.Should().Be("Phone");
        prop.DataType.Should().Be("String");
        prop.IsRequired.Should().BeTrue();
        prop.IsReadonly.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllAsync_MapsReadonlyPropertyFlag()
    {
        var property = new Property
        {
            Id = 2,
            Name = "derived_metric",
            DataType = PropertyDataType.Decimal,
            IsReadonly = true
        };
        var entityType = new EntityType
        {
            Id = 1,
            Name = "deal_analysis",
            EntityTypeProperties =
            [
                new EntityTypeProperty { PropertyId = 2, Property = property, IsRequired = true }
            ]
        };
        _repo.Setup(r => r.GetAllWithPropertiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([entityType]);

        var result = await _sut.GetAllAsync();

        result[0].Properties[0].IsReadonly.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllAsync_PropertiesOrderedByPropertyId()
    {
        var entityType = new EntityType
        {
            Id = 1,
            Name = "Deal",
            EntityTypeProperties =
            [
                new EntityTypeProperty { PropertyId = 5, Property = new Property { Id = 5, Name = "Amount", DataType = PropertyDataType.Decimal }, IsRequired = false },
                new EntityTypeProperty { PropertyId = 2, Property = new Property { Id = 2, Name = "Name", DataType = PropertyDataType.String }, IsRequired = true },
                new EntityTypeProperty { PropertyId = 9, Property = new Property { Id = 9, Name = "Closed", DataType = PropertyDataType.Date }, IsRequired = false }
            ]
        };
        _repo.Setup(r => r.GetAllWithPropertiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([entityType]);

        var result = await _sut.GetAllAsync();

        result[0].Properties.Select(p => p.PropertyId)
            .Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetAllAsync_MapsAllDataTypes()
    {
        var dataTypes = new[]
        {
            PropertyDataType.String,
            PropertyDataType.Int,
            PropertyDataType.Decimal,
            PropertyDataType.Bool,
            PropertyDataType.Date
        };
        var entityType = new EntityType
        {
            Id = 1,
            Name = "Full",
            EntityTypeProperties = dataTypes
                .Select((dt, i) => new EntityTypeProperty
                {
                    PropertyId = i + 1,
                    Property = new Property { Id = i + 1, Name = $"Field{i}", DataType = dt },
                    IsRequired = false
                })
                .ToList()
        };
        _repo.Setup(r => r.GetAllWithPropertiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([entityType]);

        var result = await _sut.GetAllAsync();

        result[0].Properties.Select(p => p.DataType)
            .Should().BeEquivalentTo(dataTypes.Select(dt => dt.ToString()));
    }

    [Fact]
    public async Task GetAllAsync_MapsOutgoingRelationshipsOrderedById()
    {
        var clientType = new EntityType { Id = 1, Name = "client", IsStandalone = true };
        var dealType = new EntityType { Id = 2, Name = "deal", IsStandalone = true };
        var relLater = new EntityRelationshipType
        {
            Id = 5,
            Name = "deal_contract",
            SourceEntityTypeId = dealType.Id,
            TargetEntityTypeId = 9,
            TargetEntityType = new EntityType { Id = 9, Name = "contract", IsStandalone = true },
            IsRequired = false
        };
        var relEarlier = new EntityRelationshipType
        {
            Id = 2,
            Name = "deal_client",
            SourceEntityTypeId = dealType.Id,
            SourceEntityType = dealType,
            TargetEntityTypeId = clientType.Id,
            TargetEntityType = clientType,
            IsRequired = true
        };
        dealType.SourceRelationshipTypes.Add(relLater);
        dealType.SourceRelationshipTypes.Add(relEarlier);
        clientType.TargetRelationshipTypes.Add(relEarlier);

        var entityTypes = new List<EntityType> { clientType, dealType };
        _repo.Setup(r => r.GetAllWithPropertiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(entityTypes);

        var result = await _sut.GetAllAsync();

        var dealDto = result.Single(t => t.Name == "deal");
        dealDto.OutgoingRelationships.Should().HaveCount(2);
        dealDto.OutgoingRelationships[0].RelationshipTypeId.Should().Be(2);
        dealDto.OutgoingRelationships[0].Name.Should().Be("deal_client");
        dealDto.OutgoingRelationships[0].TargetEntityTypeId.Should().Be(1);
        dealDto.OutgoingRelationships[0].TargetEntityTypeName.Should().Be("client");
        dealDto.OutgoingRelationships[0].IsRequired.Should().BeTrue();
        dealDto.OutgoingRelationships[1].RelationshipTypeId.Should().Be(5);
        dealDto.OutgoingRelationships[1].IsRequired.Should().BeFalse();

        var clientDto = result.Single(t => t.Name == "client");
        clientDto.IncomingRelationships.Should().HaveCount(1);
        clientDto.IncomingRelationships[0].RelationshipTypeId.Should().Be(2);
        clientDto.IncomingRelationships[0].Name.Should().Be("deal_client");
        clientDto.IncomingRelationships[0].SourceEntityTypeId.Should().Be(2);
        clientDto.IncomingRelationships[0].SourceEntityTypeName.Should().Be("deal");
        clientDto.IncomingRelationships[0].IsRequired.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllAsync_MultipleEntityTypes_ReturnsAll()
    {
        _repo.Setup(r => r.GetAllWithPropertiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new EntityType { Id = 1, Name = "Client", EntityTypeProperties = [] },
                new EntityType { Id = 2, Name = "Deal", EntityTypeProperties = [] },
                new EntityType { Id = 3, Name = "Task", EntityTypeProperties = [] }
            ]);

        var result = await _sut.GetAllAsync();

        result.Should().HaveCount(3);
        result.Select(r => r.Name).Should().BeEquivalentTo(["Client", "Deal", "Task"]);
    }

    [Fact]
    public async Task GetAllAsync_SecondCall_ServesFromCacheWithoutQueryingRepositoryAgain()
    {
        _repo.Setup(r => r.GetAllWithPropertiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new EntityType { Id = 1, Name = "Client", EntityTypeProperties = [] }]);

        var first = await _sut.GetAllAsync();
        var second = await _sut.GetAllAsync();

        second.Should().BeSameAs(first);
        _repo.Verify(r => r.GetAllWithPropertiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_PrefersExplicitDisplayNamesOverHumanizedFallback()
    {
        var clientType = new EntityType { Id = 1, Name = "client", DisplayName = "Customer", IsStandalone = true };
        var dealType = new EntityType { Id = 2, Name = "deal", DisplayName = "Opportunity", IsStandalone = true };
        var rel = new EntityRelationshipType
        {
            Id = 1,
            Name = "deal_client",
            DisplayName = "Belongs To",
            SourceEntityTypeId = dealType.Id,
            SourceEntityType = dealType,
            TargetEntityTypeId = clientType.Id,
            TargetEntityType = clientType,
            IsRequired = true
        };
        dealType.SourceRelationshipTypes.Add(rel);
        clientType.TargetRelationshipTypes.Add(rel);
        dealType.EntityTypeProperties.Add(new EntityTypeProperty
        {
            PropertyId = 1,
            Property = new Property
            {
                Id = 1,
                Name = "deal_value",
                DisplayName = "Contract Value",
                DataType = PropertyDataType.Decimal,
                AllowedValues = []
            },
            IsRequired = true
        });
        _repo.Setup(r => r.GetAllWithPropertiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([clientType, dealType]);

        var result = await _sut.GetAllAsync();

        var deal = result.Single(t => t.Name == "deal");
        deal.DisplayName.Should().Be("Opportunity");
        deal.Properties[0].DisplayName.Should().Be("Contract Value");
        deal.OutgoingRelationships[0].DisplayName.Should().Be("Belongs To");
        deal.OutgoingRelationships[0].TargetEntityTypeName.Should().Be("client");
        result.Single(t => t.Name == "client").DisplayName.Should().Be("Customer");
    }

    [Fact]
    public async Task GetAllAsync_MapsAllowedValues_WithDisplayNameFallback()
    {
        var entityType = new EntityType
        {
            Id = 1,
            Name = "deal",
            EntityTypeProperties =
            [
                new EntityTypeProperty
                {
                    PropertyId = 1,
                    Property = new Property
                    {
                        Id = 1,
                        Name = "deal_stage",
                        DataType = PropertyDataType.String,
                        AllowedValues =
                        [
                            new PropertyAllowedValue { Value = "in_progress", DisplayName = "Active" },
                            new PropertyAllowedValue { Value = "closed_won" }
                        ]
                    },
                    IsRequired = true
                }
            ]
        };
        _repo.Setup(r => r.GetAllWithPropertiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([entityType]);

        var allowed = (await _sut.GetAllAsync())[0].Properties[0].AllowedValues;

        allowed.Should().HaveCount(2);
        allowed[0].Value.Should().Be("in_progress");
        allowed[0].DisplayName.Should().Be("Active");
        allowed[1].Value.Should().Be("closed_won");
        allowed[1].DisplayName.Should().Be("Closed Won");
    }
}
