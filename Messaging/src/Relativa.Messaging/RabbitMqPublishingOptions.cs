namespace Relativa.Messaging;

public sealed class RabbitMqPublishingOptions
{
    public const string ConfigurationSectionKey = "RabbitMqAudit";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";

    public string AuditExchange { get; set; } = "audit.events";

    /// <summary>Allows legacy appsettings binding to key <c>Exchange</c> mapped to audit exchange.</summary>
    public string Exchange
    {
        get => AuditExchange;
        set => AuditExchange = value;
    }

    /// <summary>Topic exchange for choreography and domain lifecycle messages (routing keys never start with audit.).</summary>
    public string DomainExchange { get; set; } = "relativa.domain";
}
