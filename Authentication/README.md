# relativa-auth

Authentication microservice on **ASP.NET Core 10**. Solution at the root; Web API project at `src/Relativa.Authentication/`.

## Port

- **8081** (direct) — also reachable at `http://localhost:8080/auth/` through the gateway.

## Stack

| Package | Purpose |
|---|---|
| `Microsoft.AspNetCore.Authentication.JwtBearer`, `System.IdentityModel.Tokens.Jwt` | JWT issuance and validation |
| `BCrypt.Net-Next` | Password hashing (bcrypt, cost factor 12) |
| `MailKit`, `MimeKit` | SMTP email delivery |
| `FluentValidation` | Request validation |
| `Serilog` | Structured logging to console and rolling file |

## API Endpoints

All endpoints are under `/api/v1/auth`. Interactive docs are available at `http://localhost:8081/scalar/v1` when running locally.

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/login` | — | Authenticate; returns JWT access token |
| `POST` | `/register` | — | Create a new user account |
| `GET` | `/me` | JWT | Get current user profile |
| `PATCH` | `/me` | JWT | Update first and last name |
| `DELETE` | `/me` | JWT | Archive current user account |
| `POST` | `/forgot-password` | — | Send password-reset link to email |
| `GET` | `/reset-password/validate` | — | Check that a reset token is valid and not expired |
| `POST` | `/reset-password` | — | Set new password using a valid token |

## Password Reset Flow

1. Client `POST /forgot-password` with `{ "email": "..." }`.
2. If the email exists the service generates a secure random token, stores its bcrypt hash and a 1-hour expiry on the `users` row, then sends an email containing a link to the frontend reset page.
3. The frontend visits `/reset-password?token=<token>`, which calls `GET /reset-password/validate?token=<token>` to confirm the token before showing the form.
4. On submit the frontend calls `POST /reset-password` with `{ "token": "...", "newPassword": "..." }`. The service validates the token, updates the password, and clears the token columns.

Token expiry: **1 hour**. Tokens are single-use and invalidated immediately after a successful reset.

The endpoint always returns `200 OK` for `forgot-password` regardless of whether the email exists — this prevents user enumeration.

## Database Migration

Migration `20260508210000_AddPasswordResetToken` adds two nullable columns to the `users` table:

```sql
password_reset_token          TEXT NULL
password_reset_token_expires_at TIMESTAMPTZ NULL
```

These are applied automatically by the `migration` service on `docker compose up`. To run manually:

```bash
dotnet run --project Migration/src/Relativa.Migration
```

## Email Setup

The service uses **SMTP** for email delivery via `MailKit`. Two modes are supported.

### Local development — MailHog (default)

MailHog is included in `docker-compose.yaml` and requires no credentials. All outgoing emails are intercepted and visible at `http://localhost:8025`.

The default `.env.example` already points at MailHog:

```env
SMTP_HOST=mailhog
SMTP_PORT=1025
SMTP_USERNAME=
SMTP_PASSWORD=
SMTP_FROM_ADDRESS=noreply@relativa.com
SMTP_FROM_NAME=Relativa
SMTP_USE_SSL=false
```

No extra configuration is needed — just `docker compose up`.

### Production / team staging — Resend

[Resend](https://resend.com) provides an SMTP bridge that works with MailKit without any SDK changes.

**Steps:**

1. Create a free account at [resend.com](https://resend.com).
2. Go to **API Keys** → **Create API Key**. Give it a name (e.g. `relativa-local`) and copy the key.
3. Go to **Domains** and add/verify your sending domain, or use Resend's shared `onboarding@resend.dev` address for initial testing (limited to your own verified email as recipient).
4. Update your `.env`:

```env
SMTP_HOST=smtp.resend.com
SMTP_PORT=465
SMTP_USERNAME=resend
SMTP_PASSWORD=re_xxxxxxxxxxxxxxxxxxxxxxxxxxxx   # your Resend API key
SMTP_FROM_ADDRESS=noreply@yourdomain.com
SMTP_FROM_NAME=Relativa
SMTP_USE_SSL=true
```

5. Restart the auth service:

```bash
docker compose up -d --no-deps auth
```

**Testing a reset email end-to-end:**

1. Ensure the stack is up and pointed at Resend (or MailHog).
2. Open `http://localhost:3000/login` and click **Forgot password?**.
3. Enter the email of a seeded account (e.g. `admin@relativa.com`).
4. Check MailHog at `http://localhost:8025` (local) or your inbox (Resend).
5. Click the link in the email — it should open `/reset-password?token=...`.
6. Enter a new password. On success the browser redirects to `/login?reset=success`.

**Smoke-testing without the UI:**

```bash
# 1. Trigger the email
curl -s -X POST http://localhost:8080/auth/api/v1/auth/forgot-password \
  -H 'Content-Type: application/json' \
  -d '{"email":"admin@relativa.com"}'

# 2. Read the token from MailHog (if using local dev)
curl -s http://localhost:8025/api/v2/messages | jq '.items[0].Content.Body'

# 3. Validate the token (copy from the link in the email body)
curl -s "http://localhost:8080/auth/api/v1/auth/reset-password/validate?token=<TOKEN>"

# 4. Reset the password
curl -s -X POST http://localhost:8080/auth/api/v1/auth/reset-password \
  -H 'Content-Type: application/json' \
  -d '{"token":"<TOKEN>","newPassword":"NewPass123!"}'
```

## Configuration Reference

All values are set via environment variables in `.env` (mapped in `docker-compose.yaml`) or directly in `appsettings.json` for local-only runs.

| Key | Default | Description |
|---|---|---|
| `Jwt:SecretKey` | *(required)* | Signing key, min 32 chars — must match gateway |
| `Jwt:Issuer` | `relativa-auth` | JWT `iss` claim — must match gateway |
| `Jwt:Audience` | `relativa` | JWT `aud` claim — must match gateway |
| `Jwt:ExpirationMinutes` | `60` | Access token lifetime in minutes |
| `Smtp:Host` | `mailhog` | SMTP server hostname |
| `Smtp:Port` | `1025` | SMTP port |
| `Smtp:Username` | *(empty)* | SMTP username; leave blank for MailHog |
| `Smtp:Password` | *(empty)* | SMTP password or API key |
| `Smtp:FromAddress` | `noreply@relativa.com` | Sender address |
| `Smtp:FromName` | `Relativa` | Sender display name |
| `Smtp:UseSsl` | `false` | Set `true` for SSL/TLS (port 465) |
| `APP_FRONTEND_BASE_URL` | `http://localhost:3000` | Base URL used in reset-link emails |

## Demo Accounts

Seeded on first migration run. All share the same password.

| Email | Password | Org role | Workspace role |
|---|---|---|---|
| `admin@relativa.com` | `Demo1234!` | org_owner | ws_admin |
| `ivan.f@relativa.com` | `Demo1234!` | org_member | ws_manager |
| `lesya.u@relativa.com` | `Demo1234!` | org_member | ws_analyst |

## Commands

```bash
dotnet restore Relativa.Authentication.sln
dotnet build Relativa.Authentication.sln
dotnet run --project src/Relativa.Authentication
```
