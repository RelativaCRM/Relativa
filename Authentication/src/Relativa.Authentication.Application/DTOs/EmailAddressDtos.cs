namespace Relativa.Authentication.Application.DTOs;

public sealed record UserEmailDto(string Address, bool IsPrimary, bool IsVerified, string Source);

public sealed record AddEmailRequest(string Address);

public sealed record VerifyEmailAddressRequest(string Address, string Code);

public sealed record EmailAddressRequest(string Address);
