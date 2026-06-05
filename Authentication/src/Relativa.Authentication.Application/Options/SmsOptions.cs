namespace Relativa.Authentication.Application.Options;

public sealed class SmsOptions
{
    public const string SectionKey = "Sms";

    public string Provider { get; set; } = "smtp";
    public string SinkEmail { get; set; } = "sms@relativa.local";
    public string? Endpoint { get; set; }
    public string? AuthToken { get; set; }
    public string? From { get; set; }
}
