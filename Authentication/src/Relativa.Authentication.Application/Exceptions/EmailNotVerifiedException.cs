namespace Relativa.Authentication.Application.Exceptions;

public sealed class EmailNotVerifiedException(string email)
    : Exception("Email address has not been verified.")
{
    public string Email { get; } = email;
}
