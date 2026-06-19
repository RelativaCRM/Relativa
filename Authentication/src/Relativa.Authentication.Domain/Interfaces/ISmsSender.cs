namespace Relativa.Authentication.Domain.Interfaces;

public interface ISmsSender
{
    Task SendAsync(string toPhone, string message, CancellationToken ct = default);
}
