namespace Relativa.Authentication.Application.Validators;

public static class EmailValidation
{
    public const string Pattern = @"^[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}$";
    public const string Message = "A valid email address is required.";
}
