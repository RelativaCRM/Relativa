namespace Relativa.Authentication.Application;

public static class EmailNormalizer
{
    public static string Normalize(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
