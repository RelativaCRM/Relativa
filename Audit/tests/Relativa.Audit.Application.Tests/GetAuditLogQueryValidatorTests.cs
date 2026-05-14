using FluentAssertions;
using FluentValidation.TestHelper;
using Relativa.Audit.Application.Validators;
using Xunit;

namespace Relativa.Audit.Application.Tests;

public sealed class GetAuditLogQueryValidatorTests
{
    private readonly GetAuditLogQueryValidator _sut = new();

    private static GetAuditLogQuery EntityQuery(int? workspaceId = 5) =>
        new("entity", null, null, null, 1, 20, null, null, workspaceId, null, null, null);

    private static GetAuditLogQuery WorkspaceQuery(int? workspaceId = 5) =>
        new("workspace", null, null, null, 1, 20, null, null, workspaceId, null, null, null);

    private static GetAuditLogQuery OrgQuery(int? organizationId = 3) =>
        new("organization", null, null, null, 1, 20, null, null, null, organizationId, null, null);

    private static GetAuditLogQuery UserQuery() =>
        new("user", null, null, null, 1, 20, null, null, null, null, null, null);

    [Theory]
    [InlineData("entity")]
    [InlineData("workspace")]
    [InlineData("organization")]
    [InlineData("user")]
    public void AllValidCategories_WithRequiredFields_AreValid(string category)
    {
        var q = category switch
        {
            "entity" => EntityQuery(),
            "workspace" => WorkspaceQuery(),
            "organization" => OrgQuery(),
            _ => UserQuery()
        };

        _sut.TestValidate(q).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("ENTITY")]
    [InlineData("Entity")]
    [InlineData("WORKSPACE")]
    [InlineData("ORGANIZATION")]
    [InlineData("USER")]
    public void UppercaseCategories_WithRequiredFields_AreValid(string category)
    {
        var q = category.ToLowerInvariant() switch
        {
            "entity" => new GetAuditLogQuery(category, null, null, null, 1, 20, null, null, 5, null, null, null),
            "workspace" => new GetAuditLogQuery(category, null, null, null, 1, 20, null, null, 5, null, null, null),
            "organization" => new GetAuditLogQuery(category, null, null, null, 1, 20, null, null, null, 3, null, null),
            _ => new GetAuditLogQuery(category, null, null, null, 1, 20, null, null, null, null, null, null)
        };

        _sut.TestValidate(q).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unknown")]
    [InlineData("audit")]
    [InlineData("member")]
    [InlineData("all")]
    public void InvalidCategory_FailsValidation(string category) =>
        _sut.TestValidate(new GetAuditLogQuery(category, null, null, null, 1, 20, null, null, null, null, null, null))
            .ShouldHaveValidationErrorFor(q => q.EntityTypeCategory);

    [Fact]
    public void Validate_EntityScopeMissingWorkspaceId_HasError() =>
        _sut.TestValidate(EntityQuery(workspaceId: null))
            .ShouldHaveValidationErrorFor(q => q.WorkspaceId);

    [Fact]
    public void Validate_ValidEntityScopeWithWorkspace_IsValid() =>
        _sut.TestValidate(EntityQuery(workspaceId: 5))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void WorkspaceScope_MissingWorkspaceId_FailsValidation() =>
        _sut.TestValidate(WorkspaceQuery(workspaceId: null))
            .ShouldHaveValidationErrorFor(q => q.WorkspaceId);

    [Fact]
    public void OrganizationScope_MissingOrganizationId_FailsValidation() =>
        _sut.TestValidate(OrgQuery(organizationId: null))
            .ShouldHaveValidationErrorFor(q => q.OrganizationId);

    [Fact]
    public void Validate_InvalidCategory_HasError() =>
        _sut.TestValidate(new GetAuditLogQuery("unknown", null, null, null, 1, 20, null, null, null, null, null, null))
            .ShouldHaveValidationErrorFor(q => q.EntityTypeCategory);

    [Fact]
    public void UserScope_NoRequiredFields_IsValid() =>
        _sut.TestValidate(UserQuery())
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void IndexBelowOne_FailsValidation(int index) =>
        _sut.TestValidate(EntityQuery() with { Index = index })
            .ShouldHaveValidationErrorFor(q => q.Index);

    [Fact]
    public void IndexAtOne_IsValid() =>
        _sut.TestValidate(EntityQuery() with { Index = 1 })
            .ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(200)]
    public void PageSizeOutOfRange_FailsValidation(int pageSize) =>
        _sut.TestValidate(EntityQuery() with { PageSize = pageSize })
            .ShouldHaveValidationErrorFor(q => q.PageSize);

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void PageSizeInRange_IsValid(int pageSize) =>
        _sut.TestValidate(EntityQuery() with { PageSize = pageSize })
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void DateFromAfterDateTo_FailsValidation()
    {
        var from = DateTimeOffset.UtcNow;
        var to = from.AddDays(-1);
        _sut.TestValidate(EntityQuery() with { DateFrom = from, DateTo = to })
            .ShouldHaveValidationErrorFor("date_from");
    }

    [Fact]
    public void DateFromEqualToDateTo_IsValid()
    {
        var date = DateTimeOffset.UtcNow;
        _sut.TestValidate(EntityQuery() with { DateFrom = date, DateTo = date })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void DateFromBeforeDateTo_IsValid()
    {
        var from = DateTimeOffset.UtcNow.AddDays(-7);
        var to = DateTimeOffset.UtcNow;
        _sut.TestValidate(EntityQuery() with { DateFrom = from, DateTo = to })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void NullDateRange_IsValid() =>
        _sut.TestValidate(EntityQuery() with { DateFrom = null, DateTo = null })
            .ShouldNotHaveAnyValidationErrors();
}
