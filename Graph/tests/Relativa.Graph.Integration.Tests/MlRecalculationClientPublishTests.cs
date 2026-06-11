using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using RabbitMQ.Client;
using Relativa.Graph.ML;
using Relativa.Graph.Messaging;
using Relativa.Persistence.Contracts;
using Testcontainers.RabbitMq;
using Xunit;

namespace Relativa.Graph.Integration.Tests;

public sealed class MlRecalculationClientPublishTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private const string Queue = "ml.recalculate.test.capture";

    private readonly RabbitMqContainer _rabbitmq = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-alpine").Build();

    private IConnection _connection = null!;
    private IChannel _channel = null!;
    private RabbitMqMlRecalculationClient _client = null!;

    public async Task InitializeAsync()
    {
        await _rabbitmq.StartAsync();
        var rmqUri = new Uri(_rabbitmq.GetConnectionString());

        _connection = await new ConnectionFactory { Uri = rmqUri }.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();
        await _channel.ExchangeDeclareAsync(DomainRouting.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
        await _channel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false);
        await _channel.QueueBindAsync(Queue, DomainRouting.ExchangeName, "ml.recalculate.*");

        var options = Options.Create(new RabbitMqGraphConsumerOptions
        {
            Host = rmqUri.Host,
            Port = rmqUri.Port,
            Username = Uri.UnescapeDataString(rmqUri.UserInfo.Split(':')[0]),
            Password = Uri.UnescapeDataString(rmqUri.UserInfo.Split(':')[1]),
        });
        _client = new RabbitMqMlRecalculationClient(options, Substitute.For<ILogger<RabbitMqMlRecalculationClient>>());
    }

    public async Task DisposeAsync()
    {
        await _channel.CloseAsync();
        await _connection.CloseAsync();
        await _rabbitmq.DisposeAsync();
    }

    [Fact]
    public async Task EnqueueAsync_PublishesRecalculateEnvelopeWithEntityIdsAndRoutingKey()
    {
        await _client.EnqueueAsync([10, 20], requestedByUserId: 7, workspaceId: 3);

        BasicGetResult? got = null;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (got is null && DateTimeOffset.UtcNow < deadline)
        {
            got = await _channel.BasicGetAsync(Queue, autoAck: true);
            if (got is null) await Task.Delay(200);
        }

        got.Should().NotBeNull("the recalculation client must publish a message routed to ml.recalculate.*");
        got!.RoutingKey.Should().Be("ml.recalculate.enqueued");

        var envelope = JsonSerializer.Deserialize<DomainMessageEnvelope>(Encoding.UTF8.GetString(got.Body.ToArray()), Json);
        envelope!.PayloadTypeName.Should().Be(DomainPayloadTypes.MlRecalculateEnqueuedV1);

        var payload = JsonSerializer.Deserialize<MlRecalculateEnqueuedPayloadV1>(envelope.PayloadJson, Json);
        payload!.EntityIds.Should().Equal(10, 20);
        payload.RequestedByUserId.Should().Be(7);
        payload.WorkspaceId.Should().Be(3);
    }
}
