namespace Relativa.Authentication.Application.Options;

public sealed class SmtpOptions
{
    public string Host        { get; set; } = string.Empty;
    public int    Port        { get; set; } = 1025;
    public string Username    { get; set; } = string.Empty;
    public string Password    { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName    { get; set; } = string.Empty;
    public bool   UseSsl      { get; set; } = false;
}
