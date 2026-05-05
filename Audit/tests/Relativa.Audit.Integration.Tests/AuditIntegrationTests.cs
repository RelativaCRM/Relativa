using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Relativa.Audit.Infrastructure.Data;
using Relativa.Audit.Infrastructure.Services;
using Relativa.Core.Infrastructure.Data;
using Relativa.Core.Infrastructure.Services.Audit;
using Relativa.Messaging;
using Relativa.Persistence.Contracts;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

using AuditRmqOptions = Relativa.Audit.Infrastructure.Services.RabbitMqAuditOptions;
using CoreRmqOptions = Relativa.Messaging.RabbitMqPublishingOptions;

namespace Relativa.Audit.Integration.Tests;

public sealed class AuditIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("audit_test")
        .WithUsername("relativa")
        .WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    private readonly RabbitMqContainer _rabbitmq = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-alpine")
        .Build();

    private DbContextOptions<AuditDbContext> _auditDbOptions = null!;
    private DbContextOptions<RelativaDbContext> _coreDbOptions = null!;
    private IHost _consumerHost = null!;
    private IHost _dispatcherHost = null!;
    private IConnection _publishConnection = null!;
    private IChannel _publishChannel = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitmq.StartAsync());

        _coreDbOptions = new DbContextOptionsBuilder<RelativaDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        _auditDbOptions = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using (var auditDb = new AuditDbContext(_auditDbOptions))
        {
            await auditDb.Database.EnsureCreatedAsync();
            await auditDb.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE entity_audit_log DROP CONSTRAINT IF EXISTS fk_entity_audit_log_entities;
                ALTER TABLE entity_audit_log DROP CONSTRAINT IF EXISTS fk_entity_audit_log_users;
                ALTER TABLE workspace_audit_log DROP CONSTRAINT IF EXISTS fk_workspace_audit_log_workspaces;
                ALTER TABLE workspace_audit_log DROP CONSTRAINT IF EXISTS fk_workspace_audit_log_users;
                ALTER TABLE organization_audit_log DROP CONSTRAINT IF EXISTS fk_organization_audit_log_organizations;
                ALTER TABLE organization_audit_log DROP CONSTRAINT IF EXISTS fk_organization_audit_log_users;
                ALTER TABLE user_audit_log DROP CONSTRAINT IF EXISTS fk_user_audit_log_target_users;
                ALTER TABLE user_audit_log DROP CONSTRAINT IF EXISTS fk_user_audit_log_users;
            ");
        }

        var rmqUri = new Uri(_rabbitmq.GetConnectionString());
        _publishConnection = await new ConnectionFactory { Uri = rmqUri }.CreateConnectionAsync();
        _publishChannel = await _publishConnection.CreateChannelAsync();
        await _publishChannel.ExchangeDeclareAsync(AuditRouting.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
        await _publishChannel.QueueDeclareAsync("audit.events.audit", durable: true, exclusive: false, autoDelete: false);
        await _publishChannel.QueueBindAsync("audit.events.audit", AuditRouting.ExchangeName, "audit.#");

        _consumerHost = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddDbContext<AuditDbContext>(opt => opt.UseNpgsql(_postgres.GetConnectionString()));
                services.Configure<AuditRmqOptions>(opt =>
                {
                    opt.Host = rmqUri.Host;
                    opt.Port = rmqUri.Port;
                    opt.Username = Uri.UnescapeDataString(rmqUri.UserInfo.Split(':')[0]);
                    opt.Password = Uri.UnescapeDataString(rmqUri.UserInfo.Split(':')[1]);
                });
                services.AddHostedService<AuditEventConsumer>();
                services.AddLogging();
            })
            .Build();
        await _consumerHost.StartAsync();

        _dispatcherHost = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddDbContext<RelativaDbContext>(opt => opt.UseNpgsql(_postgres.GetConnectionString()));
                services.Configure<CoreRmqOptions>(opt =>
                {
                    opt.Host = rmqUri.Host;
                    opt.Port = rmqUri.Port;
                    opt.Username = Uri.UnescapeDataString(rmqUri.UserInfo.Split(':')[0]);
                    opt.Password = Uri.UnescapeDataString(rmqUri.UserInfo.Split(':')[1]);
                });
                services.AddHostedService<AuditOutboxDispatcher>();
                services.AddLogging();
            })
            .Build();
        await _dispatcherHost.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _consumerHost.StopAsync();
        await _dispatcherHost.StopAsync();
        _consumerHost.Dispose();
        _dispatcherHost.Dispose();
        await _publishChannel.CloseAsync();
        await _publishConnection.CloseAsync();
        await _postgres.DisposeAsync();
        await _rabbitmq.DisposeAsync();
    }

    private static AuditEventContract BuildContract(
        string scope, string action, int actorId = 1, int? targetId = null) =>
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

    private async Task PublishContractAsync(AuditEventContract contract)
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(contract));
        await _publishChannel.BasicPublishAsync(
            exchange: AuditRouting.ExchangeName,
            routingKey: $"audit.{contract.AuditScope}",
            body: body);
    }

    private async Task<bool> WaitForConditionAsync(Func<Task<bool>> condition, int timeoutSeconds = 8)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition()) return true;
            await Task.Delay(300);
        }
        return false;
    }

    [Fact]
    public async Task EntityScopeMessage_PersistsToEntityAuditLogWithCorrectFields()
    {
        var contract = BuildContract(AuditRouting.ScopeEntity, "entity_created", actorId: 5, targetId: 10);
        await PublishContractAsync(contract);

        var found = await WaitForConditionAsync(async () =>
        {
            await using var db = new AuditDbContext(_auditDbOptions);
            return await db.EntityAuditLogs.AsNoTracking().AnyAsync(x => x.Id == contract.EventId);
        });

        found.Should().BeTrue();
        await using var verify = new AuditDbContext(_auditDbOptions);
        var row = await verify.EntityAuditLogs.AsNoTracking().SingleAsync(x => x.Id == contract.EventId);
        row.Action.Should().Be("entity_created");
        row.ChangedById.Should().Be(5);
        row.EntityId.Should().Be(10);
    }

    [Fact]
    public async Task WorkspaceScopeMessage_PersistsToWorkspaceAuditLogWithCorrectFields()
    {
        var contract = BuildContract(AuditRouting.ScopeWorkspace, "workspace_created", actorId: 2, targetId: 7);
        await PublishContractAsync(contract);

        var found = await WaitForConditionAsync(async () =>
        {
            await using var db = new AuditDbContext(_auditDbOptions);
            return await db.WorkspaceAuditLogs.AsNoTracking().AnyAsync(x => x.Id == contract.EventId);
        });

        found.Should().BeTrue();
        await using var verify = new AuditDbContext(_auditDbOptions);
        var row = await verify.WorkspaceAuditLogs.AsNoTracking().SingleAsync(x => x.Id == contract.EventId);
        row.Action.Should().Be("workspace_created");
        row.WorkspaceId.Should().Be(7);
        row.ChangedById.Should().Be(2);
    }

    [Fact]
    public async Task OrganizationScopeMessage_PersistsToOrganizationAuditLog()
    {
        var contract = BuildContract(AuditRouting.ScopeOrganization, "organization_created", actorId: 3, targetId: 4);
        await PublishContractAsync(contract);

        var found = await WaitForConditionAsync(async () =>
        {
            await using var db = new AuditDbContext(_auditDbOptions);
            return await db.OrganizationAuditLogs.AsNoTracking().AnyAsync(x => x.Id == contract.EventId);
        });

        found.Should().BeTrue();
        await using var verify = new AuditDbContext(_auditDbOptions);
        var row = await verify.OrganizationAuditLogs.AsNoTracking().SingleAsync(x => x.Id == contract.EventId);
        row.OrganizationId.Should().Be(4);
    }

    [Fact]
    public async Task UserScopeMessage_PersistsToUserAuditLog()
    {
        var contract = BuildContract(AuditRouting.ScopeUser, "user_registered", actorId: 99, targetId: 99);
        await PublishContractAsync(contract);

        var found = await WaitForConditionAsync(async () =>
        {
            await using var db = new AuditDbContext(_auditDbOptions);
            return await db.UserAuditLogs.AsNoTracking().AnyAsync(x => x.Id == contract.EventId);
        });

        found.Should().BeTrue();
        await using var verify = new AuditDbContext(_auditDbOptions);
        var row = await verify.UserAuditLogs.AsNoTracking().SingleAsync(x => x.Id == contract.EventId);
        row.TargetUserId.Should().Be(99);
    }

    [Fact]
    public async Task DuplicateEventId_SecondMessageIgnoredByIdempotencyCheck()
    {
        var contract = BuildContract(AuditRouting.ScopeEntity, "entity_updated", actorId: 1, targetId: 5);
        await PublishContractAsync(contract);
        await WaitForConditionAsync(async () =>
        {
            await using var db = new AuditDbContext(_auditDbOptions);
            return await db.EntityAuditLogs.AsNoTracking().AnyAsync(x => x.Id == contract.EventId);
        });

        await PublishContractAsync(contract);

        var sentinel = BuildContract(AuditRouting.ScopeUser, "idempotency_sentinel");
        await PublishContractAsync(sentinel);
        await WaitForConditionAsync(async () =>
        {
            await using var db = new AuditDbContext(_auditDbOptions);
            return await db.UserAuditLogs.AsNoTracking().AnyAsync(x => x.Id == sentinel.EventId);
        });

        await using var verify = new AuditDbContext(_auditDbOptions);
        var count = await verify.EntityAuditLogs.AsNoTracking().CountAsync(x => x.Id == contract.EventId);
        count.Should().Be(1);
    }

    [Fact]
    public async Task NullDeserializedPayload_ConsumerAcksAndRemainsOperational()
    {
        var nullBody = Encoding.UTF8.GetBytes("null");
        await _publishChannel.BasicPublishAsync(AuditRouting.ExchangeName, "audit.entity", body: nullBody);

        var sentinel = BuildContract(AuditRouting.ScopeUser, "post_null_sentinel");
        await PublishContractAsync(sentinel);
        var recovered = await WaitForConditionAsync(async () =>
        {
            await using var db = new AuditDbContext(_auditDbOptions);
            return await db.UserAuditLogs.AsNoTracking().AnyAsync(x => x.Id == sentinel.EventId);
        });

        recovered.Should().BeTrue();
    }

    [Fact]
    public async Task UnknownScope_MessageConsumedWithNoAuditRowOrProcessedEvent()
    {
        var contract = BuildContract("unknown_scope", "some_action", targetId: 1);
        await PublishContractAsync(contract);

        var sentinel = BuildContract(AuditRouting.ScopeUser, "unknown_scope_sentinel");
        await PublishContractAsync(sentinel);
        await WaitForConditionAsync(async () =>
        {
            await using var db = new AuditDbContext(_auditDbOptions);
            return await db.UserAuditLogs.AsNoTracking().AnyAsync(x => x.Id == sentinel.EventId);
        });

        await using var verify = new AuditDbContext(_auditDbOptions);
        var processedExists = await verify.AuditProcessedEvents.AsNoTracking()
            .AnyAsync(x => x.EventId == contract.EventId);
        processedExists.Should().BeFalse();
    }

    [Fact]
    public async Task FullPipeline_OutboxWriterThroughDispatcherAndConsumer_PersistsToAuditLog()
    {
        await using var coreDb = new RelativaDbContext(_coreDbOptions);
        var writer = new OutboxWriter(coreDb);
        var contract = BuildContract(AuditRouting.ScopeWorkspace, "workspace_updated", actorId: 3, targetId: 12);

        await writer.EnqueueAuditAsync(contract);

        var found = await WaitForConditionAsync(async () =>
        {
            await using var db = new AuditDbContext(_auditDbOptions);
            return await db.WorkspaceAuditLogs.AsNoTracking().AnyAsync(x => x.Id == contract.EventId);
        }, timeoutSeconds: 12);

        found.Should().BeTrue();
        await using var verify = new AuditDbContext(_auditDbOptions);
        var row = await verify.WorkspaceAuditLogs.AsNoTracking().SingleAsync(x => x.Id == contract.EventId);
        row.Action.Should().Be("workspace_updated");
        row.ChangedById.Should().Be(3);
        row.WorkspaceId.Should().Be(12);
    }

    [Fact]
    public async Task FullPipeline_DispatcherSetsPublishedAtUtcAndIncrementsAttempts()
    {
        await using var coreDb = new RelativaDbContext(_coreDbOptions);
        var writer = new OutboxWriter(coreDb);
        var contract = BuildContract(AuditRouting.ScopeEntity, "entity_archived", actorId: 8, targetId: 20);

        await writer.EnqueueAuditAsync(contract);

        var published = await WaitForConditionAsync(async () =>
        {
            await using var db = new RelativaDbContext(_coreDbOptions);
            var msg = await db.AuditOutboxMessages.AsNoTracking()
                .FirstOrDefaultAsync(m => m.EventId == contract.EventId);
            return msg?.PublishedAtUtc is not null;
        }, timeoutSeconds: 12);

        published.Should().BeTrue();
        await using var verify = new RelativaDbContext(_coreDbOptions);
        var message = await verify.AuditOutboxMessages.AsNoTracking()
            .SingleAsync(m => m.EventId == contract.EventId);
        message.PublishedAtUtc.Should().NotBeNull();
        message.PublishAttempts.Should().Be(1);
        message.LastError.Should().BeNull();
    }
}
