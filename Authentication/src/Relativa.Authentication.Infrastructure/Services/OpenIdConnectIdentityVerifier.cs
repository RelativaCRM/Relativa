using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Relativa.Authentication.Application.Exceptions;
using Relativa.Authentication.Application.Interfaces;
using Relativa.Authentication.Application.Options;

namespace Relativa.Authentication.Infrastructure.Services;

public sealed class OpenIdConnectIdentityVerifier : IExternalIdentityVerifier
{
    private const string TenantPlaceholder = "{tenantid}";

    private static readonly JsonWebTokenHandler Handler = new() { MapInboundClaims = false };
    private static readonly ConcurrentDictionary<string, IssuerValidator?> IssuerValidators = new();

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly IReadOnlyDictionary<string, ProviderContext> _providers;

    public OpenIdConnectIdentityVerifier(IOptions<OAuthOptions> options)
    {
        _providers = options.Value.Providers.ToDictionary(
            p => p.Key,
            p => new ProviderContext(
                new ConfigurationManager<OpenIdConnectConfiguration>(
                    $"{p.Value.Authority.TrimEnd('/')}/.well-known/openid-configuration",
                    new OpenIdConnectConfigurationRetriever(),
                    new HttpDocumentRetriever(_http)),
                p.Value.ClientId,
                p.Value.ValidateIssuer,
                p.Value.UseAccessToken,
                p.Value.TokenInfoEndpoint));
    }

    public Task<ExternalIdentity> VerifyAsync(string provider, string token, CancellationToken ct = default)
    {
        if (!_providers.TryGetValue(provider, out var context))
        {
            throw new AuthException("oauth_provider_not_supported", 400, $"Unsupported OAuth provider '{provider}'.");
        }

        return context.UseAccessToken
            ? VerifyAccessTokenAsync(provider, context, token, ct)
            : VerifyIdTokenAsync(provider, context, token, ct);
    }

    private async Task<ExternalIdentity> VerifyIdTokenAsync(string provider, ProviderContext context, string idToken, CancellationToken ct)
    {
        var config = await context.ConfigManager.GetConfigurationAsync(ct);

        var parameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudience = context.ClientId,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = config.SigningKeys,
            ValidateLifetime = true,
            ValidateIssuer = context.ValidateIssuer,
            ValidIssuer = config.Issuer,
            IssuerValidator = context.ValidateIssuer ? ResolveIssuerValidator(config.Issuer) : null
        };

        var result = await Handler.ValidateTokenAsync(idToken, parameters);
        if (!result.IsValid)
        {
            throw new AuthException("oauth_token_invalid", 401, "The external identity token is invalid.");
        }

        var subject = FindClaim(result.ClaimsIdentity, JwtRegisteredClaimNames.Sub);
        var email = FindClaim(result.ClaimsIdentity, JwtRegisteredClaimNames.Email);

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email))
        {
            throw new AuthException("oauth_missing_claims", 401, "The external identity token is missing required claims.");
        }

        return new ExternalIdentity(
            provider,
            subject,
            email,
            FindClaim(result.ClaimsIdentity, JwtRegisteredClaimNames.GivenName),
            FindClaim(result.ClaimsIdentity, JwtRegisteredClaimNames.FamilyName));
    }

    private async Task<ExternalIdentity> VerifyAccessTokenAsync(string provider, ProviderContext context, string accessToken, CancellationToken ct)
    {
        var tokenInfoUrl = $"{context.TokenInfoEndpoint}?access_token={Uri.EscapeDataString(accessToken)}";
        using var tokenInfoResponse = await _http.GetAsync(tokenInfoUrl, ct);
        if (!tokenInfoResponse.IsSuccessStatusCode)
        {
            throw new AuthException("oauth_token_invalid", 401, "The external access token is invalid.");
        }

        using var tokenInfo = JsonDocument.Parse(await tokenInfoResponse.Content.ReadAsStringAsync(ct));
        var root = tokenInfo.RootElement;

        var audience = GetString(root, "aud");
        if (!string.Equals(audience, context.ClientId, StringComparison.Ordinal))
        {
            throw new AuthException("oauth_audience_mismatch", 401, "The external access token was issued for a different application.");
        }

        var subject = GetString(root, "sub");
        var email = GetString(root, "email");
        string? firstName = null;
        string? lastName = null;

        var config = await context.ConfigManager.GetConfigurationAsync(ct);
        if (!string.IsNullOrWhiteSpace(config.UserInfoEndpoint))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, config.UserInfoEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var userInfoResponse = await _http.SendAsync(request, ct);
            if (userInfoResponse.IsSuccessStatusCode)
            {
                using var userInfo = JsonDocument.Parse(await userInfoResponse.Content.ReadAsStringAsync(ct));
                var userRoot = userInfo.RootElement;
                subject ??= GetString(userRoot, "sub");
                email ??= GetString(userRoot, "email");
                firstName = GetString(userRoot, "given_name");
                lastName = GetString(userRoot, "family_name");
            }
        }

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email))
        {
            throw new AuthException("oauth_missing_claims", 401, "The external identity is missing required claims.");
        }

        return new ExternalIdentity(provider, subject, email, firstName, lastName);
    }

    private static string? FindClaim(ClaimsIdentity identity, string type) => identity.FindFirst(type)?.Value;

    private static string? GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IssuerValidator? ResolveIssuerValidator(string discoveryIssuer)
    {
        return IssuerValidators.GetOrAdd(discoveryIssuer, static issuerTemplate =>
        {
            if (!issuerTemplate.Contains(TenantPlaceholder))
            {
                return null;
            }

            var pattern = "^" + Regex.Escape(issuerTemplate).Replace(Regex.Escape(TenantPlaceholder), "[0-9a-fA-F-]+") + "$";
            var compiled = new Regex(pattern, RegexOptions.Compiled);

            return (issuer, _, _) => compiled.IsMatch(issuer)
                ? issuer
                : throw new SecurityTokenInvalidIssuerException($"Issuer '{issuer}' is not trusted.");
        });
    }

    private sealed record ProviderContext(
        ConfigurationManager<OpenIdConnectConfiguration> ConfigManager,
        string ClientId,
        bool ValidateIssuer,
        bool UseAccessToken,
        string? TokenInfoEndpoint);
}
