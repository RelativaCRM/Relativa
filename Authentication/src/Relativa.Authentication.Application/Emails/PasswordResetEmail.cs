namespace Relativa.Authentication.Application.Emails;

public static class PasswordResetEmail
{
    public const string Subject = "Reset your Relativa password";

    public static (string Html, string Text) Build(string firstName, string resetLink, TimeSpan expiry)
    {
        var expiryLabel = expiry.TotalHours >= 1
            ? $"{(int)expiry.TotalHours} hour{((int)expiry.TotalHours == 1 ? "" : "s")}"
            : $"{(int)expiry.TotalMinutes} minute{((int)expiry.TotalMinutes == 1 ? "" : "s")}";

        return (
            Html: $"""
<!DOCTYPE html>
<html>
<head><meta charset="utf-8" /><meta name="viewport" content="width=device-width,initial-scale=1.0" /></head>
<body style="margin:0;padding:0;background-color:#f8fafc;font-family:'Inter',ui-sans-serif,system-ui,sans-serif;">
  <table width="100%" cellpadding="0" cellspacing="0" style="background-color:#f8fafc;padding:40px 16px;">
    <tr><td align="center">
      <div style="margin-bottom:24px;">
        <span style="font-size:22px;font-weight:700;color:#0f172a;letter-spacing:-0.5px;">Relativa</span>
      </div>
      <table width="100%" cellpadding="0" cellspacing="0" style="max-width:480px;background-color:#ffffff;border:1px solid #e2e8f0;">
        <tr><td style="background-color:#2563eb;height:4px;font-size:0;line-height:0;">&nbsp;</td></tr>
        <tr><td style="padding:36px 40px;">
          <p style="margin:0 0 20px;font-size:20px;font-weight:700;color:#0f172a;line-height:1.3;">Reset your password</p>
          <p style="margin:0 0 20px;font-size:14px;color:#475569;line-height:1.6;">
            Hi {firstName},<br /><br />
            We received a request to reset the password for your Relativa account.
            Click the button below to choose a new password.
            This link expires in <strong>{expiryLabel}</strong>.
          </p>
          <table cellpadding="0" cellspacing="0" style="margin-bottom:24px;">
            <tr>
              <td style="background-color:#2563eb;">
                <a href="{resetLink}" style="display:inline-block;padding:12px 28px;font-size:14px;font-weight:600;color:#ffffff;text-decoration:none;letter-spacing:0.01em;">Reset password</a>
              </td>
            </tr>
          </table>
          <p style="margin:0 0 8px;font-size:13px;color:#94a3b8;line-height:1.6;">Or copy and paste this link into your browser:</p>
          <p style="margin:0 0 24px;font-size:12px;color:#2563eb;word-break:break-all;line-height:1.6;">{resetLink}</p>
          <hr style="border:none;border-top:1px solid #e2e8f0;margin:0 0 20px;" />
          <p style="margin:0;font-size:12px;color:#94a3b8;line-height:1.6;">
            If you didn&apos;t request a password reset, you can safely ignore this email. Your password will not change.
          </p>
        </td></tr>
      </table>
      <p style="margin-top:24px;font-size:11px;color:#94a3b8;text-align:center;">&copy; Relativa CRM Platform</p>
    </td></tr>
  </table>
</body>
</html>
""",
            Text: $"""
Hi {firstName},

We received a request to reset the password for your Relativa account.
Click the link below to choose a new password. This link expires in {expiryLabel}.

{resetLink}

If you didn't request a password reset, you can safely ignore this email.
"""
        );
    }
}
