namespace Relativa.Core.Application.Exceptions;

public sealed class ForbiddenAccessException(string message) : Exception(message);
