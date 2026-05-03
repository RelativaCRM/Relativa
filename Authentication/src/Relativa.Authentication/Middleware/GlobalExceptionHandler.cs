using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Relativa.Authentication.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is DbUpdateException dbEx && IsPostgresUniqueViolation(dbEx))
        {
            return await WriteConflictAsync(httpContext, cancellationToken,
                "A record with this unique value already exists.");
        }

        var (statusCode, title, detail) = exception switch
        {
            ValidationException ve => (StatusCodes.Status400BadRequest, "Validation Failed",
                string.Join("; ", ve.Errors.Select(e => e.ErrorMessage))),
            UnauthorizedAccessException ue => (StatusCodes.Status401Unauthorized, "Unauthorized", ue.Message),
            KeyNotFoundException knf => (StatusCodes.Status404NotFound, "Not Found", knf.Message),
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

    private static bool IsPostgresUniqueViolation(Exception exception)
    {
        for (var ex = exception; ex != null; ex = ex.InnerException!)
        {
            if (ex is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation)
                return true;
        }

        return false;
    }

    private async ValueTask<bool> WriteConflictAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken,
        string detail)
    {
        logger.LogWarning("Handled exception: unique violation");

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        await httpContext.Response.WriteAsJsonAsync(new
        {
            status = StatusCodes.Status409Conflict,
            title = "Conflict",
            detail
        }, cancellationToken);

        return true;
    }
}
