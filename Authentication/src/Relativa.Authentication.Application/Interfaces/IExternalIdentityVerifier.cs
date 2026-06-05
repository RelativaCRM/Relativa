namespace Relativa.Authentication.Application.Interfaces;

public sealed record ExternalIdentity(
    string Provider,
    string Subject,
    string Email,
    string? FirstName,
    string? LastName);

public interface IExternalIdentityVerifier
{
    Task<ExternalIdentity> VerifyAsync(string provider, string idToken, CancellationToken ct = default);
}
