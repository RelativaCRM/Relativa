using Microsoft.AspNetCore.Diagnostics;

namespace Relativa.Core.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = exception switch
        {
            InvalidOperationException ioe => (StatusCodes.Status409Conflict, "Conflict", ioe.Message),
            ArgumentException ae => (StatusCodes.Status400BadRequest, "Bad Request", ae.Message),
            UnauthorizedAccessException ue => (StatusCodes.Status401Unauthorized, "Unauthorized", ue.Message),
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
