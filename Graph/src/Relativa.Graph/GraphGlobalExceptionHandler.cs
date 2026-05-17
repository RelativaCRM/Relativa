using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace Relativa.Graph;

public sealed class GraphGlobalExceptionHandler(ILogger<GraphGlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, logAsError) = exception switch
        {
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, false),
            ArgumentException           => (StatusCodes.Status400BadRequest,   false),
            KeyNotFoundException        => (StatusCodes.Status404NotFound,      false),
            InvalidOperationException   => (StatusCodes.Status409Conflict,      false),
            TimeoutException            => (StatusCodes.Status504GatewayTimeout, false),
            _                           => (StatusCodes.Status500InternalServerError, true),
        };

        if (logAsError)
            logger.LogError(exception, "Unhandled Graph exception");
        else
            logger.LogWarning(exception, "Handled Graph exception");

        await Results.Problem(
                title: "Request failed",
                detail: httpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment()
                    ? exception.Message
                    : "An error occurred processing the request.",
                statusCode: status)
            .ExecuteAsync(httpContext);

        return true;
    }
}
