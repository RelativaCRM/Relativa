using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Testcontainers.RabbitMq;
using Xunit;

namespace Relativa.Messaging.Tests;

public sealed class RabbitMqOutboxPublishingIntegrationTests : IAsyncLifetime
{
    private RabbitMqContainer? _rabbit;

    public async Task InitializeAsync()
    {
        _rabbit = new RabbitMqBuilder()
            .WithImage("rabbitmq:3.13-alpine")
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();

        await _rabbit.StartAsync();
    }

    public Task DisposeAsync() => _rabbit is null ? Task.CompletedTask : _rabbit.DisposeAsync().AsTask();

    [Fact]
    public async Task Declares_audit_and_domain_exchanges_and_publish_round_trips_via_topic_queue()
    {
        Assert.NotNull(_rabbit);

        var opts = new RabbitMqPublishingOptions
        {
            Host = _rabbit.Hostname,
            Port = _rabbit.GetMappedPublicPort(5672),
            Username = "guest",
            Password = "guest",
            AuditExchange = "audit.events.it",
            DomainExchange = "relativa.domain.it"
        };

        await OutboxRabbitMqPublisher.EnsureTopicExchangesAsync(opts, CancellationToken.None);

        var factory = new ConnectionFactory
        {
            HostName = opts.Host,
            Port = opts.Port,
            UserName = opts.Username,
            Password = opts.Password
        };

        await using var connection = await factory.CreateConnectionAsync(CancellationToken.None);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: CancellationToken.None);

        const string queueName = "it.consume.core.workspace.created";
        await channel.QueueDeclareAsync(queueName, durable: false, exclusive: false, autoDelete: true, cancellationToken: CancellationToken.None);
        await channel.QueueBindAsync(queueName, opts.DomainExchange, routingKey: "core.workspace.created", cancellationToken: CancellationToken.None);

        var tcs = new TaskCompletionSource<byte[]>();

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            tcs.TrySetResult(ea.Body.ToArray());
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, CancellationToken.None);
        };

        await channel.BasicConsumeAsync(queueName, autoAck: false, consumer: consumer, cancellationToken: CancellationToken.None);

        var expected = Encoding.UTF8.GetBytes("""{"Smoke":true}""");
        await channel.BasicPublishAsync(
            exchange: RabbitMqExchangeRouter.ResolvePublishExchange("core.workspace.created", opts),
            routingKey: "core.workspace.created",
            mandatory: false,
            body: expected,
            cancellationToken: CancellationToken.None);

        var gate = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(25)));
        Assert.True(ReferenceEquals(gate, tcs.Task), "Did not consume a message from RabbitMQ in time.");

        Assert.Equal(expected, await tcs.Task);
    }
}
