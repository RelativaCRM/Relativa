using FluentAssertions;
using FluentValidation;
using Moq;
using Relativa.Core.Application.DTOs.Entity;
using Relativa.Core.Application.Exceptions;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class EntityServiceFilterResolutionTests
{
    private readonly Mock<IEntityRepository> _entityRepo = new();
    private readonly Mock<IWorkspaceAccessEvaluator> _workspaceAccess = new();
    private readonly Mock<IUserRoleWorkspaceRepository> _memberRepo = new();
    private readonly Mock<IValidator<CreateEntityRequest>> _createValidator = new();
    private readonly Mock<IValidator<UpdateEntityRequest>> _updateValidator = new();
    private readonly EntityService _sut;

    private const int Ws = 1;
    private const int User = 2;

    public EntityServiceFilterResolutionTests()
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
        _entityRepo
            .Setup(r => r.GetByWorkspaceAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<ResolvedFilterCondition>>(),
                It.IsAny<IReadOnlyList<EntitySortField>>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Entity>(), 0));

        _sut = new EntityService(
            _entityRepo.Object,
            _workspaceAccess.Object,
            _memberRepo.Object,
            _createValidator.Object,
            _updateValidator.Object);
    }

    private void SetupTypeProperty(int entityTypeId, int propertyId, PropertyDataType dataType, bool isReadonly = false)
    {
        _entityRepo
            .Setup(r => r.GetTypePropertiesAsync(entityTypeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new EntityTypeProperty
                {
                    PropertyId = propertyId,
                    Property = new Property { Id = propertyId, Name = "field", DataType = dataType, IsReadonly = isReadonly }
                }
            ]);
    }

    private async Task<IReadOnlyList<ResolvedFilterCondition>> CaptureResolvedAsync(int entityTypeId, EntityFilterCondition filter)
    {
        IReadOnlyList<ResolvedFilterCondition>? captured = null;
        _entityRepo
            .Setup(r => r.GetByWorkspaceAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<string?>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<ResolvedFilterCondition>>(),
                It.IsAny<IReadOnlyList<EntitySortField>>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Callback((int _, int _, int _, int? _, string? _, int _, int _,
                IReadOnlyList<ResolvedFilterCondition> filters, IReadOnlyList<EntitySortField> _, int? _, int? _, CancellationToken _) =>
                captured = filters)
            .ReturnsAsync((new List<Entity>(), 0));

        await _sut.GetByWorkspaceAsync(Ws, User, entityTypeId, null, 0, 50, [filter]);
        captured.Should().NotBeNull();
        return captured!;
    }

    [Fact]
    public async Task FiltersWithoutEntityType_Throws()
    {
        await _sut.Invoking(s => s.GetByWorkspaceAsync(Ws, User, null, null, 0, 50,
                [new EntityFilterCondition(1, "eq", "x")]))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "entity_type_required_for_filters");
    }

    [Fact]
    public async Task FilterOnUnknownEntityType_Throws()
    {
        _entityRepo
            .Setup(r => r.GetTypePropertiesAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _sut.Invoking(s => s.GetByWorkspaceAsync(Ws, User, 99, null, 0, 50,
                [new EntityFilterCondition(1, "eq", "x")]))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "entity_type_not_found");
    }

    [Fact]
    public async Task FilterOnPropertyNotInType_Throws()
    {
        SetupTypeProperty(7, propertyId: 1, PropertyDataType.String);

        await _sut.Invoking(s => s.GetByWorkspaceAsync(Ws, User, 7, null, 0, 50,
                [new EntityFilterCondition(999, "eq", "x")]))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "property_not_in_entity_type");
    }

    [Fact]
    public async Task ReadonlyPropertyWithoutAnalytics_FilterDropped()
    {
        SetupTypeProperty(7, propertyId: 1, PropertyDataType.String, isReadonly: true);
        _workspaceAccess
            .Setup(x => x.HasWorkspacePermissionAsync(User, Ws, "view_analytics", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var resolved = await CaptureResolvedAsync(7, new EntityFilterCondition(1, "eq", "x"));
        resolved.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadonlyPropertyWithAnalytics_FilterKept()
    {
        SetupTypeProperty(7, propertyId: 1, PropertyDataType.String, isReadonly: true);
        _workspaceAccess
            .Setup(x => x.HasWorkspacePermissionAsync(User, Ws, "view_analytics", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var resolved = await CaptureResolvedAsync(7, new EntityFilterCondition(1, "eq", "x"));
        resolved.Should().ContainSingle().Which.StringValue.Should().Be("x");
    }

    [Fact]
    public async Task StringFilter_ResolvesStringValue()
    {
        SetupTypeProperty(7, 1, PropertyDataType.String);
        var resolved = await CaptureResolvedAsync(7, new EntityFilterCondition(1, "contains", "abc"));
        resolved.Should().ContainSingle().Which.StringValue.Should().Be("abc");
    }

    [Fact]
    public async Task IntFilter_Valid_ResolvesIntValue()
    {
        SetupTypeProperty(7, 1, PropertyDataType.Int);
        var resolved = await CaptureResolvedAsync(7, new EntityFilterCondition(1, "gte", "42"));
        resolved.Should().ContainSingle().Which.IntValue.Should().Be(42);
    }

    [Fact]
    public async Task IntFilter_Invalid_Throws()
    {
        SetupTypeProperty(7, 1, PropertyDataType.Int);
        await _sut.Invoking(s => s.GetByWorkspaceAsync(Ws, User, 7, null, 0, 50,
                [new EntityFilterCondition(1, "gte", "not-int")]))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "property_expects_integer");
    }

    [Fact]
    public async Task DecimalFilter_Valid_ResolvesDecimalValue()
    {
        SetupTypeProperty(7, 1, PropertyDataType.Decimal);
        var resolved = await CaptureResolvedAsync(7, new EntityFilterCondition(1, "lt", "12.5"));
        resolved.Should().ContainSingle().Which.DecimalValue.Should().Be(12.5m);
    }

    [Fact]
    public async Task DecimalFilter_Invalid_Throws()
    {
        SetupTypeProperty(7, 1, PropertyDataType.Decimal);
        await _sut.Invoking(s => s.GetByWorkspaceAsync(Ws, User, 7, null, 0, 50,
                [new EntityFilterCondition(1, "lt", "money")]))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "property_expects_decimal");
    }

    [Fact]
    public async Task BoolFilter_Valid_ResolvesBoolValue()
    {
        SetupTypeProperty(7, 1, PropertyDataType.Bool);
        var resolved = await CaptureResolvedAsync(7, new EntityFilterCondition(1, "eq", "true"));
        resolved.Should().ContainSingle().Which.BoolValue.Should().BeTrue();
    }

    [Fact]
    public async Task BoolFilter_Invalid_Throws()
    {
        SetupTypeProperty(7, 1, PropertyDataType.Bool);
        await _sut.Invoking(s => s.GetByWorkspaceAsync(Ws, User, 7, null, 0, 50,
                [new EntityFilterCondition(1, "eq", "maybe")]))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "property_expects_boolean");
    }

    [Fact]
    public async Task DateFilter_Valid_ResolvesDateValue()
    {
        SetupTypeProperty(7, 1, PropertyDataType.Date);
        var resolved = await CaptureResolvedAsync(7, new EntityFilterCondition(1, "gt", "2026-01-15"));
        resolved.Should().ContainSingle().Which.DateValue.Should().Be(new DateOnly(2026, 1, 15));
    }

    [Fact]
    public async Task DateFilter_Invalid_Throws()
    {
        SetupTypeProperty(7, 1, PropertyDataType.Date);
        await _sut.Invoking(s => s.GetByWorkspaceAsync(Ws, User, 7, null, 0, 50,
                [new EntityFilterCondition(1, "gt", "15/01/2026")]))
            .Should().ThrowAsync<AppException>()
            .Where(e => e.Code == "property_expects_date");
    }
}
