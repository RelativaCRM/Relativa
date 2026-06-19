namespace Relativa.Authentication.Application.Exceptions;

public sealed class EmailAddressTakenException()
    : Exception("This email address is already in use.");
