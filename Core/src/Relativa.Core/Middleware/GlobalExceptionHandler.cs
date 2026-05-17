using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Relativa.Core.Application.Exceptions;

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
            ValidationException ve        => (StatusCodes.Status400BadRequest,  "Validation Failed",    BuildValidationDetail(ve)),
            ArgumentException ae          => (StatusCodes.Status400BadRequest,  "Bad Request",          ae.Message),
            KeyNotFoundException ke       => (StatusCodes.Status404NotFound,    "Not Found",            ke.Message),
            UnauthorizedAccessException ue when string.Equals(ue.Message, "Access denied", StringComparison.Ordinal)
                                         => (StatusCodes.Status403Forbidden,   "Forbidden",            ue.Message),
            UnauthorizedAccessException ue => (StatusCodes.Status401Unauthorized, "Unauthorized",       ue.Message),
            ForbiddenAccessException fe   => (StatusCodes.Status403Forbidden,     "Forbidden",          fe.Message),
            InvalidOperationException ioe => (StatusCodes.Status409Conflict,    "Conflict",             ioe.Message),
            DbUpdateException { InnerException: PostgresException { SqlState: "23505" } }
                                          => (StatusCodes.Status409Conflict,    "Conflict",             "A record with this value already exists."),
            _                             => (StatusCodes.Status500InternalServerError, "Internal Server Error", "An unexpected error occurred.")
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

    private static string BuildValidationDetail(ValidationException ve)
    {
        var errors = ve.Errors
            .Select(e => $"{e.PropertyName}: {e.ErrorMessage}")
            .ToList();
        return string.Join("; ", errors);
    }
}
