using FluentAssertions;
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
        _sut = new EntityTypeService(_repo.Object);
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
}
