using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace Relativa.Authentication.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = exception switch
        {
            ValidationException ve => (StatusCodes.Status400BadRequest, "Validation Failed",
                string.Join("; ", ve.Errors.Select(e => e.ErrorMessage))),
            UnauthorizedAccessException ue => (StatusCodes.Status401Unauthorized, "Unauthorized", ue.Message),
            InvalidOperationException ioe => (StatusCodes.Status409Conflict, "Conflict", ioe.Message),
            ArgumentException ae => (StatusCodes.Status400BadRequest, "Bad Request", ae.Message),
            JsonException je => (StatusCodes.Status400BadRequest, "Bad Request",
                $"Invalid JSON in request body: {je.Message}"),
            BadHttpRequestException bhr => (StatusCodes.Status400BadRequest, "Bad Request", bhr.Message),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error",
                "An unexpected error occurred.")
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception");
        else
            logger.LogWarning(exception, "Handled exception: {Title}", title);

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new
        {
            status = statusCode,
            title,
            detail
        }, cancellationToken);

        return true;
    }
}
