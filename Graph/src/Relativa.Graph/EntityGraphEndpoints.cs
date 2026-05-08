using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Relativa.Graph.Messaging;
using Relativa.Persistence.Contracts;

namespace Relativa.Graph;

public static class EntityGraphEndpoints
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static void MapEntityGraphEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/v1/workspaces/{workspaceId:int}/entity-graph")
            .WithTags("EntityGraph");

        group.MapPost("/create", async (
                int workspaceId,
                HttpContext httpContext,
                JsonElement createBody,
                IOptions<RabbitMqGraphConsumerOptions> mq,
                CancellationToken ct) =>
            {
                int userId;
                try
                {
                    userId = GetUserIdOrThrow(httpContext);
                }
                catch (UnauthorizedAccessException)
                {
                    return Results.Unauthorized();
                }

                var cmd = new EntityGraphCreateRpcV1(workspaceId, userId, createBody.GetRawText());
                try
                {
                    var reply = await RpcCreateAsync(mq.Value, cmd, ct);
                    if (!reply.Success || string.IsNullOrEmpty(reply.EntityDetailJson))
                        return Results.BadRequest(reply.ErrorMessage ?? "Graph create failed.");

                    return Results.Content(reply.EntityDetailJson, "application/json", statusCode: StatusCodes.Status200OK);
                }
                catch (TimeoutException)
                {
                    return Results.StatusCode((int)HttpStatusCode.GatewayTimeout);
                }
            })
            .WithName("EntityGraphCreate")
            .WithSummary("Delegates entity creation to Core via RabbitMQ RPC (single-node create body matches Core CreateEntityRequest).");
    }

    private static int GetUserIdOrThrow(HttpContext ctx)
    {
        var v = ctx.Request.Headers["X-User-Id"].ToString();
        if (string.IsNullOrEmpty(v) || !int.TryParse(v, out var id))
            throw new UnauthorizedAccessException("Missing or invalid X-User-Id header.");
        return id;
    }

    private static async Task<EntityGraphCreateRpcReplyV1> RpcCreateAsync(
        RabbitMqGraphConsumerOptions opts,
        EntityGraphCreateRpcV1 cmd,
        CancellationToken ct)
    {
        var factory = new ConnectionFactory
        {
            HostName = opts.Host,
            Port = opts.Port,
            UserName = opts.Username,
            Password = opts.Password,
        };

        await using var connection = await factory.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await channel.ExchangeDeclareAsync(
            exchange: EntityGraphRouting.ExchangeName,
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

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cmd, Json));
        await channel.BasicPublishAsync(
            exchange: EntityGraphRouting.ExchangeName,
            routingKey: EntityGraphRouting.CommandRoutingKey,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: ct);

        var deadline = DateTime.UtcNow.AddSeconds(30);
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
            var reply = JsonSerializer.Deserialize<EntityGraphCreateRpcReplyV1>(replyJson, Json);
            return reply ?? new EntityGraphCreateRpcReplyV1(false, "Invalid reply payload.", null);
        }

        throw new TimeoutException("Timed out waiting for Core entity graph reply.");
    }
}
