using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Relativa.Graph;
using Xunit;

namespace Relativa.Graph.Integration.Tests;

public sealed class GraphGlobalExceptionHandlerTests
{
    private static async Task<int> StatusForAsync(Exception exception)
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(Environments.Development);
        var services = new ServiceCollection();
        services.AddSingleton(env);
        services.AddLogging();
        var ctx = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        ctx.Response.Body = new MemoryStream();

        var handled = await new GraphGlobalExceptionHandler(NullLogger<GraphGlobalExceptionHandler>.Instance)
            .TryHandleAsync(ctx, exception, CancellationToken.None);

        handled.Should().BeTrue("the global handler claims responsibility for every exception");
        return ctx.Response.StatusCode;
    }

    [Fact]
    public async Task UnauthorizedAccess_MapsTo401() =>
        (await StatusForAsync(new UnauthorizedAccessException())).Should().Be((int)HttpStatusCode.Unauthorized);

    [Fact]
    public async Task ForbiddenAccess_MapsTo403() =>
        (await StatusForAsync(new ForbiddenAccessException("nope"))).Should().Be((int)HttpStatusCode.Forbidden);

    [Fact]
    public async Task Argument_MapsTo400() =>
        (await StatusForAsync(new ArgumentException("bad"))).Should().Be((int)HttpStatusCode.BadRequest);

    [Fact]
    public async Task WorkspaceNotFound_MapsTo404() =>
        (await StatusForAsync(new WorkspaceNotFoundException(7))).Should().Be((int)HttpStatusCode.NotFound);

    [Fact]
    public async Task InvalidOperation_MapsTo409() =>
        (await StatusForAsync(new InvalidOperationException("conflict"))).Should().Be((int)HttpStatusCode.Conflict);

    [Fact]
    public async Task Timeout_MapsTo504() =>
        (await StatusForAsync(new TimeoutException())).Should().Be((int)HttpStatusCode.GatewayTimeout);

    [Fact]
    public async Task UnexpectedException_MapsTo500() =>
        (await StatusForAsync(new Exception("boom"))).Should().Be((int)HttpStatusCode.InternalServerError);
}
