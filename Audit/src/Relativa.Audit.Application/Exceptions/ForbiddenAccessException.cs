namespace Relativa.Audit.Application.Exceptions;

public sealed class ForbiddenAccessException(string message) : Exception(message);
