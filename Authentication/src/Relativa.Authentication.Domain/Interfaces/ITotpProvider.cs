namespace Relativa.Authentication.Domain.Interfaces;

public interface ITotpProvider
{
    string GenerateSecret();
    string BuildOtpAuthUri(string secret, string accountName, string issuer);
    bool VerifyCode(string secret, string code);
}
