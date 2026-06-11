using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static Microsoft.Extensions.Options.Options;
using Moq;
using Relativa.Authentication.Application.Options;
using Relativa.Authentication.Infrastructure.Services;
using Xunit;

namespace Relativa.Authentication.Application.Tests;

public sealed class HttpSmsSenderTests
{
    [Fact]
    public async Task SendAsync_NoEndpointConfigured_ReturnsWithoutThrowing()
    {
        var sut = new HttpSmsSender(Create(new SmsOptions { Endpoint = null }), Mock.Of<ILogger<HttpSmsSender>>());

        var act = () => sut.SendAsync("+380501234567", "code 123");

        await act.Should().NotThrowAsync(
            "with no SMS endpoint configured the sender must log and no-op rather than fault the verification flow");
    }
}
