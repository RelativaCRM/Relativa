using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Relativa.Graph.Data;
using Relativa.Graph.Hubs;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;

namespace Relativa.Graph.Messaging;

public sealed class DomainEventConsumerHostedService(
    ILogger<DomainEventConsumerHostedService> logger,
    IOptions<RabbitMqGraphConsumerOptions> optionsAccessor,
    IHubContext<GraphHub> hubContext,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string ConsumerGroup   = "graph.domain.workspace.v1";
    private const string MlConsumerGroup = "graph.domain.ml.v1";
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
                    var inserted = await TryMarkProcessedOnceAsync(envelope.MessageId, ConsumerGroup, stoppingToken);
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

        // ── ML recalculation consumer ─────────────────────────────────────────
        await channel.ExchangeDeclareAsync(
            exchange:   _opts.MlDeadLetterExchange,
            type:       ExchangeType.Fanout,
            durable:    true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(
            queue:      _opts.MlDeadLetterQueueName,
            durable:    true,
            exclusive:  false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await channel.QueueBindAsync(
            queue:      _opts.MlDeadLetterQueueName,
            exchange:   _opts.MlDeadLetterExchange,
            routingKey: string.Empty,
            cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(
            queue:      _opts.MlQueueName,
            durable:    true,
            exclusive:  false,
            autoDelete: false,
            arguments:  new Dictionary<string, object?> { ["x-dead-letter-exchange"] = _opts.MlDeadLetterExchange },
            cancellationToken: stoppingToken);

        await channel.QueueBindAsync(
            queue:      _opts.MlQueueName,
            exchange:   _opts.DomainExchange,
            routingKey: _opts.MlBindingRoutingKeyPattern,
            cancellationToken: stoppingToken);

        var mlConsumer = new AsyncEventingBasicConsumer(channel);
        mlConsumer.ReceivedAsync += async (_, ea) =>
        {
            var body = Encoding.UTF8.GetString(ea.Body.ToArray());
            try
            {
                var envelope = JsonSerializer.Deserialize<DomainMessageEnvelope>(body, SerializerJson);
                if (envelope is null)
                {
                    await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                    return;
                }

                var inserted = await TryMarkProcessedOnceAsync(envelope.MessageId, MlConsumerGroup, stoppingToken);
                if (!inserted)
                {
                    await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                    return;
                }

                if (string.Equals(envelope.PayloadTypeName, DomainPayloadTypes.MlRecalculateProgressV1, StringComparison.Ordinal))
                {
                    var prog = JsonSerializer.Deserialize<MlRecalculateProgressPayloadV1>(envelope.PayloadJson, SerializerJson);
                    if (prog?.WorkspaceId is { } wsId)
                    {
                        await hubContext.Clients
                            .Group($"workspace-{wsId}")
                            .SendAsync(GraphSignalREvents.MlRecalculateProgress,
                                new { prog.JobId, prog.WorkspaceId, prog.ProcessedCount, prog.TotalCount, prog.Status },
                                stoppingToken);
                    }
                }
                else if (string.Equals(envelope.PayloadTypeName, DomainPayloadTypes.MlRecalculateCompletedV1, StringComparison.Ordinal))
                {
                    var comp = JsonSerializer.Deserialize<MlRecalculateCompletedPayloadV1>(envelope.PayloadJson, SerializerJson);
                    if (comp?.WorkspaceId is { } wsId)
                    {
                        await hubContext.Clients
                            .Group($"workspace-{wsId}")
                            .SendAsync(GraphSignalREvents.MlRecalculateCompleted,
                                new { comp.JobId, comp.WorkspaceId, comp.SucceededCount, comp.FailedCount, comp.Status },
                                stoppingToken);
                    }
                }

                logger.LogInformation(
                    "Processed ML recalculate choreography routingKey={RoutingKey} type={Type}",
                    ea.RoutingKey,
                    envelope.PayloadTypeName);

                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
            }
            catch (JsonException jx)
            {
                logger.LogError(jx, "Malformed ML recalculate envelope; rejecting without requeue.");
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ML recalculate handler failed.");
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(_opts.MlQueueName, autoAck: false, consumer: mlConsumer, cancellationToken: stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    /// <returns>True when this consumer should process the payload (fresh insert).</returns>
    private async Task<bool> TryMarkProcessedOnceAsync(Guid messageId, string consumerGroup, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GraphDbContext>();
        db.RabbitMqProcessedDeliveries.Add(new RabbitMqProcessedDelivery
        {
            MessageId = messageId,
            ConsumerGroup = consumerGroup,
            ProcessedAtUtc = DateTimeOffset.UtcNow
        });
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
                                           pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return false;
        }
    }
}
