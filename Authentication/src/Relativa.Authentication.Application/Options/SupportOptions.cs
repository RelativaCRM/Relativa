namespace Relativa.Authentication.Application.Options;

public sealed class SupportOptions
{
    public const string SectionKey = "Support";

    public string DevEmail { get; set; } = string.Empty;
    public int MaxPerIpPerHour { get; set; } = 5;
}
