namespace Relativa.Authentication.Application.Exceptions;

public sealed class RateLimitExceededException(string message) : Exception(message);
