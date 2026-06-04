namespace Relativa.Authentication.Application.Exceptions;

public sealed class InvalidTwoFactorCodeException()
    : Exception("Invalid two-factor authentication code.");
