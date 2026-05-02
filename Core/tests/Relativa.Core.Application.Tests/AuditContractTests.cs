using FluentAssertions;
using Relativa.Persistence.Contracts;
using Xunit;

namespace Relativa.Core.Application.Tests;

public sealed class AuditContractTests
{
    [Fact]
    public void AuditEventContract_SerializationRoundTrip_PreservesAllFields()
    {
        var original = new AuditEventContract(
            EventId: Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
            SchemaVersion: 1,
            OccurredAtUtc: new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero),
            SourceService: "core",
            ActorUserId: 42,
            AuditScope: AuditRouting.ScopeEntity,
            TargetId: 7,
            Action: "entity_created",
            FieldName: "properties",
            EntityType: "client",
            OldValueJson: "{\"name\":\"old\"}",
            NewValueJson: "{\"name\":\"new\"}");

        var json = System.Text.Json.JsonSerializer.Serialize(original);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<AuditEventContract>(json);

        deserialized!.EventId.Should().Be(original.EventId);
        deserialized.SchemaVersion.Should().Be(original.SchemaVersion);
        deserialized.OccurredAtUtc.Should().Be(original.OccurredAtUtc);
        deserialized.SourceService.Should().Be(original.SourceService);
        deserialized.ActorUserId.Should().Be(original.ActorUserId);
        deserialized.AuditScope.Should().Be(original.AuditScope);
        deserialized.TargetId.Should().Be(original.TargetId);
        deserialized.Action.Should().Be(original.Action);
        deserialized.FieldName.Should().Be(original.FieldName);
        deserialized.EntityType.Should().Be(original.EntityType);
        deserialized.OldValueJson.Should().Be(original.OldValueJson);
        deserialized.NewValueJson.Should().Be(original.NewValueJson);
    }

    [Fact]
    public void AuditRouting_ScopeConstants_HaveExpectedValues()
    {
        AuditRouting.ScopeEntity.Should().Be("entity");
        AuditRouting.ScopeWorkspace.Should().Be("workspace");
        AuditRouting.ScopeOrganization.Should().Be("organization");
        AuditRouting.ScopeUser.Should().Be("user");
        AuditRouting.ExchangeName.Should().Be("audit.events");
    }
}
