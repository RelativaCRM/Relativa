using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Relativa.Core.Infrastructure.Data;
using Relativa.Core.Infrastructure.Services.Audit;
using Relativa.Persistence.Contracts;
using Testcontainers.PostgreSql;
using Xunit;

namespace Relativa.Core.Integration.Tests;

public sealed class AuditOutboxWriterIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("relativa_test")
        .WithUsername("relativa")
        .WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    private RelativaDbContext _db = null!;
    private AuditOutboxWriter _writer = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var options = new DbContextOptionsBuilder<RelativaDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        _db = new RelativaDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _writer = new AuditOutboxWriter(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private static AuditEventContract Contract(string scope, string action, int actorId = 1, int targetId = 10) =>
        new(
            EventId: Guid.NewGuid(),
            SchemaVersion: 1,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            SourceService: "core",
            ActorUserId: actorId,
            AuditScope: scope,
            TargetId: targetId,
            Action: action,
            FieldName: null,
            EntityType: null,
            OldValueJson: null,
            NewValueJson: null);

    [Fact]
    public async Task EnqueueAsync_PersistsMessageWithCorrectRoutingKey()
    {
        var contract = Contract(AuditRouting.ScopeEntity, "entity_created");

        await _writer.EnqueueAsync(contract);

        var message = await _db.AuditOutboxMessages.SingleAsync(m => m.EventId == contract.EventId);
        message.RoutingKey.Should().Be("audit.entity");
        message.PublishedAtUtc.Should().BeNull();
        message.PublishAttempts.Should().Be(0);
    }

    [Fact]
    public async Task EnqueueAsync_PayloadJsonRoundTripsToOriginalContract()
    {
        var contract = Contract(AuditRouting.ScopeWorkspace, "workspace_created", actorId: 7, targetId: 42);

        await _writer.EnqueueAsync(contract);

        var message = await _db.AuditOutboxMessages.SingleAsync(m => m.EventId == contract.EventId);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<AuditEventContract>(message.PayloadJson);
        deserialized!.EventId.Should().Be(contract.EventId);
        deserialized.AuditScope.Should().Be(AuditRouting.ScopeWorkspace);
        deserialized.Action.Should().Be("workspace_created");
        deserialized.ActorUserId.Should().Be(7);
        deserialized.TargetId.Should().Be(42);
        deserialized.SourceService.Should().Be("core");
    }

    [Fact]
    public async Task EnqueueAsync_EachScopeProducesCorrectRoutingKey()
    {
        var entity = Contract(AuditRouting.ScopeEntity, "entity_created");
        var workspace = Contract(AuditRouting.ScopeWorkspace, "workspace_created");
        var org = Contract(AuditRouting.ScopeOrganization, "org_created");
        var user = Contract(AuditRouting.ScopeUser, "user_registered");

        await _writer.EnqueueAsync(entity);
        await _writer.EnqueueAsync(workspace);
        await _writer.EnqueueAsync(org);
        await _writer.EnqueueAsync(user);

        (await _db.AuditOutboxMessages.SingleAsync(m => m.EventId == entity.EventId)).RoutingKey
            .Should().Be("audit.entity");
        (await _db.AuditOutboxMessages.SingleAsync(m => m.EventId == workspace.EventId)).RoutingKey
            .Should().Be("audit.workspace");
        (await _db.AuditOutboxMessages.SingleAsync(m => m.EventId == org.EventId)).RoutingKey
            .Should().Be("audit.organization");
        (await _db.AuditOutboxMessages.SingleAsync(m => m.EventId == user.EventId)).RoutingKey
            .Should().Be("audit.user");
    }
}
