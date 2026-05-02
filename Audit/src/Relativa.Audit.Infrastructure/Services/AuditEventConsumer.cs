using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Relativa.Audit.Infrastructure.Data;
using Relativa.Persistence.Contracts;
using Relativa.Persistence.Entities;
using Relativa.Persistence.Entities.AuditLogs;

namespace Relativa.Audit.Infrastructure.Services;

public sealed class AuditEventConsumer(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqAuditOptions> options,
    ILogger<AuditEventConsumer> logger) : BackgroundService
{
    private readonly RabbitMqAuditOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            UserName = _options.Username,
            Password = _options.Password
        };

        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.ExchangeDeclareAsync(
            exchange: _options.Exchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(
            queue: _options.Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await channel.QueueBindAsync(
            queue: _options.Queue,
            exchange: _options.Exchange,
            routingKey: _options.RoutingKey,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var payload = Encoding.UTF8.GetString(ea.Body.ToArray());
            try
            {
                var evt = JsonSerializer.Deserialize<AuditEventContract>(payload);
                if (evt is null)
                {
                    await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                    return;
                }

                await PersistAsync(evt, stoppingToken);
                await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process audit event payload");
                await channel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
            }
        };

        await channel.BasicConsumeAsync(_options.Queue, false, consumer, stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task PersistAsync(AuditEventContract evt, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

        var exists = await db.AuditProcessedEvents.AnyAsync(x => x.EventId == evt.EventId, ct);
        if (exists)
        {
            return;
        }

        switch (evt.AuditScope)
        {
            case AuditRouting.ScopeEntity:
                db.EntityAuditLogs.Add(new EntityAuditLog
                {
                    Id = evt.EventId,
                    EntityId = evt.TargetId,
                    EntityType = evt.EntityType,
                    Action = evt.Action,
                    FieldName = evt.FieldName,
                    ChangedById = evt.ActorUserId,
                    OldValue = TryParseJsonDocument(evt.OldValueJson),
                    NewValue = TryParseJsonDocument(evt.NewValueJson),
                    ChangedAt = evt.OccurredAtUtc
                });
                break;
            case AuditRouting.ScopeWorkspace:
                db.WorkspaceAuditLogs.Add(new WorkspaceAuditLog
                {
                    Id = evt.EventId,
                    WorkspaceId = evt.TargetId,
                    Action = evt.Action,
                    FieldName = evt.FieldName,
                    ChangedById = evt.ActorUserId,
                    OldValue = TryParseJsonDocument(evt.OldValueJson),
                    NewValue = TryParseJsonDocument(evt.NewValueJson),
                    ChangedAt = evt.OccurredAtUtc
                });
                break;
            case AuditRouting.ScopeOrganization:
                db.OrganizationAuditLogs.Add(new OrganizationAuditLog
                {
                    Id = evt.EventId,
                    OrganizationId = evt.TargetId,
                    Action = evt.Action,
                    FieldName = evt.FieldName,
                    ChangedById = evt.ActorUserId,
                    OldValue = TryParseJsonDocument(evt.OldValueJson),
                    NewValue = TryParseJsonDocument(evt.NewValueJson),
                    ChangedAt = evt.OccurredAtUtc
                });
                break;
            case AuditRouting.ScopeUser:
                db.UserAuditLogs.Add(new UserAuditLog
                {
                    Id = evt.EventId,
                    TargetUserId = evt.TargetId,
                    Action = evt.Action,
                    FieldName = evt.FieldName,
                    ChangedById = evt.ActorUserId,
                    OldValue = TryParseJsonDocument(evt.OldValueJson),
                    NewValue = TryParseJsonDocument(evt.NewValueJson),
                    ChangedAt = evt.OccurredAtUtc
                });
                break;
            default:
                return;
        }

        db.AuditProcessedEvents.Add(new AuditProcessedEvent
        {
            EventId = evt.EventId,
            ProcessedAtUtc = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(ct);
    }

    private static JsonDocument? TryParseJsonDocument(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonDocument.Parse(json);
    }
}
