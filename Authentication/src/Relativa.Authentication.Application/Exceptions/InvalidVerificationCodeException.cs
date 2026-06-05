namespace Relativa.Authentication.Application.Exceptions;

public sealed class InvalidVerificationCodeException()
    : Exception("Invalid or expired verification code.");
