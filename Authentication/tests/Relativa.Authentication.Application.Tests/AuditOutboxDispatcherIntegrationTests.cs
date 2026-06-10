using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Relativa.Authentication.Infrastructure.Data;
using Relativa.Authentication.Infrastructure.Services.Audit;
using Relativa.Messaging;
using Relativa.Persistence.Contracts;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace Relativa.Authentication.Application.Tests;

public sealed class AuditOutboxDispatcherIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").WithDatabase("auth_dispatch_test")
        .WithUsername("relativa").WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432)).Build();

    private readonly RabbitMqContainer _rabbitmq = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-alpine").Build();

    private DbContextOptions<AuthDbContext> _dbOptions = null!;
    private IHost _dispatcherHost = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitmq.StartAsync());

        _dbOptions = new DbContextOptionsBuilder<AuthDbContext>()
            .UseNpgsql(_postgres.GetConnectionString()).Options;
        await using (var db = new AuthDbContext(_dbOptions))
        {
            await db.Database.EnsureCreatedAsync();
        }

        var rmqUri = new Uri(_rabbitmq.GetConnectionString());
        _dispatcherHost = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddDbContext<AuthDbContext>(opt => opt.UseNpgsql(_postgres.GetConnectionString()));
                services.Configure<RabbitMqPublishingOptions>(opt =>
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
        await _dispatcherHost.StopAsync();
        _dispatcherHost.Dispose();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _rabbitmq.DisposeAsync().AsTask());
    }

    private static AuditEventContract Event() => new(
        EventId: Guid.NewGuid(), SchemaVersion: 1, OccurredAtUtc: DateTimeOffset.UtcNow,
        SourceService: "auth", ActorUserId: 7, AuditScope: AuditRouting.ScopeUser,
        TargetId: 7, Action: "user_logged_in", FieldName: null, EntityType: null,
        OldValueJson: null, NewValueJson: null);

    private async Task<bool> WaitForAsync(Func<Task<bool>> condition, int timeoutSeconds = 14)
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
    public async Task PendingOutboxMessage_IsPublishedAndMarkedWithAttempt()
    {
        var evt = Event();
        await using (var db = new AuthDbContext(_dbOptions))
        {
            await new OutboxWriter(db).EnqueueAuditAsync(evt);
        }

        var published = await WaitForAsync(async () =>
        {
            await using var db = new AuthDbContext(_dbOptions);
            var msg = await db.AuditOutboxMessages.AsNoTracking().FirstOrDefaultAsync(m => m.EventId == evt.EventId);
            return msg?.PublishedAtUtc is not null;
        });

        published.Should().BeTrue("the dispatcher must publish pending outbox rows to RabbitMQ");
        await using var verify = new AuthDbContext(_dbOptions);
        var message = await verify.AuditOutboxMessages.AsNoTracking().SingleAsync(m => m.EventId == evt.EventId);
        message.PublishedAtUtc.Should().NotBeNull();
        message.PublishAttempts.Should().Be(1, "a successful publish counts as exactly one attempt");
        message.LastError.Should().BeNull("a successful publish records no error");
    }

    [Fact]
    public async Task AlreadyPublishedMessage_IsNotRepublished()
    {
        var evt = Event();
        await using (var db = new AuthDbContext(_dbOptions))
        {
            await new OutboxWriter(db).EnqueueAuditAsync(evt);
        }

        await WaitForAsync(async () =>
        {
            await using var db = new AuthDbContext(_dbOptions);
            var msg = await db.AuditOutboxMessages.AsNoTracking().FirstOrDefaultAsync(m => m.EventId == evt.EventId);
            return msg?.PublishedAtUtc is not null;
        });

        var firstPublishedAt = await ReadPublishedAtAsync(evt.EventId);
        await Task.Delay(4000);
        var secondPublishedAt = await ReadPublishedAtAsync(evt.EventId);

        secondPublishedAt.Should().Be(firstPublishedAt,
            "the dispatcher only selects rows where PublishedAtUtc is null, so a published row is never touched again");
    }

    private async Task<DateTimeOffset?> ReadPublishedAtAsync(Guid eventId)
    {
        await using var db = new AuthDbContext(_dbOptions);
        var msg = await db.AuditOutboxMessages.AsNoTracking().SingleAsync(m => m.EventId == eventId);
        return msg.PublishedAtUtc;
    }
}
