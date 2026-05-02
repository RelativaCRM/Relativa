namespace Relativa.Audit.Exceptions;

public sealed class ForbiddenAccessException(string message) : Exception(message);
