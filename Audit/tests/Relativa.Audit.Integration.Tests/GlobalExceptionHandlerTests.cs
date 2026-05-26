using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Relativa.Audit.Application.Exceptions;
using Relativa.Audit.Middleware;
using Xunit;

namespace Relativa.Audit.Integration.Tests;

public sealed class GlobalExceptionHandlerTests
{
    private static GlobalExceptionHandler Sut() =>
        new(NullLogger<GlobalExceptionHandler>.Instance);

    private static async Task<(int StatusCode, JsonElement Body)> InvokeAsync(Exception exception)
    {
        var context = new DefaultHttpContext();
        var body    = new MemoryStream();
        context.Response.Body = body;

        var result = await Sut().TryHandleAsync(context, exception, CancellationToken.None);
        result.Should().BeTrue();

        body.Seek(0, SeekOrigin.Begin);
        var doc = await JsonDocument.ParseAsync(body);
        return (context.Response.StatusCode, doc.RootElement);
    }

    [Fact]
    public async Task ValidationException_Returns400_WithDetails()
    {
        var ve = new ValidationException(new[]
        {
            new ValidationFailure("Name", "Required"),
            new ValidationFailure("Email", "Invalid format"),
        });

        var (code, body) = await InvokeAsync(ve);

        code.Should().Be(StatusCodes.Status400BadRequest);
        body.GetProperty("status").GetInt32().Should().Be(400);
        body.GetProperty("detail").GetString().Should().Contain("Name").And.Contain("Email");
    }

    [Fact]
    public async Task ArgumentException_Returns400()
    {
        var (code, body) = await InvokeAsync(new ArgumentException("Bad value"));

        code.Should().Be(StatusCodes.Status400BadRequest);
        body.GetProperty("detail").GetString().Should().Be("Bad value");
    }

    [Fact]
    public async Task KeyNotFoundException_Returns404()
    {
        var (code, body) = await InvokeAsync(new KeyNotFoundException("Not found: 42"));

        code.Should().Be(StatusCodes.Status404NotFound);
        body.GetProperty("detail").GetString().Should().Contain("42");
    }

    [Fact]
    public async Task ForbiddenAccessException_Returns403()
    {
        var (code, body) = await InvokeAsync(new ForbiddenAccessException("Access denied"));

        code.Should().Be(StatusCodes.Status403Forbidden);
        body.GetProperty("title").GetString().Should().Be("Forbidden");
    }

    [Fact]
    public async Task UnauthorizedAccessException_Returns403()
    {
        var (code, body) = await InvokeAsync(new UnauthorizedAccessException("Unauthorized"));

        code.Should().Be(StatusCodes.Status403Forbidden);
        body.GetProperty("title").GetString().Should().Be("Forbidden");
    }

    [Fact]
    public async Task InvalidOperationException_Returns409()
    {
        var (code, body) = await InvokeAsync(new InvalidOperationException("Conflict state"));

        code.Should().Be(StatusCodes.Status409Conflict);
        body.GetProperty("title").GetString().Should().Be("Conflict");
    }

    [Fact]
    public async Task GenericException_Returns500()
    {
        var (code, body) = await InvokeAsync(new Exception("Something exploded"));

        code.Should().Be(StatusCodes.Status500InternalServerError);
        body.GetProperty("title").GetString().Should().Be("Internal Server Error");
    }

    [Fact]
    public async Task TryHandleAsync_AlwaysReturnsTrue()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var result = await Sut().TryHandleAsync(context, new Exception("any"), CancellationToken.None);

        result.Should().BeTrue();
    }
}
