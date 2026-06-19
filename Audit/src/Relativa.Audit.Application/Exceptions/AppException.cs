namespace Relativa.Audit.Application.Exceptions;

public class AppException(string code, int statusCode, string message) : Exception(message)
{
    public string Code { get; } = code;

    public int StatusCode { get; } = statusCode;
}
