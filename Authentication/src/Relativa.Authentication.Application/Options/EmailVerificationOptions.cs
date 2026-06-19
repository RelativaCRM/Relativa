namespace Relativa.Authentication.Application.Options;

public sealed class EmailVerificationOptions
{
    public const string SectionKey = "EmailVerification";

    public int CodeLength { get; set; } = 6;
    public string CodeAlphabet { get; set; } = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    public int ResendCooldownSeconds { get; set; } = 120;
    public int CodeLifetimeMinutes { get; set; } = 15;
    public int MaxPerEmailPerHour { get; set; } = 5;
    public int MaxPerIpPerHour { get; set; } = 20;
    public int MaxVerifyAttemptsPerHour { get; set; } = 10;
}
