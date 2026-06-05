using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Relativa.Authentication.Application.DTOs;
using Relativa.Authentication.Application.Interfaces;
using Relativa.Authentication.Application.Options;

namespace Relativa.Authentication.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/auth")
            .WithTags("Authentication");

        group.MapPost("/login", async (LoginRequestDto request, IAuthService authService, CancellationToken ct) =>
        {
            var result = await authService.LoginAsync(request, ct);
            return Results.Ok(result);
        })
        .WithName("Login")
        .WithSummary("Authenticate a user and return a JWT access token")
        .AllowAnonymous()
        .Produces<LoginResponseDto>()
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/oauth/{provider}", async (string provider, OAuthLoginRequestDto request, IAuthService authService, IOptions<OAuthOptions> oauthOptions, CancellationToken ct) =>
        {
            if (!oauthOptions.Value.Providers.ContainsKey(provider))
            {
                return Results.Problem(
                    title: "Bad Request",
                    detail: $"Unsupported OAuth provider '{provider}'.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var result = await authService.OAuthLoginAsync(provider, request, ct);
            return Results.Ok(result);
        })
        .WithName("OAuthLogin")
        .WithSummary("Authenticate via an external identity provider and return a JWT access token")
        .AllowAnonymous()
        .Produces<LoginResponseDto>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/exists", async (string email, IAuthService authService, CancellationToken ct) =>
        {
            var exists = await authService.EmailExistsAsync(email, ct);
            return Results.Ok(new { exists });
        })
        .WithName("EmailExists")
        .WithSummary("Check whether an account exists for the given email")
        .AllowAnonymous()
        .Produces(StatusCodes.Status200OK);

        group.MapPost("/register", async (RegisterRequestDto request, IAuthService authService, CancellationToken ct) =>
        {
            var result = await authService.RegisterAsync(request, ct);
            return Results.Created($"/api/v1/auth/users/{result.Id}", result);
        })
        .WithName("Register")
        .WithSummary("Register a new user")
        .AllowAnonymous()
        .Produces<RegisterResponseDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/verify-email", async (VerifyEmailRequest request, IEmailVerificationService verificationService, HttpContext http, CancellationToken ct) =>
        {
            await verificationService.VerifyAsync(request.Email, request.Code, ClientIp.From(http), ct);
            return Results.NoContent();
        })
        .WithName("VerifyEmail")
        .WithSummary("Verify a user's email address using the emailed code")
        .AllowAnonymous()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status429TooManyRequests);

        group.MapPost("/resend-verification", async (ResendVerificationRequest request, IEmailVerificationService verificationService, HttpContext http, CancellationToken ct) =>
        {
            var channel = string.Equals(request.Channel, "sms", StringComparison.OrdinalIgnoreCase)
                ? VerificationChannel.Sms
                : VerificationChannel.Email;
            await verificationService.ResendAsync(request.Email, channel, ClientIp.From(http), ct);
            return Results.NoContent();
        })
        .WithName("ResendVerification")
        .WithSummary("Resend the verification code via the chosen channel if the address is registered and unverified")
        .AllowAnonymous()
        .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/verification-channels", async (string email, IEmailVerificationService verificationService, CancellationToken ct) =>
        {
            var sms = await verificationService.SmsAvailableAsync(email, ct);
            return Results.Ok(new { email = true, sms });
        })
        .WithName("VerificationChannels")
        .WithSummary("Report which verification channels are available for an address")
        .AllowAnonymous()
        .Produces(StatusCodes.Status200OK);

        group.MapGet("/me", async (IAuthService authService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var sub = user.FindFirstValue("sub")
                ?? throw new UnauthorizedAccessException("Missing sub claim.");
            var userId = int.Parse(sub);
            var result = await authService.GetProfileAsync(userId, ct);
            return Results.Ok(result);
        })
        .WithName("GetProfile")
        .WithSummary("Get the current user's profile")
        .RequireAuthorization()
        .Produces<UserProfileDto>()
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPatch("/me", async (UpdateMyProfileRequest request, IAuthService authService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var sub = user.FindFirstValue("sub")
                ?? throw new UnauthorizedAccessException("Missing sub claim.");
            var userId = int.Parse(sub);
            var result = await authService.UpdateMyProfileAsync(userId, request, ct);
            return Results.Ok(result);
        })
        .WithName("UpdateMyProfile")
        .WithSummary("Update the current user's profile (first and last name)")
        .RequireAuthorization()
        .Produces<UserProfileDto>()
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/me/settings", async (IUserSettingsService settingsService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var sub = user.FindFirstValue("sub")
                ?? throw new UnauthorizedAccessException("Missing sub claim.");
            var userId = int.Parse(sub);
            var result = await settingsService.GetMySettingsAsync(userId, ct);
            return Results.Ok(result);
        })
        .WithName("GetMySettings")
        .WithSummary("Get the current user's personal settings")
        .RequireAuthorization()
        .Produces<UserSettingsDto>()
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPatch("/me/settings", async (UpdateUserSettingsRequest request, IUserSettingsService settingsService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var sub = user.FindFirstValue("sub")
                ?? throw new UnauthorizedAccessException("Missing sub claim.");
            var userId = int.Parse(sub);
            var result = await settingsService.UpdateMySettingsAsync(userId, request, ct);
            return Results.Ok(result);
        })
        .WithName("UpdateMySettings")
        .WithSummary("Update the current user's personal settings")
        .RequireAuthorization()
        .Produces<UserSettingsDto>()
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/me/2fa", async (ITwoFactorService twoFactorService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var result = await twoFactorService.GetStatusAsync(CurrentUserId(user), ct);
            return Results.Ok(result);
        })
        .WithName("GetTwoFactorStatus")
        .WithSummary("Report whether two-factor authentication is enabled for the current user")
        .RequireAuthorization()
        .Produces<TwoFactorStatusDto>()
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/me/2fa/setup", async (ITwoFactorService twoFactorService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var result = await twoFactorService.StartSetupAsync(CurrentUserId(user), ct);
            return Results.Ok(result);
        })
        .WithName("StartTwoFactorSetup")
        .WithSummary("Generate a TOTP secret and otpauth URI for the current user to scan")
        .RequireAuthorization()
        .Produces<TwoFactorSetupDto>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/me/2fa/enable", async (TwoFactorCodeRequest request, ITwoFactorService twoFactorService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var result = await twoFactorService.EnableAsync(CurrentUserId(user), request.Code, ct);
            return Results.Ok(result);
        })
        .WithName("EnableTwoFactor")
        .WithSummary("Confirm a TOTP code to enable two-factor authentication and receive backup codes and a master code")
        .RequireAuthorization()
        .Produces<TwoFactorEnableResultDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/me/2fa/disable", async (TwoFactorCodeRequest request, ITwoFactorService twoFactorService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            await twoFactorService.DisableAsync(CurrentUserId(user), request.Code, ct);
            return Results.NoContent();
        })
        .WithName("DisableTwoFactor")
        .WithSummary("Disable two-factor authentication after confirming a current code")
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/me/2fa/backup-codes", async (TwoFactorCodeRequest request, ITwoFactorService twoFactorService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var result = await twoFactorService.RegenerateBackupCodesAsync(CurrentUserId(user), request.Code, ct);
            return Results.Ok(result);
        })
        .WithName("RegenerateBackupCodes")
        .WithSummary("Regenerate the set of one-time backup codes after confirming a current code")
        .RequireAuthorization()
        .Produces<TwoFactorBackupCodesDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/me/2fa/master-code", async (TwoFactorCodeRequest request, ITwoFactorService twoFactorService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var result = await twoFactorService.RegenerateMasterCodeAsync(CurrentUserId(user), request.Code, ct);
            return Results.Ok(result);
        })
        .WithName("RegenerateMasterCode")
        .WithSummary("Regenerate the persistent master recovery code after confirming a current code")
        .RequireAuthorization()
        .Produces<TwoFactorMasterCodeDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/me/emails", async (IEmailAddressService emailService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var result = await emailService.ListAsync(CurrentUserId(user), ct);
            return Results.Ok(result);
        })
        .WithName("ListEmails")
        .WithSummary("List the current user's email addresses")
        .RequireAuthorization()
        .Produces<IReadOnlyList<UserEmailDto>>()
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/me/emails", async (AddEmailRequest request, IEmailAddressService emailService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            await emailService.AddAsync(CurrentUserId(user), request.Address, ct);
            return Results.NoContent();
        })
        .WithName("AddEmail")
        .WithSummary("Add an email address and send a verification code to it")
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/me/emails/verify", async (VerifyEmailAddressRequest request, IEmailAddressService emailService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            await emailService.VerifyAsync(CurrentUserId(user), request.Address, request.Code, ct);
            return Results.NoContent();
        })
        .WithName("VerifyEmailAddress")
        .WithSummary("Verify an added email address using its code")
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/me/emails/resend", async (EmailAddressRequest request, IEmailAddressService emailService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            await emailService.ResendAsync(CurrentUserId(user), request.Address, ct);
            return Results.NoContent();
        })
        .WithName("ResendEmailCode")
        .WithSummary("Resend the verification code for an added email address")
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/me/emails/primary", async (EmailAddressRequest request, IEmailAddressService emailService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            await emailService.SetPrimaryAsync(CurrentUserId(user), request.Address, ct);
            return Results.NoContent();
        })
        .WithName("SetPrimaryEmail")
        .WithSummary("Set a verified email address as the primary login email")
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/me/emails/remove", async (EmailAddressRequest request, IEmailAddressService emailService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            await emailService.RemoveAsync(CurrentUserId(user), request.Address, ct);
            return Results.NoContent();
        })
        .WithName("RemoveEmail")
        .WithSummary("Remove a non-primary email address")
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/me/connections/{provider}", async (string provider, OAuthLoginRequestDto request, IAuthService authService, IOptions<OAuthOptions> oauthOptions, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (!oauthOptions.Value.Providers.ContainsKey(provider))
            {
                return Results.Problem(
                    title: "Bad Request",
                    detail: $"Unsupported OAuth provider '{provider}'.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            await authService.LinkProviderAsync(CurrentUserId(user), provider, request, ct);
            return Results.NoContent();
        })
        .WithName("LinkProvider")
        .WithSummary("Link an external sign-in provider to the current account")
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict);

        group.MapDelete("/me", async (IAuthService authService, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var sub = user.FindFirstValue("sub")
                ?? throw new UnauthorizedAccessException("Missing sub claim.");
            var userId = int.Parse(sub);
            await authService.DeleteMyAccountAsync(userId, ct);
            return Results.NoContent();
        })
        .WithName("DeleteMyAccount")
        .WithSummary("Archive the current user's account")
        .RequireAuthorization()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/forgot-password", async (ForgotPasswordRequest request, IAuthService authService, CancellationToken ct) =>
        {
            await authService.ForgotPasswordAsync(request.Email, ct);
            return Results.NoContent();
        })
        .WithName("ForgotPassword")
        .WithSummary("Send a password reset email if the address is registered")
        .AllowAnonymous()
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem();

        group.MapGet("/reset-password/validate", async (string token, IAuthService authService, CancellationToken ct) =>
        {
            await authService.ValidateResetTokenAsync(token, ct);
            return Results.NoContent();
        })
        .WithName("ValidateResetToken")
        .WithSummary("Check whether a reset token is still valid without consuming it")
        .AllowAnonymous()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/reset-password", async (ResetPasswordRequest request, IAuthService authService, CancellationToken ct) =>
        {
            await authService.ResetPasswordAsync(request.Token, request.NewPassword, ct);
            return Results.NoContent();
        })
        .WithName("ResetPassword")
        .WithSummary("Reset a user's password using a valid reset token")
        .AllowAnonymous()
        .Produces(StatusCodes.Status204NoContent)
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status400BadRequest);

        return group;
    }

    private static int CurrentUserId(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("Missing sub claim.");
        return int.Parse(sub);
    }
}
