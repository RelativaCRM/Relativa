namespace Relativa.Authentication.Application.Options;

public sealed class TwoFactorOptions
{
    public const string SectionKey = "TwoFactor";

    public string Issuer { get; set; } = "Relativa";
    public int BackupCodeCount { get; set; } = 10;
    public int BackupCodeLength { get; set; } = 10;
    public int MasterCodeLength { get; set; } = 8;
    public string BackupCodeAlphabet { get; set; } = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
}
