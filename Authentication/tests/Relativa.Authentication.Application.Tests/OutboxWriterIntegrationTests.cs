using System.Text.Json;
using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Relativa.Authentication.Infrastructure.Data;
using Relativa.Authentication.Infrastructure.Services.Audit;
using Relativa.Persistence.Contracts;
using Testcontainers.PostgreSql;
using Xunit;

namespace Relativa.Authentication.Application.Tests;

public sealed class OutboxWriterIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("auth_outbox_test")
        .WithUsername("relativa")
        .WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    private DbContextOptions<AuthDbContext> _opts = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _opts = new DbContextOptionsBuilder<AuthDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var db = Db();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private AuthDbContext Db() => new(_opts);

    private static AuditEventContract Event(string scope, Guid? id = null) => new(
        EventId: id ?? Guid.NewGuid(),
        SchemaVersion: 1,
        OccurredAtUtc: DateTimeOffset.UtcNow,
        SourceService: "auth",
        ActorUserId: 7,
        AuditScope: scope,
        TargetId: 42,
        Action: "user_logged_in",
        FieldName: null,
        EntityType: null,
        OldValueJson: null,
        NewValueJson: null);

    [Fact]
    public async Task EnqueueAuditAsync_PersistsPendingMessageWithScopedRoutingKey()
    {
        var evt = Event(AuditRouting.ScopeUser);

        await using (var db = Db())
        {
            await new OutboxWriter(db).EnqueueAuditAsync(evt);
        }

        await using (var db = Db())
        {
            var stored = await db.AuditOutboxMessages.SingleAsync();
            stored.EventId.Should().Be(evt.EventId);
            stored.RoutingKey.Should().Be("audit.user", "the routing key is derived as audit.<scope> so the broker topic exchange routes it correctly");
            stored.PublishedAtUtc.Should().BeNull("a freshly enqueued event is pending until the dispatcher publishes it");
            stored.PublishAttempts.Should().Be(0);
        }
    }

    [Fact]
    public async Task EnqueueAuditAsync_SerializesPayloadThatRoundTripsToTheSameContract()
    {
        var evt = Event(AuditRouting.ScopeOrganization);

        await using (var db = Db())
        {
            await new OutboxWriter(db).EnqueueAuditAsync(evt);
        }

        await using (var db = Db())
        {
            var stored = await db.AuditOutboxMessages.SingleAsync();
            var roundTripped = JsonSerializer.Deserialize<AuditEventContract>(stored.PayloadJson);
            roundTripped.Should().Be(evt, "the persisted payload must preserve every field of the audit contract");
            stored.OccurredAtUtc.Should().BeCloseTo(evt.OccurredAtUtc, TimeSpan.FromMilliseconds(1),
                "the occurred-at column persists the event time to database (microsecond) precision");
        }
    }

    [Fact]
    public async Task EnqueueAuditAsync_DifferentScope_ProducesDifferentRoutingKey()
    {
        await using (var db = Db())
        {
            var writer = new OutboxWriter(db);
            await writer.EnqueueAuditAsync(Event(AuditRouting.ScopeUser));
            await writer.EnqueueAuditAsync(Event(AuditRouting.ScopeWorkspace));
        }

        await using (var db = Db())
        {
            var keys = await db.AuditOutboxMessages.Select(m => m.RoutingKey).ToListAsync();
            keys.Should().BeEquivalentTo(["audit.user", "audit.workspace"]);
        }
    }

    [Fact]
    public async Task EnqueueDomainAsync_IsANoOp_AuthServicePublishesNoChoreographyMessages()
    {
        await using (var db = Db())
        {
            await new OutboxWriter(db).EnqueueDomainAsync("anything", null!);
        }

        await using (var db = Db())
        {
            (await db.AuditOutboxMessages.CountAsync()).Should().Be(0,
                "the auth service intentionally does not emit domain choreography messages, so nothing is persisted");
        }
    }
}
