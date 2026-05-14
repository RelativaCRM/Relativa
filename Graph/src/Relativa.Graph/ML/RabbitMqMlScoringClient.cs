using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Relativa.Graph.Messaging;
using Relativa.Persistence.Contracts;

namespace Relativa.Graph.ML;

public sealed class RabbitMqMlScoringClient(
    IOptions<RabbitMqGraphConsumerOptions> opts,
    ILogger<RabbitMqMlScoringClient> logger) : IMlScoringClient
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private const int TimeoutSeconds = 8;

    public async Task<IReadOnlyDictionary<int, MlScoreDto>> ScoreBatchAsync(
        IReadOnlyList<int> dealEntityIds,
        CancellationToken ct = default)
    {
        if (dealEntityIds.Count == 0)
            return new Dictionary<int, MlScoreDto>();

        try
        {
            return await SendRpcAsync(dealEntityIds, ct);
        }
        catch (TimeoutException)
        {
            logger.LogWarning("ML scoring RPC timed out after {Seconds}s — graph renders without highlights", TimeoutSeconds);
            return new Dictionary<int, MlScoreDto>();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ML scoring RPC failed — graph renders without highlights");
            return new Dictionary<int, MlScoreDto>();
        }
    }

    private async Task<IReadOnlyDictionary<int, MlScoreDto>> SendRpcAsync(
        IReadOnlyList<int> dealEntityIds,
        CancellationToken ct)
    {
        var o = opts.Value;
        var factory = new ConnectionFactory
        {
            HostName = o.Host,
            Port = o.Port,
            UserName = o.Username,
            Password = o.Password,
        };

        await using var connection = await factory.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await channel.ExchangeDeclareAsync(
            exchange: MlScoringRouting.ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

        var declareOk = await channel.QueueDeclareAsync(
            queue: string.Empty,
            durable: false,
            exclusive: true,
            autoDelete: true,
            arguments: null,
            passive: false,
            cancellationToken: ct);

        var replyQueue = declareOk.QueueName;
        var correlationId = Guid.NewGuid().ToString("N");
        var props = new BasicProperties
        {
            CorrelationId = correlationId,
            ReplyTo = replyQueue,
        };

        var request = new MlScoreRpcRequestV1(dealEntityIds);
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, Json));

        await channel.BasicPublishAsync(
            exchange: MlScoringRouting.ExchangeName,
            routingKey: MlScoringRouting.CommandRoutingKey,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: ct);

        var deadline = DateTime.UtcNow.AddSeconds(TimeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var get = await channel.BasicGetAsync(replyQueue, autoAck: true, cancellationToken: ct);
            if (get is null)
            {
                await Task.Delay(25, ct);
                continue;
            }

            if (get.BasicProperties?.CorrelationId != correlationId)
                continue;

            var replyJson = Encoding.UTF8.GetString(get.Body.ToArray());
            var reply = JsonSerializer.Deserialize<MlScoreRpcReplyV1>(replyJson, Json);
            if (reply is null || reply.ErrorMessage is not null)
            {
                logger.LogWarning("ML scoring RPC returned error: {Error}", reply?.ErrorMessage ?? "null reply");
                return new Dictionary<int, MlScoreDto>();
            }

            return reply.Scores.ToDictionary(
                s => s.EntityId,
                s => new MlScoreDto(s.EntityId, s.ClosureScore, s.ChurnScore, s.UnavailableReason));
        }

        throw new TimeoutException($"Timed out waiting for ML scoring RPC reply after {TimeoutSeconds}s.");
    }
}
