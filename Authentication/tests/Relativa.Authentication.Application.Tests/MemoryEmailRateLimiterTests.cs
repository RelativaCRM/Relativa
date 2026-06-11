using FluentAssertions;
using Relativa.Authentication.Infrastructure.Services;
using Xunit;

namespace Relativa.Authentication.Application.Tests;

public sealed class MemoryEmailRateLimiterTests
{
    private readonly MemoryEmailRateLimiter _sut = new();

    [Fact]
    public void TryConsume_WithinLimit_AllowsUpToPermitLimit()
    {
        var window = TimeSpan.FromHours(1);

        _sut.TryConsume("key", 3, window).Should().BeTrue();
        _sut.TryConsume("key", 3, window).Should().BeTrue();
        _sut.TryConsume("key", 3, window).Should().BeTrue();
    }

    [Fact]
    public void TryConsume_ExceedingLimit_ReturnsFalse()
    {
        var window = TimeSpan.FromHours(1);
        for (var i = 0; i < 3; i++)
        {
            _sut.TryConsume("key", 3, window);
        }

        _sut.TryConsume("key", 3, window).Should().BeFalse();
    }

    [Fact]
    public void TryConsume_DistinctKeys_AreCountedIndependently()
    {
        var window = TimeSpan.FromHours(1);

        _sut.TryConsume("a", 1, window).Should().BeTrue();
        _sut.TryConsume("a", 1, window).Should().BeFalse();
        _sut.TryConsume("b", 1, window).Should().BeTrue();
    }

    [Fact]
    public void TryConsume_ExpiredWindow_ResetsTheCount()
    {
        _sut.TryConsume("key", 1, TimeSpan.Zero).Should().BeTrue();
        _sut.TryConsume("key", 1, TimeSpan.Zero).Should().BeTrue();
    }
}
