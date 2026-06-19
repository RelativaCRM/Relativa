using FluentAssertions;
using Relativa.Authentication.Infrastructure.Services;
using Xunit;

namespace Relativa.Authentication.Application.Tests;

public sealed class BcryptPasswordHasherTests
{
    private readonly BcryptPasswordHasher _sut = new();

    [Fact]
    public void Hash_ReturnsBcryptFormattedString()
    {
        var hash = _sut.Hash("plain-password");
        hash.Should().StartWith("$2");
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var hash = _sut.Hash("my-secret-123");
        _sut.Verify("my-secret-123", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = _sut.Hash("correct-password");
        _sut.Verify("wrong-password", hash).Should().BeFalse();
    }
}
