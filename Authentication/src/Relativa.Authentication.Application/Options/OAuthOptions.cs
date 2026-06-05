using Microsoft.Extensions.Options;

namespace Relativa.Authentication.Application.Options;

public sealed class OAuthOptions
{
    public const string SectionKey = "OAuth";

    public Dictionary<string, OAuthProviderOptions> Providers { get; set; } = new();
}

public sealed class OAuthProviderOptions
{
    public string Authority { get; set; } = null!;
    public string ClientId { get; set; } = null!;
    public bool ValidateIssuer { get; set; } = true;
    public bool UseAccessToken { get; set; }
    public string? TokenInfoEndpoint { get; set; }
}

public sealed class OAuthOptionsValidator : IValidateOptions<OAuthOptions>
{
    public ValidateOptionsResult Validate(string? name, OAuthOptions options)
    {
        var failures = new List<string>();

        foreach (var (key, provider) in options.Providers)
        {
            if (string.IsNullOrWhiteSpace(provider.Authority))
            {
                failures.Add($"OAuth provider '{key}' is missing Authority.");
            }

            if (string.IsNullOrWhiteSpace(provider.ClientId))
            {
                failures.Add($"OAuth provider '{key}' is missing ClientId.");
            }

            if (provider.UseAccessToken && string.IsNullOrWhiteSpace(provider.TokenInfoEndpoint))
            {
                failures.Add($"OAuth provider '{key}' has UseAccessToken=true but no TokenInfoEndpoint.");
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
