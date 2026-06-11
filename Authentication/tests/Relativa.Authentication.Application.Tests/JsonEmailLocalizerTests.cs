using FluentAssertions;
using Relativa.Authentication.Infrastructure.Services;
using Xunit;

namespace Relativa.Authentication.Application.Tests;

public sealed class JsonEmailLocalizerTests
{
    private readonly JsonEmailLocalizer _sut = new();

    [Fact]
    public void Get_UnknownKey_FallsBackToKeyItself()
    {
        _sut.Get("en", "nonexistent.key").Should().Be("nonexistent.key");
    }

    [Fact]
    public void Get_NullLocale_DoesNotThrowAndFallsBack()
    {
        _sut.Get(null, "some.key").Should().Be("some.key");
    }

    [Fact]
    public void Get_RegionLocale_FallsBackThroughBaseLanguageToKey()
    {
        _sut.Get("uk-UA", "another.key").Should().Be("another.key");
    }

    [Fact]
    public void Get_TemplateWithArguments_FormatsThemIntoTheKeyFallback()
    {
        _sut.Get("en", "{0} - {1}", "Hello", "World").Should().Be("Hello - World");
    }

    [Fact]
    public void Get_NoArguments_ReturnsTemplateUnformatted()
    {
        _sut.Get("en", "plain text with no placeholders").Should().Be("plain text with no placeholders");
    }
}
