namespace Relativa.Authentication.Application.Interfaces;

public interface IEmailRateLimiter
{
    bool TryConsume(string key, int permitLimit, TimeSpan window);
}
