using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Relativa.Authentication.Application.Exceptions;

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
            AuthException ax => (ax.StatusCode, TitleForStatus(ax.StatusCode), ax.Message),
            EmailNotVerifiedException env => (StatusCodes.Status403Forbidden, "Forbidden", env.Message),
            TwoFactorRequiredException tfr => (StatusCodes.Status403Forbidden, "Forbidden", tfr.Message),
            InvalidTwoFactorCodeException itf => (StatusCodes.Status400BadRequest, "Bad Request", itf.Message),
            EmailAddressTakenException eat => (StatusCodes.Status409Conflict, "Conflict", eat.Message),
            InvalidVerificationCodeException ivc => (StatusCodes.Status400BadRequest, "Bad Request", ivc.Message),
            RateLimitExceededException rle => (StatusCodes.Status429TooManyRequests, "Too Many Requests", rle.Message),
            UnauthorizedAccessException ue => (StatusCodes.Status401Unauthorized, "Unauthorized", ue.Message),
            KeyNotFoundException knf => (StatusCodes.Status404NotFound, "Not Found", knf.Message),
            InvalidOperationException ioe => (StatusCodes.Status409Conflict, "Conflict", ioe.Message),
            ArgumentException ae => (StatusCodes.Status400BadRequest, "Bad Request", ae.Message),
            ConfigurationException => (StatusCodes.Status500InternalServerError, "Internal Server Error",
                "An unexpected error occurred."),
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

        object? errors = exception is ValidationException vex
            ? vex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => new { code = e.ErrorCode, message = e.ErrorMessage }))
            : null;

        var code = exception switch
        {
            AuthException ax => ax.Code,
            EmailNotVerifiedException => "email_not_verified",
            TwoFactorRequiredException => "two_factor_required",
            InvalidTwoFactorCodeException => "invalid_two_factor_code",
            EmailAddressTakenException => "email_address_taken",
            InvalidVerificationCodeException => "invalid_verification_code",
            RateLimitExceededException => "rate_limit_exceeded",
            _ => null
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new
        {
            status = statusCode,
            title,
            detail,
            code,
            errors
        }, cancellationToken);

        return true;
    }

    private static string TitleForStatus(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status429TooManyRequests => "Too Many Requests",
        _ => "Error"
    };

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
