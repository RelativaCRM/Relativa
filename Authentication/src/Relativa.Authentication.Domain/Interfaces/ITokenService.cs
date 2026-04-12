using Relativa.Authentication.Domain.Entities;

namespace Relativa.Authentication.Domain.Interfaces;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) GenerateAccessToken(User user, IEnumerable<string> permissions);
}
