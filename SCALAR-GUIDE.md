# Relativa API Reference — Scalar Guide

This guide walks through every step needed to open the Scalar interactive API docs and execute the live endpoints that are currently available in the application.

---

## Table of contents

1. [Prerequisites](#1-prerequisites)
2. [Start the stack](#2-start-the-stack)
3. [Open Scalar](#3-open-scalar)
4. [Register a new user](#4-register-a-new-user)
5. [Log in and obtain a JWT](#5-log-in-and-obtain-a-jwt)
6. [Authorise subsequent requests](#6-authorise-subsequent-requests)
7. [Health checks](#7-health-checks)
8. [Available endpoints reference](#8-available-endpoints-reference)
9. [Tips and troubleshooting](#9-tips-and-troubleshooting)

---

## 1. Prerequisites

- Docker Desktop running (see `DOCKER-BUILD.md` for the full setup guide).
- The stack is up: `docker compose up -d`.
- A browser (Scalar runs entirely in the browser — no extra tooling required).

---

## 2. Start the stack

```powershell
# PowerShell
docker compose up -d
```

```bash
# Bash / WSL
docker compose up -d
```

Wait until all containers report **healthy**:

```bash
docker compose ps
```

The services you need for the API docs are:

| Service | Internal port | Exposed on host |
|---------|---------------|-----------------|
| Gateway | 8080 | `http://localhost:8080` |
| Authentication | 8081 | `http://localhost:8081` |

---

## 3. Open Scalar

Each service hosts its own Scalar UI. The path is always:

```
http://localhost:<port>/scalar/v1
```

| Service | Scalar URL |
|---------|-----------|
| **Authentication** | `http://localhost:8081/scalar/v1` |
| **Gateway** | `http://localhost:8080/scalar/v1` |

> **Recommended starting point:** open the **Authentication** Scalar (`http://localhost:8081/scalar/v1`). It lists every endpoint of the auth service with full request/response schemas.

The raw OpenAPI JSON (useful for import into Postman, Insomnia, etc.) is at:

```
http://localhost:8081/openapi/v1.json
```

---

## 4. Register a new user

**Endpoint:** `POST /api/v1/auth/register`

This endpoint is **public** — no token required.

### Steps in Scalar

1. In the left sidebar, expand **Authentication** → click **Register**.
2. Click **Try it**.
3. In the **Request body** section, fill in:

```json
{
  "firstName": "Jane",
  "lastName": "Doe",
  "email": "jane.doe@example.com",
  "password": "S3cur3P@ss!",
  "roleId": null
}
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `firstName` | string | yes | |
| `lastName` | string | yes | |
| `email` | string | yes | Must be a valid email address |
| `password` | string | yes | Must meet the minimum complexity rules |
| `roleId` | integer | no | Leave `null` to get the default role |

4. Click **Send**.

### Expected response — `201 Created`

```json
{
  "id": 1,
  "email": "jane.doe@example.com",
  "firstName": "Jane",
  "lastName": "Doe",
  "roleName": "User"
}
```

The `Location` response header points to the user resource:

```
Location: /api/v1/auth/users/1
```

### Error responses

| Status | When |
|--------|------|
| `400 Validation Problem` | A required field is missing or fails validation (e.g. invalid email format, password too short) |
| `409 Conflict` | A user with that email already exists |

---

## 5. Log in and obtain a JWT

**Endpoint:** `POST /api/v1/auth/login`

This endpoint is **public** — no token required.

### Steps in Scalar

1. In the left sidebar, expand **Authentication** → click **Login**.
2. Click **Try it**.
3. Enter the credentials you registered with:

```json
{
  "email": "jane.doe@example.com",
  "password": "S3cur3P@ss!"
}
```

| Field | Type | Required |
|-------|------|----------|
| `email` | string | yes |
| `password` | string | yes |

4. Click **Send**.

### Expected response — `200 OK`

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2026-04-13T10:30:00Z"
}
```

| Field | Type | Notes |
|-------|------|-------|
| `accessToken` | string | Bearer JWT — copy this value |
| `expiresAt` | string (ISO 8601) | Token expiry timestamp (UTC) |

### Error responses

| Status | When |
|--------|------|
| `400 Validation Problem` | Missing or malformed fields |
| `401 Unauthorized` | Wrong email or password |

---

## 6. Authorise subsequent requests

Most routes that go through the **Gateway** require the JWT from step 5.

### In the Authentication Scalar UI

1. Click the **Authorize** button (padlock icon, top-right of the Scalar page) or look for the **Security** section at the top of the page.
2. Enter the token in the **Bearer token** field — paste the raw `accessToken` value (without the `Bearer ` prefix; Scalar adds it automatically).
3. Click **Authorize** / **Save**.

All subsequent **Try it** requests will now include `Authorization: Bearer <token>`.

### Gateway-prefixed paths (when using `http://localhost:8080/scalar/v1`)

The Gateway proxies auth endpoints under the `/auth` prefix:

| Direct (auth service) | Via gateway |
|-----------------------|-------------|
| `POST /api/v1/auth/login` | `POST /auth/api/v1/auth/login` |
| `POST /api/v1/auth/register` | `POST /auth/api/v1/auth/register` |

The `/auth/api/v1/auth/login` and `/auth/api/v1/auth/register` paths are marked **Anonymous** in the gateway — no JWT needed to call them via the gateway either.

---

## 7. Health checks

Both services expose a health check endpoint that does **not** require a token. These are useful for verifying the stack is alive before running other calls.

| Service | URL | Expected response |
|---------|-----|-------------------|
| Authentication | `http://localhost:8081/health` | `200 OK` with health status JSON |
| Gateway | `http://localhost:8080/health` | `200 OK` with `{"status":"Healthy","service":"relativa-gateway"}` |
| Auth via gateway | `http://localhost:8080/auth/health` | same as auth health |

To test a health check in Scalar: open the Gateway Scalar (`http://localhost:8080/scalar/v1`), find **Health** in the sidebar, and click **Try it** → **Send**.

---

## 8. Available endpoints reference

### Authentication service — `http://localhost:8081`

| Method | Path | Auth | Summary |
|--------|------|------|---------|
| `POST` | `/api/v1/auth/register` | None | Register a new user |
| `POST` | `/api/v1/auth/login` | None | Authenticate and receive JWT |
| `GET` | `/health` | None | Health check (DB connectivity) |
| `GET` | `/scalar/v1` | None | Scalar interactive docs |
| `GET` | `/openapi/v1.json` | None | Raw OpenAPI spec |

### Gateway — `http://localhost:8080`

| Method | Path | Auth | Proxied to |
|--------|------|------|------------|
| `POST` | `/auth/api/v1/auth/login` | None | Auth service |
| `POST` | `/auth/api/v1/auth/register` | None | Auth service |
| `GET` | `/auth/health` | None | Auth service |
| `GET` | `/core/health` | None | Core service |
| `GET` | `/health` | None | Gateway itself |
| `GET` | `/auth/{**rest}` | Bearer JWT | Auth service |
| `GET` | `/core/{**rest}` | Bearer JWT | Core service |
| `GET` | `/graph/{**rest}` | Bearer JWT | Graph service |
| `GET` | `/audit/{**rest}` | Bearer JWT | Audit service |
| `GET` | `/ml/{**rest}` | Bearer JWT | ML service |
| `GET` | `/scalar/v1` | None | Gateway Scalar docs |
| `GET` | `/openapi/v1.json` | None | Raw OpenAPI spec |

---

## 9. Tips and troubleshooting

**Scalar page is blank or shows a network error**
- Confirm the service is running: `docker compose ps`.
- Check service logs: `docker compose logs -f auth`.
- Verify you are using `http://` not `https://` — the local stack does not use TLS.

**401 on login**
- Double-check email and password against what you registered.
- Passwords are case-sensitive.

**409 on register**
- That email is already in the database. Use a different email or reset the database: `docker compose down -v && docker compose up -d`.

**Token expired (401 on protected routes)**
- Log in again to get a fresh `accessToken` and re-enter it in the Scalar **Authorize** dialog.

**Scalar shows no endpoints**
- The OpenAPI JSON may have failed to load. Navigate directly to `http://localhost:8081/openapi/v1.json` and confirm you get valid JSON (not an error page).

**Port already in use**
- Update the relevant port variable in `.env` (e.g. change `CLIENT_PORT`) and restart: `docker compose up -d`.
