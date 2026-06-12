using FluentAssertions;
using Microsoft.Extensions.Options;
using static Microsoft.Extensions.Options.Options;
using Relativa.Authentication.Application.Exceptions;
using Relativa.Authentication.Application.Options;
using Relativa.Authentication.Infrastructure.Services;
using Xunit;

namespace Relativa.Authentication.Application.Tests;

public sealed class OpenIdConnectIdentityVerifierTests
{
    [Fact]
    public async Task VerifyAsync_UnknownProvider_ThrowsUnsupportedProvider()
    {
        var sut = new OpenIdConnectIdentityVerifier(Create(new OAuthOptions()));

        var act = () => sut.VerifyAsync("google", "any-token");

        (await act.Should().ThrowAsync<AuthException>()).Which.Code.Should().Be("oauth_provider_not_supported");
    }

    [Fact]
    public async Task VerifyAsync_ProviderNotInConfiguredSet_IsRejectedBeforeAnyNetworkCall()
    {
        var options = new OAuthOptions();
        options.Providers["google"] = new OAuthProviderOptions { Authority = "https://accounts.google.com", ClientId = "cid" };
        var sut = new OpenIdConnectIdentityVerifier(Create(options));

        var act = () => sut.VerifyAsync("microsoft", "any-token");

        (await act.Should().ThrowAsync<AuthException>()).Which.Code.Should().Be("oauth_provider_not_supported");
    }
}
