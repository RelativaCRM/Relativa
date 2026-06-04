namespace Relativa.Authentication.Application.DTOs;

public sealed record TwoFactorStatusDto(bool Enabled);

public sealed record TwoFactorSetupDto(string Secret, string OtpauthUri);

public sealed record TwoFactorBackupCodesDto(IReadOnlyList<string> Codes);

public sealed record TwoFactorEnableResultDto(IReadOnlyList<string> BackupCodes, string MasterCode);

public sealed record TwoFactorMasterCodeDto(string MasterCode);

public sealed record TwoFactorCodeRequest(string Code);
