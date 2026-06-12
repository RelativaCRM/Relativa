using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Relativa.Core.Application.DTOs.Entity;
using Relativa.Core.Application.Exceptions;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Application.Services;
using Relativa.Core.Domain.Interfaces;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class EntityPropertyValidationTests
{
    private readonly Mock<IEntityRepository> _entityRepo = new();
    private readonly Mock<IWorkspaceAccessEvaluator> _workspaceAccess = new();
    private readonly Mock<IUserRoleWorkspaceRepository> _memberRepo = new();
    private readonly Mock<IValidator<CreateEntityRequest>> _createValidator = new();
    private readonly Mock<IValidator<UpdateEntityRequest>> _updateValidator = new();
    private readonly EntityService _sut;

    private const int Ws = 1;
    private const int UserId = 7;
    private const int TypeId = 100;

    public EntityPropertyValidationTests()
    {
        _workspaceAccess.Setup(x => x.HasWorkspacePermissionAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _createValidator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateEntityRequest>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());
        _entityRepo.Setup(r => r.GetOutgoingRelationshipTypesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _sut = new EntityService(_entityRepo.Object, _workspaceAccess.Object, _memberRepo.Object, _createValidator.Object, _updateValidator.Object);
    }

    private static Property Prop(int id, string name, PropertyDataType type, bool readOnly = false, string[]? allowed = null)
    {
        var p = new Property { Id = id, Name = name, DataType = type, IsReadonly = readOnly };
        if (allowed is not null)
            foreach (var a in allowed) p.AllowedValues.Add(new PropertyAllowedValue { PropertyId = id, Value = a });
        return p;
    }

    private static EntityTypeProperty TypeProp(Property p, bool required = false) =>
        new() { EntityTypeId = TypeId, PropertyId = p.Id, IsRequired = required, Property = p };

    private void TypeHas(params EntityTypeProperty[] props) =>
        _entityRepo.Setup(r => r.GetTypePropertiesAsync(TypeId, It.IsAny<CancellationToken>())).ReturnsAsync(props.ToList());

    private static CreateEntityRequest Req(params (int propId, string? value)[] values) =>
        new(TypeId, values.Select(v => new PropertyValueInput(v.propId, v.value)).ToList());

    private async Task<string> CodeFromCreate(CreateEntityRequest request)
    {
        var ex = await Assert.ThrowsAsync<AppException>(() => _sut.CreateAsync(Ws, UserId, request));
        return ex.Code;
    }

    [Fact]
    public async Task IntProperty_NonNumericValue_Throws() { TypeHas(TypeProp(Prop(1, "count", PropertyDataType.Int))); (await CodeFromCreate(Req((1, "abc")))).Should().Be("property_expects_integer"); }

    [Fact]
    public async Task DecimalProperty_NonNumericValue_Throws() { TypeHas(TypeProp(Prop(1, "amount", PropertyDataType.Decimal))); (await CodeFromCreate(Req((1, "x")))).Should().Be("property_expects_decimal"); }

    [Fact]
    public async Task BoolProperty_NonBooleanValue_Throws() { TypeHas(TypeProp(Prop(1, "flag", PropertyDataType.Bool))); (await CodeFromCreate(Req((1, "maybe")))).Should().Be("property_expects_boolean"); }

    [Fact]
    public async Task DateProperty_WrongFormat_Throws() { TypeHas(TypeProp(Prop(1, "due", PropertyDataType.Date))); (await CodeFromCreate(Req((1, "31-12-2026")))).Should().Be("property_expects_date"); }

    [Fact]
    public async Task StringProperty_ValueNotInAllowedSet_Throws() { TypeHas(TypeProp(Prop(1, "color", PropertyDataType.String, allowed: ["red", "green"]))); (await CodeFromCreate(Req((1, "blue")))).Should().Be("invalid_allowed_value"); }

    [Fact]
    public async Task DuplicatePropertyIds_Throws() { TypeHas(TypeProp(Prop(1, "a", PropertyDataType.String))); (await CodeFromCreate(Req((1, "x"), (1, "y")))).Should().Be("duplicate_property_ids"); }

    [Fact]
    public async Task UnknownPropertyId_Throws() { TypeHas(TypeProp(Prop(1, "a", PropertyDataType.String))); (await CodeFromCreate(Req((999, "x")))).Should().Be("properties_not_in_entity_type"); }

    [Fact]
    public async Task MissingRequiredProperty_Throws() { TypeHas(TypeProp(Prop(1, "a", PropertyDataType.String)), TypeProp(Prop(2, "req", PropertyDataType.Int), required: true)); (await CodeFromCreate(Req((1, "x")))).Should().Be("required_properties_missing"); }

    [Fact]
    public async Task RequiredStringProperty_Whitespace_Throws() { TypeHas(TypeProp(Prop(1, "name", PropertyDataType.String), required: true)); (await CodeFromCreate(Req((1, "   ")))).Should().Be("required_string_empty"); }

    [Fact]
    public async Task ReadonlyPropertySubmitted_Throws() { TypeHas(TypeProp(Prop(1, "a", PropertyDataType.String)), TypeProp(Prop(2, "locked", PropertyDataType.String, readOnly: true))); (await CodeFromCreate(Req((1, "x"), (2, "tamper")))).Should().Be("property_read_only"); }

    [Fact]
    public async Task EntityTypeWithNoProperties_Throws() { _entityRepo.Setup(r => r.GetTypePropertiesAsync(TypeId, It.IsAny<CancellationToken>())).ReturnsAsync([]); (await CodeFromCreate(Req((1, "x")))).Should().Be("entity_type_not_found"); }

    [Fact]
    public async Task EntityTypeAllReadonly_Throws() { TypeHas(TypeProp(Prop(1, "a", PropertyDataType.String, readOnly: true))); (await CodeFromCreate(Req())).Should().Be("entity_type_all_readonly"); }

    private void OutgoingHas(EntityRelationshipType rt) =>
        _entityRepo.Setup(r => r.GetOutgoingRelationshipTypesAsync(TypeId, It.IsAny<CancellationToken>())).ReturnsAsync([rt]);

    private static EntityRelationshipType RelType(int id = 10, int targetType = 200, bool required = false) =>
        new() { Id = id, Name = "deal_client", SourceEntityTypeId = TypeId, TargetEntityTypeId = targetType, IsRequired = required };

    [Fact]
    public async Task RequiredRelationshipMissing_Throws()
    {
        TypeHas(TypeProp(Prop(1, "a", PropertyDataType.String)));
        OutgoingHas(RelType(required: true));
        (await CodeFromCreate(Req((1, "x")))).Should().Be("required_relationship_missing");
    }

    [Fact]
    public async Task Link_WithRelationshipTypeNotValidForEntity_Throws()
    {
        TypeHas(TypeProp(Prop(1, "a", PropertyDataType.String)));
        var request = new CreateEntityRequest(TypeId, [new PropertyValueInput(1, "x")], [new EntityRelationshipLinkInput(999, 5)]);
        (await CodeFromCreate(request)).Should().Be("relationship_type_invalid_for_entity");
    }

    [Fact]
    public async Task Link_TargetEntityNotInWorkspace_Throws()
    {
        TypeHas(TypeProp(Prop(1, "a", PropertyDataType.String)));
        OutgoingHas(RelType());
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(5, Ws, It.IsAny<CancellationToken>())).ReturnsAsync((Entity?)null);
        var request = new CreateEntityRequest(TypeId, [new PropertyValueInput(1, "x")], [new EntityRelationshipLinkInput(10, 5)]);
        (await CodeFromCreate(request)).Should().Be("target_entity_not_found");
    }

    [Fact]
    public async Task Link_TargetEntityWrongType_Throws()
    {
        TypeHas(TypeProp(Prop(1, "a", PropertyDataType.String)));
        OutgoingHas(RelType(targetType: 200));
        _entityRepo.Setup(r => r.GetByIdInWorkspaceAsync(5, Ws, It.IsAny<CancellationToken>())).ReturnsAsync(new Entity { Id = 5, EntityTypeId = 999 });
        var request = new CreateEntityRequest(TypeId, [new PropertyValueInput(1, "x")], [new EntityRelationshipLinkInput(10, 5)]);
        (await CodeFromCreate(request)).Should().Be("target_entity_wrong_type");
    }
}
