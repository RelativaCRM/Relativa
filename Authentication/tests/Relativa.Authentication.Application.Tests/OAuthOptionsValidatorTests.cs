using FluentAssertions;
using Microsoft.Extensions.Options;
using Relativa.Authentication.Application.Options;
using Xunit;

namespace Relativa.Authentication.Application.Tests;

public sealed class OAuthOptionsValidatorTests
{
    private readonly OAuthOptionsValidator _sut = new();

    [Fact]
    public void Validate_NoProviders_Succeeds()
    {
        _sut.Validate(null, new OAuthOptions()).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_FullyConfiguredProvider_Succeeds()
    {
        var options = new OAuthOptions();
        options.Providers["google"] = new OAuthProviderOptions { Authority = "https://accounts.google.com", ClientId = "cid" };

        _sut.Validate(null, options).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_MissingAuthority_Fails()
    {
        var options = new OAuthOptions();
        options.Providers["google"] = new OAuthProviderOptions { Authority = "", ClientId = "cid" };

        var result = _sut.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Authority");
    }

    [Fact]
    public void Validate_MissingClientId_Fails()
    {
        var options = new OAuthOptions();
        options.Providers["google"] = new OAuthProviderOptions { Authority = "https://accounts.google.com", ClientId = "" };

        var result = _sut.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("ClientId");
    }

    [Fact]
    public void Validate_AccessTokenProviderWithoutTokenInfoEndpoint_Fails()
    {
        var options = new OAuthOptions();
        options.Providers["github"] = new OAuthProviderOptions
        {
            Authority = "https://github.com",
            ClientId = "cid",
            UseAccessToken = true,
            TokenInfoEndpoint = null,
        };

        var result = _sut.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("TokenInfoEndpoint");
    }
}
