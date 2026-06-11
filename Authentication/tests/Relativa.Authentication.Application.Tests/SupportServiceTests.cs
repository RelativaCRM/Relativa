using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Options;
using static Microsoft.Extensions.Options.Options;
using Moq;
using Relativa.Authentication.Application.DTOs;
using Relativa.Authentication.Application.Exceptions;
using Relativa.Authentication.Application.Interfaces;
using Relativa.Authentication.Application.Options;
using Relativa.Authentication.Application.Services;
using Relativa.Authentication.Application.Validators;
using Relativa.Authentication.Domain.Interfaces;
using Xunit;

namespace Relativa.Authentication.Application.Tests;

public sealed class SupportServiceTests
{
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly Mock<IEmailLocalizer> _localizer = new();
    private readonly Mock<IEmailRateLimiter> _rateLimiter = new();
    private readonly SupportContactRequestValidator _validator = new();
    private readonly SupportOptions _options = new() { DevEmail = "support@relativa.io" };

    public SupportServiceTests()
    {
        _localizer.Setup(l => l.Get(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string? _, string key, object[] _) => key);
        _rateLimiter.Setup(r => r.TryConsume(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>())).Returns(true);
    }

    private SupportService Build() =>
        new(_emailSender.Object, _localizer.Object, _rateLimiter.Object, _validator, Create(_options));

    private static SupportContactRequest ValidRequest(string name = "Ivan") =>
        new(name, "ivan@relativa.io", "Need help", "My message body");

    [Fact]
    public async Task SendContactAsync_ValidRequest_SendsToDevEmail()
    {
        await Build().SendContactAsync(ValidRequest(), "203.0.113.5");

        _emailSender.Verify(e => e.SendAsync("support@relativa.io", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendContactAsync_InvalidRequest_ThrowsValidation()
    {
        var request = new SupportContactRequest("Ivan", "not-an-email", "", "");

        var act = () => Build().SendContactAsync(request);

        await act.Should().ThrowAsync<ValidationException>();
        _emailSender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendContactAsync_DevEmailNotConfigured_ThrowsConfiguration()
    {
        _options.DevEmail = string.Empty;

        var act = () => Build().SendContactAsync(ValidRequest());

        await act.Should().ThrowAsync<ConfigurationException>();
    }

    [Fact]
    public async Task SendContactAsync_DevEmailMalformed_ThrowsConfiguration()
    {
        _options.DevEmail = "not-an-email";

        var act = () => Build().SendContactAsync(ValidRequest());

        await act.Should().ThrowAsync<ConfigurationException>();
    }

    [Fact]
    public async Task SendContactAsync_IpRateLimited_ThrowsRateLimit()
    {
        _rateLimiter.Setup(r => r.TryConsume(It.Is<string>(k => k.StartsWith("support-ip:")), It.IsAny<int>(), It.IsAny<TimeSpan>())).Returns(false);

        var act = () => Build().SendContactAsync(ValidRequest(), "203.0.113.5");

        await act.Should().ThrowAsync<RateLimitExceededException>();
    }

    [Fact]
    public async Task SendContactAsync_NoClientIp_SkipsRateLimitAndSends()
    {
        await Build().SendContactAsync(ValidRequest(), clientIp: null);

        _emailSender.Verify(e => e.SendAsync(_options.DevEmail, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _rateLimiter.Verify(r => r.TryConsume(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TimeSpan>()), Times.Never);
    }
}
