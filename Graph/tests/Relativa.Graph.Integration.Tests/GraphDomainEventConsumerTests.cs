using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using RabbitMQ.Client;
using Relativa.Graph.Data;
using Relativa.Graph.Hubs;
using Relativa.Graph.Messaging;
using Relativa.Persistence.Contracts;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace Relativa.Graph.Integration.Tests;

public sealed class GraphDomainEventConsumerTests : IAsyncLifetime
{
    private const string ConsumerGroup = "graph.domain.workspace.v1";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").WithDatabase("graph_consumer_test")
        .WithUsername("relativa").WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432)).Build();

    private readonly RabbitMqContainer _rabbitmq = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-alpine").Build();

    private DbContextOptions<GraphDbContext> _dbOptions = null!;
    private IHost _host = null!;
    private IConnection _publishConnection = null!;
    private IChannel _publishChannel = null!;
    private IClientProxy _allClients = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitmq.StartAsync());

        _dbOptions = new DbContextOptionsBuilder<GraphDbContext>()
            .UseNpgsql(_postgres.GetConnectionString()).Options;
        await using (var db = new GraphDbContext(_dbOptions))
        {
            await db.Database.EnsureCreatedAsync();
        }

        var rmqUri = new Uri(_rabbitmq.GetConnectionString());
        _publishConnection = await new ConnectionFactory { Uri = rmqUri }.CreateConnectionAsync();
        _publishChannel = await _publishConnection.CreateChannelAsync();
        await _publishChannel.ExchangeDeclareAsync(DomainRouting.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
        await _publishChannel.ExchangeDeclareAsync("relativa.consumer.graph.workspace.v1.dlx", ExchangeType.Fanout, durable: true, autoDelete: false);
        await _publishChannel.QueueDeclareAsync(
            "domain.events.graph.workspace.v1", durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = "relativa.consumer.graph.workspace.v1.dlx" });
        await _publishChannel.QueueBindAsync("domain.events.graph.workspace.v1", DomainRouting.ExchangeName, "core.workspace.*");

        _allClients = Substitute.For<IClientProxy>();
        var groupClients = Substitute.For<IClientProxy>();
        var hubClients = Substitute.For<IHubClients>();
        hubClients.All.Returns(_allClients);
        hubClients.Group(Arg.Any<string>()).Returns(groupClients);
        var hubContext = Substitute.For<IHubContext<GraphHub>>();
        hubContext.Clients.Returns(hubClients);

        _host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddDbContext<GraphDbContext>(opt => opt.UseNpgsql(_postgres.GetConnectionString()));
                services.AddSingleton(hubContext);
                services.Configure<RabbitMqGraphConsumerOptions>(opt =>
                {
                    opt.Host = rmqUri.Host;
                    opt.Port = rmqUri.Port;
                    opt.Username = Uri.UnescapeDataString(rmqUri.UserInfo.Split(':')[0]);
                    opt.Password = Uri.UnescapeDataString(rmqUri.UserInfo.Split(':')[1]);
                });
                services.AddHostedService<DomainEventConsumerHostedService>();
                services.AddLogging();
            })
            .Build();
        await _host.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
        await _publishChannel.CloseAsync();
        await _publishConnection.CloseAsync();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _rabbitmq.DisposeAsync().AsTask());
    }

    private static DomainMessageEnvelope WorkspaceEnvelope(Guid messageId, int workspaceId = 5) =>
        new(
            SchemaVersion: 1,
            MessageId: messageId,
            CorrelationId: Guid.NewGuid(),
            SagaInstanceId: Guid.NewGuid(),
            CausationId: null,
            OccurredAtUtc: DateTimeOffset.UtcNow,
            SourceService: "core",
            PayloadTypeName: DomainPayloadTypes.WorkspaceLifecycleV1,
            PayloadJson: JsonSerializer.Serialize(new WorkspaceLifecyclePayloadV1("created", workspaceId, 1, 7, "WS")));

    private async Task PublishAsync(DomainMessageEnvelope envelope)
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope));
        await _publishChannel.BasicPublishAsync(
            exchange: DomainRouting.ExchangeName,
            routingKey: DomainRouting.RoutingKeyCoreWorkspace(DomainRouting.CoreWorkspaceVerbCreated),
            body: body);
    }

    private async Task PublishRawAsync(string raw)
    {
        await _publishChannel.BasicPublishAsync(
            exchange: DomainRouting.ExchangeName,
            routingKey: DomainRouting.RoutingKeyCoreWorkspace(DomainRouting.CoreWorkspaceVerbCreated),
            body: Encoding.UTF8.GetBytes(raw));
    }

    private async Task<bool> WaitForDeliveryAsync(Guid messageId, int timeoutSeconds = 12)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var db = new GraphDbContext(_dbOptions);
            if (await db.RabbitMqProcessedDeliveries.AsNoTracking()
                    .AnyAsync(d => d.MessageId == messageId && d.ConsumerGroup == ConsumerGroup))
                return true;
            await Task.Delay(300);
        }
        return false;
    }

    [Fact]
    public async Task WorkspaceLifecycle_ForwardsToSignalRAndRecordsProcessedDelivery()
    {
        var envelope = WorkspaceEnvelope(Guid.NewGuid());

        await PublishAsync(envelope);

        (await WaitForDeliveryAsync(envelope.MessageId)).Should().BeTrue(
            "the consumer must record an idempotency row for the processed choreography message");
        await _allClients.Received().SendCoreAsync(
            GraphSignalREvents.WorkspaceLifecycleDomain, Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DuplicateMessageId_IsProcessedExactlyOnce()
    {
        var envelope = WorkspaceEnvelope(Guid.NewGuid());

        await PublishAsync(envelope);
        (await WaitForDeliveryAsync(envelope.MessageId)).Should().BeTrue();
        await PublishAsync(envelope);

        var sentinel = WorkspaceEnvelope(Guid.NewGuid());
        await PublishAsync(sentinel);
        (await WaitForDeliveryAsync(sentinel.MessageId)).Should().BeTrue();

        await using var db = new GraphDbContext(_dbOptions);
        var count = await db.RabbitMqProcessedDeliveries.AsNoTracking()
            .CountAsync(d => d.MessageId == envelope.MessageId);
        count.Should().Be(1, "a duplicate delivery of the same MessageId must be deduplicated, not reprocessed");
    }

    [Fact]
    public async Task MalformedEnvelope_IsRejectedAndConsumerStaysOperational()
    {
        await PublishRawAsync("{ this is not valid json");

        var sentinel = WorkspaceEnvelope(Guid.NewGuid());
        await PublishAsync(sentinel);

        (await WaitForDeliveryAsync(sentinel.MessageId)).Should().BeTrue(
            "a poison message must be dead-lettered without stalling the consumer for subsequent valid messages");
    }

    [Fact]
    public async Task NullEnvelope_IsAcknowledgedAndConsumerStaysOperational()
    {
        await PublishRawAsync("null");

        var sentinel = WorkspaceEnvelope(Guid.NewGuid());
        await PublishAsync(sentinel);

        (await WaitForDeliveryAsync(sentinel.MessageId)).Should().BeTrue(
            "a literal null envelope is acknowledged and skipped without stalling subsequent valid messages");
    }
}
