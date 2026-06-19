using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Relativa.Graph.ML;
using Relativa.Graph.Messaging;
using Relativa.Persistence.Contracts;
using Testcontainers.RabbitMq;
using Xunit;

namespace Relativa.Graph.Integration.Tests;

public sealed class MlScoringClientRpcTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly RabbitMqContainer _rabbitmq = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-alpine").Build();

    private IConnection _connection = null!;
    private IChannel _channel = null!;
    private RabbitMqMlScoringClient _client = null!;
    private Func<MlScoreRpcRequestV1, MlScoreRpcReplyV1?> _replyFactory = _ => new MlScoreRpcReplyV1([], null);

    public async Task InitializeAsync()
    {
        await _rabbitmq.StartAsync();
        var rmqUri = new Uri(_rabbitmq.GetConnectionString());
        _connection = await new ConnectionFactory { Uri = rmqUri }.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        await _channel.ExchangeDeclareAsync(MlScoringRouting.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
        await _channel.QueueDeclareAsync(MlScoringRouting.CommandQueueName, durable: true, exclusive: false, autoDelete: false);
        await _channel.QueueBindAsync(MlScoringRouting.CommandQueueName, MlScoringRouting.ExchangeName, MlScoringRouting.CommandRoutingKey);

        var responder = new AsyncEventingBasicConsumer(_channel);
        responder.ReceivedAsync += async (_, ea) =>
        {
            var request = JsonSerializer.Deserialize<MlScoreRpcRequestV1>(Encoding.UTF8.GetString(ea.Body.ToArray()), Json)!;
            var reply = _replyFactory(request);
            var replyProps = new BasicProperties { CorrelationId = ea.BasicProperties.CorrelationId };
            await _channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: ea.BasicProperties.ReplyTo!,
                mandatory: false,
                basicProperties: replyProps,
                body: Encoding.UTF8.GetBytes(JsonSerializer.Serialize(reply, Json)));
        };
        await _channel.BasicConsumeAsync(MlScoringRouting.CommandQueueName, autoAck: true, consumer: responder);

        var options = Options.Create(new RabbitMqGraphConsumerOptions
        {
            Host = rmqUri.Host,
            Port = rmqUri.Port,
            Username = Uri.UnescapeDataString(rmqUri.UserInfo.Split(':')[0]),
            Password = Uri.UnescapeDataString(rmqUri.UserInfo.Split(':')[1]),
        });
        _client = new RabbitMqMlScoringClient(options, Substitute.For<ILogger<RabbitMqMlScoringClient>>());
    }

    public async Task DisposeAsync()
    {
        await _channel.CloseAsync();
        await _connection.CloseAsync();
        await _rabbitmq.DisposeAsync();
    }

    [Fact]
    public async Task ScoreBatchAsync_ResponderReturnsScores_MapsThemByEntityId()
    {
        _replyFactory = req => new MlScoreRpcReplyV1(
            req.EntityIds.Select(id => new MlScoreRpcItemV1(id, ClosureScore: 0.5 + id / 100.0, ChurnScore: 0.1, UnavailableReason: null)).ToList(),
            ErrorMessage: null);

        var result = await _client.ScoreBatchAsync([10, 20]);

        result.Should().HaveCount(2);
        result[10].ClosureScore.Should().Be(0.6);
        result[20].ClosureScore.Should().Be(0.7);
    }

    [Fact]
    public async Task ScoreBatchAsync_ResponderReturnsError_DegradesToEmpty()
    {
        _replyFactory = _ => new MlScoreRpcReplyV1([], ErrorMessage: "scoring model unavailable");

        var result = await _client.ScoreBatchAsync([10, 20]);

        result.Should().BeEmpty("an error reply from the ML service must degrade to no highlights, not propagate");
    }

    [Fact]
    public async Task ScoreBatchAsync_EmptyInput_ShortCircuitsWithoutBrokerRoundTrip()
    {
        var result = await _client.ScoreBatchAsync([]);

        result.Should().BeEmpty("an empty batch requires no scoring call at all");
    }

    [Fact]
    public async Task ScoreBatchAsync_NullReplyBody_DegradesToEmpty()
    {
        _replyFactory = _ => null;

        var result = await _client.ScoreBatchAsync([10, 20]);

        result.Should().BeEmpty("a null reply payload must degrade to no highlights, not throw");
    }
}
