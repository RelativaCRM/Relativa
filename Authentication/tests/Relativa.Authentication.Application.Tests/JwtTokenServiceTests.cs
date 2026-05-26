using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Relativa.Authentication.Infrastructure.Services;
using Relativa.Persistence.Entities;
using Xunit;

namespace Relativa.Authentication.Application.Tests;

public sealed class JwtTokenServiceTests
{
    private static JwtOptions DefaultOptions() => new()
    {
        SecretKey = "relativa-test-secret-key-32bytes!!",
        Issuer = "relativa-test",
        Audience = "relativa-client",
        ExpirationMinutes = 60,
    };

    private static JwtTokenService Build(JwtOptions? opts = null) =>
        new(new OptionsWrapper<JwtOptions>(opts ?? DefaultOptions()));

    private static User SampleUser(int id = 7, string email = "taras@relativa.io") =>
        new() { Id = id, Email = email, FirstName = "Taras", LastName = "K", Password = "x" };

    private JwtSecurityToken Parse(string token)
    {
        var opts = DefaultOptions();
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.SecretKey));
        handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidIssuer = opts.Issuer,
            ValidateAudience = true,
            ValidAudience = opts.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        }, out var validated);
        return (JwtSecurityToken)validated;
    }

    [Fact]
    public void GenerateAccessToken_SubClaimContainsUserId()
    {
        var user = SampleUser(id: 42);
        var sut = Build();

        var (token, _) = sut.GenerateAccessToken(user);

        var jwt = Parse(token);
        jwt.Subject.Should().Be("42");
    }

    [Fact]
    public void GenerateAccessToken_EmailClaimMatchesUser()
    {
        var user = SampleUser(email: "oksana@relativa.io");
        var sut = Build();

        var (token, _) = sut.GenerateAccessToken(user);

        var jwt = Parse(token);
        jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value
            .Should().Be("oksana@relativa.io");
    }

    [Fact]
    public void GenerateAccessToken_JtiIsUniqueAcrossTwoCalls()
    {
        var user = SampleUser();
        var sut = Build();

        var (t1, _) = sut.GenerateAccessToken(user);
        var (t2, _) = sut.GenerateAccessToken(user);

        var jti1 = Parse(t1).Id;
        var jti2 = Parse(t2).Id;
        jti1.Should().NotBe(jti2);
    }

    [Fact]
    public void GenerateAccessToken_ExpiresAtReflectsConfiguredDuration()
    {
        var opts = new JwtOptions { SecretKey = "relativa-test-secret-key-32bytes!!", Issuer = "relativa-test", Audience = "relativa-client", ExpirationMinutes = 30 };
        var sut = Build(opts);
        var before = DateTime.UtcNow;

        var (_, expiresAt) = sut.GenerateAccessToken(SampleUser());

        var after = DateTime.UtcNow;
        expiresAt.Should().BeOnOrAfter(before.AddMinutes(30))
                          .And.BeOnOrBefore(after.AddMinutes(30));
    }

    [Fact]
    public void GenerateAccessToken_TokenPassesFullSignatureIssuerAndAudienceValidation()
    {
        var (token, _) = Build().GenerateAccessToken(SampleUser());

        var act = () => Parse(token);

        act.Should().NotThrow();
    }

    [Fact]
    public void GenerateAccessToken_TokenFailsValidationWhenSignedWithDifferentKey()
    {
        var (token, _) = Build().GenerateAccessToken(SampleUser());

        var wrongKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("wrong-secret-key-32-bytes-long!!!"));
        var handler = new JwtSecurityTokenHandler();

        var act = () => handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = wrongKey,
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
        }, out _);

        act.Should().Throw<SecurityTokenException>();
    }

    [Fact]
    public void GenerateAccessToken_IssuerAndAudienceMatchConfiguration()
    {
        var (token, _) = Build().GenerateAccessToken(SampleUser());

        var opts = DefaultOptions();
        var jwt = Parse(token);
        jwt.Issuer.Should().Be(opts.Issuer);
        jwt.Audiences.Should().Contain(opts.Audience);
    }
}
