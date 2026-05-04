namespace Relativa.Messaging;

public static class RabbitMqExchangeRouter
{
    /// <summary>Routes audit envelope keys (<c>audit.*</c>) to the audit topic exchange; all other keys to the domain choreography exchange.</summary>
    public static string ResolvePublishExchange(string routingKey, RabbitMqPublishingOptions options)
    {
        ArgumentNullException.ThrowIfNull(routingKey);
        ArgumentNullException.ThrowIfNull(options);
        return routingKey.StartsWith("audit.", StringComparison.OrdinalIgnoreCase)
            ? options.AuditExchange
            : options.DomainExchange;
    }
}
