using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Relativa.Graph.Hubs;
using Relativa.Persistence.Contracts;

namespace Relativa.Graph.Messaging;

public sealed class DomainEventConsumerHostedService(
    ILogger<DomainEventConsumerHostedService> logger,
    IOptions<RabbitMqGraphConsumerOptions> optionsAccessor,
    IHubContext<GraphHub> hubContext,
    NpgsqlDataSource dataSource) : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string ConsumerGroup = "graph.domain.workspace.v1";
    private readonly RabbitMqGraphConsumerOptions _opts = optionsAccessor.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _opts.Host,
            Port = _opts.Port,
            UserName = _opts.Username,
            Password = _opts.Password
        };

        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync(
            exchange: _opts.DomainExchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync(
            exchange: _opts.DeadLetterFanoutExchange,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(
            queue: _opts.DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await channel.QueueBindAsync(
            queue: _opts.DeadLetterQueueName,
            exchange: _opts.DeadLetterFanoutExchange,
            routingKey: string.Empty,
            cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(
            queue: _opts.WorkspaceQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = _opts.DeadLetterFanoutExchange
            },
            cancellationToken: stoppingToken);

        await channel.QueueBindAsync(
            queue: _opts.WorkspaceQueueName,
            exchange: _opts.DomainExchange,
            routingKey: _opts.WorkspaceBindingRoutingKeyPattern,
            cancellationToken: stoppingToken);

        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 32, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var payload = Encoding.UTF8.GetString(ea.Body.ToArray());
            try
            {
                var envelope = JsonSerializer.Deserialize<DomainMessageEnvelope>(payload, SerializerJson);
                if (envelope is null)
                {
                    await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                    return;
                }

                using (logger.BeginScope(new Dictionary<string, object?>
                       {
                           ["CorrelationId"] = envelope.CorrelationId,
                           ["SagaInstanceId"] = envelope.SagaInstanceId ?? Guid.Empty,
                           ["MessageId"] = envelope.MessageId
                       }))
                {
                    var inserted = await TryMarkProcessedOnceAsync(envelope.MessageId, stoppingToken);
                    if (!inserted)
                    {
                        logger.LogDebug("Skipping duplicate choreography delivery {MessageId}", envelope.MessageId);
                        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                        return;
                    }

                    if (string.Equals(envelope.PayloadTypeName, DomainPayloadTypes.WorkspaceLifecycleV1, StringComparison.Ordinal))
                    {
                        var wsPayload =
                            JsonSerializer.Deserialize<WorkspaceLifecyclePayloadV1>(envelope.PayloadJson, SerializerJson);
                        await hubContext.Clients.All.SendAsync(
                            GraphSignalREvents.WorkspaceLifecycleDomain,
                            new
                            {
                                envelope.MessageId,
                                envelope.CorrelationId,
                                envelope.SagaInstanceId,
                                Lifecycle = wsPayload
                            },
                            stoppingToken);
                    }

                    logger.LogInformation(
                        "Processed domain choreography routingKey={RoutingKey} type={Type}",
                        ea.RoutingKey,
                        envelope.PayloadTypeName);

                    await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                }
            }
            catch (JsonException jx)
            {
                logger.LogError(jx, "Malformed choreography envelope; rejecting without requeue.");
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Choreography handler failed.");
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(_opts.WorkspaceQueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    /// <returns>True when this consumer should process the payload (fresh insert).</returns>
    private async Task<bool> TryMarkProcessedOnceAsync(Guid messageId, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO rabbitmq_processed_delivery (message_id, consumer_group, processed_at_utc)
            VALUES (@message_id, @consumer_group, @processed_at_utc)
            ON CONFLICT DO NOTHING
            """;

        cmd.Parameters.AddWithValue("message_id", messageId);
        cmd.Parameters.AddWithValue("consumer_group", ConsumerGroup);
        cmd.Parameters.AddWithValue("processed_at_utc", DateTimeOffset.UtcNow);

        var n = await cmd.ExecuteNonQueryAsync(ct);
        return n > 0;
    }
}
