namespace Relativa.Authentication.Application.Exceptions;

public sealed class TwoFactorRequiredException()
    : Exception("A two-factor authentication code is required.");
