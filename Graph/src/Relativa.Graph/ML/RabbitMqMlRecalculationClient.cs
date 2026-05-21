using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Relativa.Graph.Messaging;
using Relativa.Persistence.Contracts;

namespace Relativa.Graph.ML;

public sealed class RabbitMqMlRecalculationClient(
    IOptions<RabbitMqGraphConsumerOptions> opts,
    ILogger<RabbitMqMlRecalculationClient> logger) : IMlRecalculationClient
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public async Task EnqueueAsync(
        IReadOnlyList<int> dealEntityIds,
        int requestedByUserId,
        int? workspaceId = null,
        CancellationToken ct = default)
    {
        if (dealEntityIds.Count == 0) return;

        try
        {
            var o = opts.Value;
            var factory = new ConnectionFactory
            {
                HostName = o.Host,
                Port     = o.Port,
                UserName = o.Username,
                Password = o.Password,
            };

            await using var connection = await factory.CreateConnectionAsync(ct);
            await using var channel   = await connection.CreateChannelAsync(cancellationToken: ct);

            await channel.ExchangeDeclareAsync(
                exchange:   DomainRouting.ExchangeName,
                type:       ExchangeType.Topic,
                durable:    true,
                autoDelete: false,
                cancellationToken: ct);

            var jobId = Guid.NewGuid();
            var payload = new MlRecalculateEnqueuedPayloadV1(
                JobId:              jobId,
                WorkspaceId:        workspaceId,
                RequestedByUserId:  requestedByUserId,
                RequestedAtUtc:     DateTimeOffset.UtcNow,
                Scope:              "entity_ids",
                EntityIds:          dealEntityIds,
                Reason:             "dashboard_auto_recalc");

            var envelope = new DomainMessageEnvelope(
                SchemaVersion:  1,
                MessageId:      Guid.NewGuid(),
                CorrelationId:  jobId,
                SagaInstanceId: null,
                CausationId:    null,
                OccurredAtUtc:  DateTimeOffset.UtcNow,
                SourceService:  "graph",
                PayloadTypeName: DomainPayloadTypes.MlRecalculateEnqueuedV1,
                PayloadJson:    JsonSerializer.Serialize(payload, Json));

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope, Json));

            await channel.BasicPublishAsync(
                exchange:   DomainRouting.ExchangeName,
                routingKey: DomainRouting.RoutingKeyMlRecalculate(DomainRouting.MlRecalculateVerbEnqueued),
                mandatory:  false,
                body:       body,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to enqueue ML recalculation for {Count} deals — scores will appear on next load",
                dealEntityIds.Count);
        }
    }
}
