using FluentAssertions;
using OtpNet;
using Relativa.Authentication.Infrastructure.Services;
using Xunit;

namespace Relativa.Authentication.Application.Tests;

public sealed class OtpNetTotpProviderTests
{
    private readonly OtpNetTotpProvider _sut = new();

    [Fact]
    public void GenerateSecret_ProducesDecodableBase32Secret()
    {
        var secret = _sut.GenerateSecret();

        secret.Should().NotBeNullOrWhiteSpace();
        var act = () => Base32Encoding.ToBytes(secret);
        act.Should().NotThrow();
    }

    [Fact]
    public void GenerateSecret_ProducesUniqueSecrets()
    {
        _sut.GenerateSecret().Should().NotBe(_sut.GenerateSecret());
    }

    [Fact]
    public void VerifyCode_CurrentCodeForSecret_ReturnsTrue()
    {
        var secret = _sut.GenerateSecret();
        var currentCode = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();

        _sut.VerifyCode(secret, currentCode).Should().BeTrue();
    }

    [Fact]
    public void VerifyCode_WrongCode_ReturnsFalse()
    {
        var secret = _sut.GenerateSecret();

        _sut.VerifyCode(secret, "000000").Should().BeFalse();
    }

    [Theory]
    [InlineData("", "123456")]
    [InlineData("SECRET", "")]
    [InlineData(" ", "123456")]
    public void VerifyCode_EmptyInput_ReturnsFalse(string secret, string code)
    {
        _sut.VerifyCode(secret, code).Should().BeFalse();
    }

    [Fact]
    public void VerifyCode_MalformedSecret_ReturnsFalseWithoutThrowing()
    {
        _sut.VerifyCode("not-valid-base32!!!", "123456").Should().BeFalse();
    }

    [Fact]
    public void BuildOtpAuthUri_ContainsSecretIssuerAndStandardParameters()
    {
        var uri = _sut.BuildOtpAuthUri("SECRET123", "user@relativa.io", "Relativa");

        uri.Should().StartWith("otpauth://totp/");
        uri.Should().Contain("secret=SECRET123");
        uri.Should().Contain("issuer=Relativa");
        uri.Should().Contain("digits=6");
        uri.Should().Contain("period=30");
    }
}
