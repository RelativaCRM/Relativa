namespace Relativa.Core.Application.Exceptions;

public sealed class AppException(string code, int statusCode, string? message = null)
    : Exception(message ?? code)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
