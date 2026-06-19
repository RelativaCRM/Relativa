using FluentAssertions;
using Microsoft.Extensions.Options;
using static Microsoft.Extensions.Options.Options;
using Moq;
using Relativa.Authentication.Application.Options;
using Relativa.Authentication.Domain.Interfaces;
using Relativa.Authentication.Infrastructure.Services;
using Xunit;

namespace Relativa.Authentication.Application.Tests;

public sealed class SmtpSmsSenderTests
{
    private readonly Mock<IEmailSender> _emailSender = new();

    private SmtpSmsSender Build(string sink = "sink@relativa.local") =>
        new(_emailSender.Object, Create(new SmsOptions { SinkEmail = sink }));

    [Fact]
    public async Task SendAsync_RoutesSmsToTheConfiguredSinkEmailWithRawMessageAsText()
    {
        await Build("sink@relativa.local").SendAsync("+380501234567", "Your code is 123456");

        _emailSender.Verify(e => e.SendAsync(
            "sink@relativa.local",
            It.Is<string>(subject => subject.Contains("+380501234567")),
            It.IsAny<string>(),
            "Your code is 123456",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_HtmlEncodesTheMessageBodyToPreventInjection()
    {
        string? capturedHtml = null;
        _emailSender
            .Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback((string _, string _, string html, string? _, CancellationToken _) => capturedHtml = html)
            .Returns(Task.CompletedTask);

        await Build().SendAsync("+380501234567", "<script>alert(1)</script>");

        capturedHtml.Should().NotContain("<script>", "the SMS body must be HTML-encoded so it cannot inject markup into the sink email");
        capturedHtml.Should().Contain("&lt;script&gt;");
    }
}
