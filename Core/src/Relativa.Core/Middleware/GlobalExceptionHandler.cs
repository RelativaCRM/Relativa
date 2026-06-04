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
        var (statusCode, title, detail, code) = exception switch
        {
            AppException ax               => (ax.StatusCode, TitleForStatus(ax.StatusCode), ax.Message == ax.Code ? null : ax.Message, ax.Code),
            ValidationException ve        => (StatusCodes.Status400BadRequest,  "Validation Failed",    BuildValidationDetail(ve), (string?)null),
            ArgumentException ae          => (StatusCodes.Status400BadRequest,  "Bad Request",          ae.Message, (string?)null),
            KeyNotFoundException ke       => (StatusCodes.Status404NotFound,    "Not Found",            ke.Message, (string?)null),
            UnauthorizedAccessException ue => (StatusCodes.Status403Forbidden,   "Forbidden",            ue.Message, (string?)null),
            ForbiddenAccessException fe   => (StatusCodes.Status403Forbidden,     "Forbidden",          fe.Message, (string?)null),
            InvalidOperationException ioe => (StatusCodes.Status409Conflict,    "Conflict",             ioe.Message, (string?)null),
            DbUpdateException { InnerException: PostgresException { SqlState: "23505" } }
                                          => (StatusCodes.Status409Conflict,    "Conflict",             "A record with this value already exists.", (string?)null),
            _                             => (StatusCodes.Status500InternalServerError, "Internal Server Error", "An unexpected error occurred.", (string?)null)
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception");
        else
            logger.LogWarning(exception, "Handled exception: {Title}", title);

        object? errors = exception is ValidationException vex
            ? vex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new { code = e.ErrorCode, message = e.ErrorMessage }))
            : null;

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new
        {
            status = statusCode,
            code,
            title,
            detail,
            errors
        }, cancellationToken);

        return true;
    }

    private static string TitleForStatus(int status) => status switch
    {
        StatusCodes.Status400BadRequest          => "Bad Request",
        StatusCodes.Status401Unauthorized        => "Unauthorized",
        StatusCodes.Status403Forbidden           => "Forbidden",
        StatusCodes.Status404NotFound            => "Not Found",
        StatusCodes.Status409Conflict            => "Conflict",
        StatusCodes.Status422UnprocessableEntity => "Unprocessable Entity",
        _                                        => "Error"
    };

    private static string BuildValidationDetail(ValidationException ve)
    {
        var errors = ve.Errors
            .Select(e => $"{e.PropertyName}: {e.ErrorMessage}")
            .ToList();
        return string.Join("; ", errors);
    }
}
