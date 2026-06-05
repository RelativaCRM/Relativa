namespace Relativa.Authentication.Application.Interfaces;

public interface IEmailVerificationService
{
    Task ResendAsync(string email, VerificationChannel channel, string? clientIp, CancellationToken ct = default);
    Task<bool> SmsAvailableAsync(string email, CancellationToken ct = default);
    Task VerifyAsync(string email, string code, string? clientIp, CancellationToken ct = default);
}
