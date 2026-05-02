namespace Relativa.Authentication.Infrastructure.Services.Audit;

public sealed class RabbitMqAuditOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string Exchange { get; set; } = "audit.events";
}
