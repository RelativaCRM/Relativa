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

public sealed class GraphMlConsumerTests : IAsyncLifetime
{
    private const string MlConsumerGroup = "graph.domain.ml.v1";
    private const int WorkspaceId = 5;

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").WithDatabase("graph_ml_consumer_test")
        .WithUsername("relativa").WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432)).Build();

    private readonly RabbitMqContainer _rabbitmq = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-alpine").Build();

    private DbContextOptions<GraphDbContext> _dbOptions = null!;
    private IHost _host = null!;
    private IConnection _publishConnection = null!;
    private IChannel _publishChannel = null!;
    private IClientProxy _workspaceGroup = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitmq.StartAsync());
        _dbOptions = new DbContextOptionsBuilder<GraphDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        await using (var db = new GraphDbContext(_dbOptions)) { await db.Database.EnsureCreatedAsync(); }

        var rmqUri = new Uri(_rabbitmq.GetConnectionString());
        _publishConnection = await new ConnectionFactory { Uri = rmqUri }.CreateConnectionAsync();
        _publishChannel = await _publishConnection.CreateChannelAsync();
        await _publishChannel.ExchangeDeclareAsync(DomainRouting.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
        await _publishChannel.ExchangeDeclareAsync("relativa.consumer.graph.ml.v1.dlx", ExchangeType.Fanout, durable: true, autoDelete: false);
        await _publishChannel.QueueDeclareAsync("domain.events.graph.ml.v1", durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = "relativa.consumer.graph.ml.v1.dlx" });
        await _publishChannel.QueueBindAsync("domain.events.graph.ml.v1", DomainRouting.ExchangeName, "ml.recalculate.*");

        _workspaceGroup = Substitute.For<IClientProxy>();
        var hubClients = Substitute.For<IHubClients>();
        hubClients.All.Returns(Substitute.For<IClientProxy>());
        hubClients.Group($"workspace-{WorkspaceId}").Returns(_workspaceGroup);
        var hubContext = Substitute.For<IHubContext<GraphHub>>();
        hubContext.Clients.Returns(hubClients);

        _host = new HostBuilder().ConfigureServices(services =>
        {
            services.AddDbContext<GraphDbContext>(opt => opt.UseNpgsql(_postgres.GetConnectionString()));
            services.AddSingleton(hubContext);
            services.Configure<RabbitMqGraphConsumerOptions>(opt =>
            {
                opt.Host = rmqUri.Host; opt.Port = rmqUri.Port;
                opt.Username = Uri.UnescapeDataString(rmqUri.UserInfo.Split(':')[0]);
                opt.Password = Uri.UnescapeDataString(rmqUri.UserInfo.Split(':')[1]);
            });
            services.AddHostedService<DomainEventConsumerHostedService>();
            services.AddLogging();
        }).Build();
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

    private async Task PublishAsync(Guid messageId, string verb, string payloadType, object payload)
    {
        var envelope = new DomainMessageEnvelope(
            1, messageId, Guid.NewGuid(), null, null, DateTimeOffset.UtcNow, "ml", payloadType,
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        await _publishChannel.BasicPublishAsync(
            exchange: DomainRouting.ExchangeName,
            routingKey: DomainRouting.RoutingKeyMlRecalculate(verb),
            body: Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope)));
    }

    private async Task<bool> WaitForDeliveryAsync(Guid messageId, int timeoutSeconds = 12)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var db = new GraphDbContext(_dbOptions);
            if (await db.RabbitMqProcessedDeliveries.AsNoTracking().AnyAsync(d => d.MessageId == messageId && d.ConsumerGroup == MlConsumerGroup))
                return true;
            await Task.Delay(300);
        }
        return false;
    }

    [Fact]
    public async Task MlRecalculateProgress_ForwardsToWorkspaceGroup()
    {
        var id = Guid.NewGuid();
        var payload = new MlRecalculateProgressPayloadV1(Guid.NewGuid(), WorkspaceId, "running", 3, 10, DateTimeOffset.UtcNow, null);

        await PublishAsync(id, DomainRouting.MlRecalculateVerbProgress, DomainPayloadTypes.MlRecalculateProgressV1, payload);

        (await WaitForDeliveryAsync(id)).Should().BeTrue();
        await _workspaceGroup.Received().SendCoreAsync(
            GraphSignalREvents.MlRecalculateProgress, Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MlRecalculateCompleted_ForwardsToWorkspaceGroup()
    {
        var id = Guid.NewGuid();
        var payload = new MlRecalculateCompletedPayloadV1(Guid.NewGuid(), WorkspaceId, "completed", 10, 9, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);

        await PublishAsync(id, DomainRouting.MlRecalculateVerbCompleted, DomainPayloadTypes.MlRecalculateCompletedV1, payload);

        (await WaitForDeliveryAsync(id)).Should().BeTrue();
        await _workspaceGroup.Received().SendCoreAsync(
            GraphSignalREvents.MlRecalculateCompleted, Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MlRecalculateProgress_NullWorkspaceId_AcknowledgedWithoutGroupBroadcast()
    {
        var id = Guid.NewGuid();
        var payload = new MlRecalculateProgressPayloadV1(Guid.NewGuid(), null, "running", 3, 10, DateTimeOffset.UtcNow, null);

        await PublishAsync(id, DomainRouting.MlRecalculateVerbProgress, DomainPayloadTypes.MlRecalculateProgressV1, payload);

        (await WaitForDeliveryAsync(id)).Should().BeTrue("the message is still recorded as processed even without a workspace target");
        await _workspaceGroup.DidNotReceive().SendCoreAsync(
            GraphSignalREvents.MlRecalculateProgress, Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MlRecalculateCompleted_NullWorkspaceId_AcknowledgedWithoutGroupBroadcast()
    {
        var id = Guid.NewGuid();
        var payload = new MlRecalculateCompletedPayloadV1(Guid.NewGuid(), null, "completed", 10, 9, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);

        await PublishAsync(id, DomainRouting.MlRecalculateVerbCompleted, DomainPayloadTypes.MlRecalculateCompletedV1, payload);

        (await WaitForDeliveryAsync(id)).Should().BeTrue();
        await _workspaceGroup.DidNotReceive().SendCoreAsync(
            GraphSignalREvents.MlRecalculateCompleted, Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MlMessage_IsDeduplicatedByMessageId()
    {
        var id = Guid.NewGuid();
        var payload = new MlRecalculateProgressPayloadV1(Guid.NewGuid(), WorkspaceId, "running", 1, 10, DateTimeOffset.UtcNow, null);

        await PublishAsync(id, DomainRouting.MlRecalculateVerbProgress, DomainPayloadTypes.MlRecalculateProgressV1, payload);
        (await WaitForDeliveryAsync(id)).Should().BeTrue();
        await PublishAsync(id, DomainRouting.MlRecalculateVerbProgress, DomainPayloadTypes.MlRecalculateProgressV1, payload);

        var sentinel = Guid.NewGuid();
        await PublishAsync(sentinel, DomainRouting.MlRecalculateVerbCompleted, DomainPayloadTypes.MlRecalculateCompletedV1,
            new MlRecalculateCompletedPayloadV1(Guid.NewGuid(), WorkspaceId, "completed", 1, 1, 0, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null));
        (await WaitForDeliveryAsync(sentinel)).Should().BeTrue();

        await using var db = new GraphDbContext(_dbOptions);
        (await db.RabbitMqProcessedDeliveries.AsNoTracking().CountAsync(d => d.MessageId == id)).Should().Be(1);
    }
}
