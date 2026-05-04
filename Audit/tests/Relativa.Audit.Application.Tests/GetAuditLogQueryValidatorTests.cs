using FluentAssertions;
using Relativa.Audit.Application.Validators;
using Xunit;

namespace Relativa.Audit.Application.Tests;

public sealed class GetAuditLogQueryValidatorTests
{
    private readonly GetAuditLogQueryValidator _sut = new();

    [Fact]
    public void Validate_EntityScopeMissingWorkspaceId_HasError()
    {
        var q = new GetAuditLogQuery(
            EntityTypeCategory: "entity",
            DateFrom: null,
            DateTo: null,
            Action: null,
            Index: 1,
            PageSize: 20,
            EntityId: 1,
            DomainEntityType: null,
            WorkspaceId: null,
            OrganizationId: null,
            ActorUserId: null,
            TargetUserId: null);

        var result = _sut.Validate(q);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(GetAuditLogQuery.WorkspaceId));
    }

    [Fact]
    public void Validate_ValidEntityScopeWithWorkspace_IsValid()
    {
        var q = new GetAuditLogQuery(
            EntityTypeCategory: "entity",
            DateFrom: null,
            DateTo: null,
            Action: null,
            Index: 1,
            PageSize: 20,
            EntityId: 1,
            DomainEntityType: null,
            WorkspaceId: 5,
            OrganizationId: null,
            ActorUserId: null,
            TargetUserId: null);

        var result = _sut.Validate(q);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidCategory_HasError()
    {
        var q = new GetAuditLogQuery(
            EntityTypeCategory: "unknown",
            DateFrom: null,
            DateTo: null,
            Action: null,
            Index: 1,
            PageSize: 20,
            EntityId: null,
            DomainEntityType: null,
            WorkspaceId: null,
            OrganizationId: null,
            ActorUserId: null,
            TargetUserId: null);

        var result = _sut.Validate(q);

        result.IsValid.Should().BeFalse();
    }
}
