using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Relativa.Core.Application.DTOs.Entity;
using Relativa.Core.Application.Interfaces;
using Relativa.Persistence.Contracts;

namespace Relativa.Core.Infrastructure.Messaging;

public sealed class EntityGraphRabbitOptions
{
    public const string SectionKey = "RabbitMqGraph";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}

public sealed class EntityGraphCommandConsumerHostedService(
    ILogger<EntityGraphCommandConsumerHostedService> logger,
    IOptions<EntityGraphRabbitOptions> optionsAccessor,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly EntityGraphRabbitOptions _opts = optionsAccessor.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _opts.Host,
            Port = _opts.Port,
            UserName = _opts.Username,
            Password = _opts.Password,
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await channel.ExchangeDeclareAsync(
                    exchange: EntityGraphRouting.ExchangeName,
                    type: ExchangeType.Topic,
                    durable: true,
                    autoDelete: false,
                    cancellationToken: stoppingToken);

                await channel.QueueDeclareAsync(
                    queue: EntityGraphRouting.CommandQueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    cancellationToken: stoppingToken);

                await channel.QueueBindAsync(
                    queue: EntityGraphRouting.CommandQueueName,
                    exchange: EntityGraphRouting.ExchangeName,
                    routingKey: EntityGraphRouting.CommandRoutingKey,
                    cancellationToken: stoppingToken);

                await channel.BasicQosAsync(0, 8, false, stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, ea) =>
                {
                    await HandleAsync(channel, ea, stoppingToken);
                };

                await channel.BasicConsumeAsync(
                    EntityGraphRouting.CommandQueueName,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                try
                {
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // shutting down
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Entity graph Rabbit consumer loop failed; retrying in 5s");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task HandleAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
        var props = ea.BasicProperties;
        var replyProps = new BasicProperties
        {
            CorrelationId = props.CorrelationId,
        };

        async Task ReplyAsync(EntityGraphCreateRpcReplyV1 payload)
        {
            if (string.IsNullOrEmpty(props.ReplyTo))
                return;

            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, Json));
            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: props.ReplyTo,
                mandatory: false,
                basicProperties: replyProps,
                body: bytes,
                cancellationToken: ct);
        }

        try
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            var cmd = JsonSerializer.Deserialize<EntityGraphCreateRpcV1>(json, Json);
            if (cmd is null)
            {
                await ReplyAsync(new EntityGraphCreateRpcReplyV1(false, "Invalid payload.", null));
                await channel.BasicAckAsync(ea.DeliveryTag, false, ct);
                return;
            }

            var request = JsonSerializer.Deserialize<CreateEntityRequest>(cmd.CreateEntityJson, Json);
            if (request is null)
            {
                await ReplyAsync(new EntityGraphCreateRpcReplyV1(false, "Invalid createEntityJson.", null));
                await channel.BasicAckAsync(ea.DeliveryTag, false, ct);
                return;
            }

            using var scope = scopeFactory.CreateScope();
            var entityService = scope.ServiceProvider.GetRequiredService<IEntityService>();

            var detail = await entityService.CreateAsync(cmd.WorkspaceId, cmd.UserId, request, ct);
            var detailJson = JsonSerializer.Serialize(detail, Json);
            await ReplyAsync(new EntityGraphCreateRpcReplyV1(true, null, detailJson));
            await channel.BasicAckAsync(ea.DeliveryTag, false, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Entity graph create failed");
            try
            {
                await ReplyAsync(new EntityGraphCreateRpcReplyV1(false, ex.Message, null));
            }
            catch (Exception replyEx)
            {
                logger.LogWarning(replyEx, "Failed to publish entity graph RPC reply");
            }

            await channel.BasicAckAsync(ea.DeliveryTag, false, ct);
        }
    }
}
