using Microsoft.AspNetCore.Diagnostics;

namespace Relativa.Gateway.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled gateway exception");

        httpContext.Response.StatusCode = StatusCodes.Status502BadGateway;
        await httpContext.Response.WriteAsJsonAsync(new
        {
            status = StatusCodes.Status502BadGateway,
            title = "Bad Gateway",
            detail = "An error occurred while processing your request."
        }, cancellationToken);

        return true;
    }
}
