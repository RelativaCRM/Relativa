using RabbitMQ.Client;

namespace Relativa.Messaging;

/// <summary>Declares topic exchanges used by transactional outbox dispatchers (<c>audit.*</c> vs domain routing keys).</summary>
public static class OutboxRabbitMqPublisher
{
    public static async Task EnsureTopicExchangesAsync(
        RabbitMqPublishingOptions options,
        CancellationToken ct)
    {
        await using var connection = await CreateConnectionAsync(options, ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
        await EnsureTopicExchangesOnChannelAsync(channel, options, ct);
    }

    public static Task<IConnection> CreateConnectionAsync(RabbitMqPublishingOptions options, CancellationToken ct) =>
        new ConnectionFactory
        {
            HostName = options.Host,
            Port = options.Port,
            UserName = options.Username,
            Password = options.Password
        }.CreateConnectionAsync(ct);

    public static async Task EnsureTopicExchangesOnChannelAsync(
        IChannel channel,
        RabbitMqPublishingOptions options,
        CancellationToken ct)
    {
        await EnsureTopic(channel, options.AuditExchange, ct);
        await EnsureTopic(channel, options.DomainExchange, ct);
    }

    private static async Task EnsureTopic(IChannel channel, string exchangeName, CancellationToken ct) =>
        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);
}
