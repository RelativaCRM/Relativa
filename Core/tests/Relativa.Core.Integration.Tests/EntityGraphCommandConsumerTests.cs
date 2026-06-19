using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Relativa.Core.Application.DTOs.Entity;
using Relativa.Core.Application.Exceptions;
using Relativa.Core.Application.Interfaces;
using Relativa.Core.Infrastructure.Messaging;
using Relativa.Persistence.Contracts;
using Testcontainers.RabbitMq;
using Xunit;

namespace Relativa.Core.Integration.Tests;

public sealed class EntityGraphCommandConsumerTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private const string ReplyQueue = "rpc.reply.coretest";

    private readonly RabbitMqContainer _rabbitmq = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-alpine").Build();

    private readonly Mock<IEntityService> _entityService = new();
    private IHost _host = null!;
    private IConnection _connection = null!;
    private IChannel _channel = null!;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _replies = new();

    public async Task InitializeAsync()
    {
        await _rabbitmq.StartAsync();
        var rmqUri = new Uri(_rabbitmq.GetConnectionString());

        _connection = await new ConnectionFactory { Uri = rmqUri }.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();
        await _channel.ExchangeDeclareAsync(EntityGraphRouting.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
        await _channel.QueueDeclareAsync(EntityGraphRouting.CommandQueueName, durable: true, exclusive: false, autoDelete: false);
        await _channel.QueueBindAsync(EntityGraphRouting.CommandQueueName, EntityGraphRouting.ExchangeName, EntityGraphRouting.CommandRoutingKey);
        await _channel.QueueDeclareAsync(ReplyQueue, durable: false, exclusive: false, autoDelete: true);

        var replyConsumer = new AsyncEventingBasicConsumer(_channel);
        replyConsumer.ReceivedAsync += (_, ea) =>
        {
            var correlationId = ea.BasicProperties.CorrelationId ?? "";
            if (_replies.TryGetValue(correlationId, out var tcs))
                tcs.TrySetResult(Encoding.UTF8.GetString(ea.Body.ToArray()));
            return Task.CompletedTask;
        };
        await _channel.BasicConsumeAsync(ReplyQueue, autoAck: true, consumer: replyConsumer);

        _host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(_entityService.Object);
                services.Configure<EntityGraphRabbitOptions>(opt =>
                {
                    opt.Host = rmqUri.Host;
                    opt.Port = rmqUri.Port;
                    opt.Username = Uri.UnescapeDataString(rmqUri.UserInfo.Split(':')[0]);
                    opt.Password = Uri.UnescapeDataString(rmqUri.UserInfo.Split(':')[1]);
                });
                services.AddHostedService<EntityGraphCommandConsumerHostedService>();
                services.AddLogging();
            })
            .Build();
        await _host.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
        await _channel.CloseAsync();
        await _connection.CloseAsync();
        await _rabbitmq.DisposeAsync();
    }

    private async Task<EntityGraphCreateRpcReplyV1> SendRpcAsync(string commandBody, int timeoutSeconds = 12)
    {
        var correlationId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _replies[correlationId] = tcs;

        var props = new BasicProperties { CorrelationId = correlationId, ReplyTo = ReplyQueue };
        await _channel.BasicPublishAsync(
            exchange: EntityGraphRouting.ExchangeName,
            routingKey: EntityGraphRouting.CommandRoutingKey,
            mandatory: false,
            basicProperties: props,
            body: Encoding.UTF8.GetBytes(commandBody));

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));
        completed.Should().Be(tcs.Task, "the RPC consumer must reply within the timeout");
        return JsonSerializer.Deserialize<EntityGraphCreateRpcReplyV1>(await tcs.Task, Json)!;
    }

    private static string Command(int workspaceId, int userId, string createEntityJson) =>
        JsonSerializer.Serialize(new EntityGraphCreateRpcV1(workspaceId, userId, createEntityJson), Json);

    [Fact]
    public async Task ValidCreate_ServiceSucceeds_RepliesSuccessWithEntityDetailJson()
    {
        _entityService
            .Setup(s => s.CreateAsync(7, 3, It.IsAny<CreateEntityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntityDetailDto(1, 2, "deal", "Deal", false, [], [], []));
        var createJson = JsonSerializer.Serialize(new CreateEntityRequest(2, []), Json);

        var reply = await SendRpcAsync(Command(7, 3, createJson));

        reply.Success.Should().BeTrue();
        reply.EntityDetailJson.Should().NotBeNullOrEmpty();
        reply.ErrorMessage.Should().BeNull();
        _entityService.Verify(s => s.CreateAsync(7, 3, It.IsAny<CreateEntityRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ServiceThrows_RepliesFailureWithExceptionMessage()
    {
        _entityService
            .Setup(s => s.CreateAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CreateEntityRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AppException("permission_denied", 403, "You do not have the 'create_entities' permission."));
        var createJson = JsonSerializer.Serialize(new CreateEntityRequest(2, []), Json);

        var reply = await SendRpcAsync(Command(7, 3, createJson));

        reply.Success.Should().BeFalse("a failure in the entity service must be surfaced as an RPC failure reply, not a crash");
        reply.ErrorMessage.Should().Contain("create_entities");
        reply.EntityDetailJson.Should().BeNull();
    }

    [Fact]
    public async Task InvalidCreateEntityJson_RepliesFailureWithoutCallingService()
    {
        var reply = await SendRpcAsync(Command(7, 3, "null"));

        reply.Success.Should().BeFalse();
        reply.ErrorMessage.Should().Be("Invalid createEntityJson.");
        _entityService.Verify(s => s.CreateAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CreateEntityRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NullCommandPayload_RepliesInvalidPayload()
    {
        var reply = await SendRpcAsync("null");

        reply.Success.Should().BeFalse();
        reply.ErrorMessage.Should().Be("Invalid payload.");
    }
}
