namespace Relativa.Authentication.Application.Exceptions;

public class AuthException(string code, int statusCode, string message) : Exception(message)
{
    public string Code { get; } = code;

    public int StatusCode { get; } = statusCode;
}
