namespace Relativa.Authentication.Application.Interfaces;

public interface IEmailLocalizer
{
    string Get(string? locale, string key, params object[] args);
}
