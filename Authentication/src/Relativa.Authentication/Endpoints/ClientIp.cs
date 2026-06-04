using Microsoft.AspNetCore.Http;

namespace Relativa.Authentication.Endpoints;

public static class ClientIp
{
    public static string? From(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }
}
