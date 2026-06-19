using Relativa.Authentication.Application.DTOs;

namespace Relativa.Authentication.Application.Interfaces;

public interface ISupportService
{
    Task SendContactAsync(SupportContactRequest request, string? clientIp = null, CancellationToken ct = default);
}
