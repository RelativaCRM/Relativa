using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Relativa.Core.Infrastructure.Data;
using Relativa.Persistence.Contracts;

namespace Relativa.Core.Infrastructure.Services.Audit;

public sealed class AuditOutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqAuditOptions> options,
    ILogger<AuditOutboxDispatcher> logger) : BackgroundService
{
    private readonly RabbitMqAuditOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatch(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Core audit outbox dispatcher failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    private async Task DispatchBatch(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RelativaDbContext>();

        var pending = await db.AuditOutboxMessages
            .Where(x => x.PublishedAtUtc == null)
            .OrderBy(x => x.Id)
            .Take(100)
            .ToListAsync(ct);

        if (pending.Count == 0)
        {
            return;
        }

        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            UserName = _options.Username,
            Password = _options.Password
        };

        await using var connection = await factory.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
        await channel.ExchangeDeclareAsync(
            exchange: _options.Exchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

        foreach (var message in pending)
        {
            try
            {
                var body = System.Text.Encoding.UTF8.GetBytes(message.PayloadJson);
                await channel.BasicPublishAsync(
                    exchange: _options.Exchange,
                    routingKey: message.RoutingKey,
                    mandatory: false,
                    body: body,
                    cancellationToken: ct);

                message.PublishedAtUtc = DateTimeOffset.UtcNow;
                message.LastError = null;
            }
            catch (Exception ex)
            {
                message.LastError = ex.Message;
                logger.LogWarning(ex, "Failed publishing core audit outbox event {EventId}", message.EventId);
            }
            finally
            {
                message.PublishAttempts++;
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
