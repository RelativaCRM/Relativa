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
7. [Create a workspace](#7-create-a-workspace)
8. [Invite a member to the workspace](#8-invite-a-member-to-the-workspace)
9. [Accept an invitation](#9-accept-an-invitation)
10. [Manage workspace members](#10-manage-workspace-members)
11. [Manage roles and permissions](#11-manage-roles-and-permissions)
12. [Health checks](#12-health-checks)
13. [Available endpoints reference](#13-available-endpoints-reference)
14. [Tips and troubleshooting](#14-tips-and-troubleshooting)

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
| Core | 8082 | `http://localhost:8082` |

---

## 3. Open Scalar

Each service hosts its own Scalar UI. The path is always:

```
http://localhost:<port>/scalar/v1
```

| Service | Scalar URL |
|---------|-----------|
| **Authentication** | `http://localhost:8081/scalar/v1` |
| **Core** | `http://localhost:8082/scalar/v1` |
| **Gateway** | `http://localhost:8080/scalar/v1` |

> **Recommended starting point:** open the **Authentication** Scalar (`http://localhost:8081/scalar/v1`) to register and log in, then switch to the **Core** Scalar (`http://localhost:8082/scalar/v1`) to test workspace and RBAC endpoints.

The raw OpenAPI JSON (useful for import into Postman, Insomnia, etc.) is at:

```
http://localhost:8081/openapi/v1.json   # Auth
http://localhost:8082/openapi/v1.json   # Core
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
  "password": "S3cur3P@ss!"
}
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `firstName` | string | yes | |
| `lastName` | string | yes | |
| `email` | string | yes | Must be a valid email address |
| `password` | string | yes | Min 8 characters |

4. Click **Send**.

### Expected response — `201 Created`

```json
{
  "id": 4,
  "email": "jane.doe@example.com",
  "firstName": "Jane",
  "lastName": "Doe"
}
```

The `Location` response header points to the user resource:

```
Location: /api/v1/auth/users/4
```

> **Note:** Newly registered users have no role (`RoleId` is null). A user gains a role when they create or join a workspace.

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

The JWT contains `sub` (user ID) and `email` claims. Role and permissions are **not** in the token — they are resolved per-request by Core based on workspace membership.

### Error responses

| Status | When |
|--------|------|
| `400 Validation Problem` | Missing or malformed fields |
| `401 Unauthorized` | Wrong email or password |

---

## 6. Authorise subsequent requests

Most routes that go through the **Gateway** require the JWT from step 5. All Core endpoints require it.

### In the Core Scalar UI (`http://localhost:8082/scalar/v1`)

1. Click the **Authorize** button (padlock icon, top-right of the Scalar page) or look for the **Security** section at the top of the page.
2. Enter the token in the **Bearer token** field — paste the raw `accessToken` value (without the `Bearer ` prefix; Scalar adds it automatically).
3. Click **Authorize** / **Save**.

All subsequent **Try it** requests will now include `Authorization: Bearer <token>`.

### Gateway-prefixed paths (when using `http://localhost:8080/scalar/v1`)

The Gateway proxies endpoints under service prefixes:

| Direct (service) | Via gateway |
|-----------------------|-------------|
| `POST /api/v1/auth/login` | `POST /auth/api/v1/auth/login` |
| `POST /api/v1/auth/register` | `POST /auth/api/v1/auth/register` |
| `POST /api/v1/workspaces` | `POST /core/api/v1/workspaces` |

The `/auth/api/v1/auth/login` and `/auth/api/v1/auth/register` paths are marked **Anonymous** in the gateway — no JWT needed. All `/core/...` paths require a valid JWT.

---

## 7. Create a workspace

**Endpoint:** `POST /api/v1/workspaces`

Requires a valid JWT. Any authenticated user can create a workspace.

### Steps in Scalar (Core)

1. Open the Core Scalar UI: `http://localhost:8082/scalar/v1`.
2. Make sure you have authorised (step 6).
3. Find **Workspaces** → **CreateWorkspace** in the sidebar.
4. Click **Try it** and enter:

```json
{
  "name": "My Sales Team"
}
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `name` | string | yes | Max 200 characters |
| `organizationId` | integer | no | Optional; links to existing organization |

5. Click **Send**.

### Expected response — `201 Created`

```json
{
  "id": 3,
  "name": "My Sales Team",
  "memberCount": 1,
  "userRole": "admin"
}
```

The creating user is automatically added as a member with the **admin** system role.

### List your workspaces

**Endpoint:** `GET /api/v1/workspaces`

Returns all workspaces where the authenticated user is a member:

```json
[
  {
    "id": 3,
    "name": "My Sales Team",
    "memberCount": 1,
    "userRole": "admin"
  }
]
```

---

## 8. Invite a member to the workspace

**Endpoint:** `POST /api/v1/workspaces/{workspaceId}/invitations`

Requires the `can_assign_roles` permission in the workspace (admin role has this by default).

### Steps in Scalar (Core)

1. Find **Invitations** → **InviteMember** in the sidebar.
2. Set the `workspaceId` path parameter (e.g. `3`).
3. Enter the request body:

```json
{
  "email": "colleague@example.com",
  "roleId": 2
}
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `email` | string | yes | Email of the person to invite |
| `roleId` | integer | yes | Role to assign on acceptance. Use `GET .../roles` to see available role IDs |

4. Click **Send**.

### Expected response — `201 Created`

```json
{
  "id": 1,
  "email": "colleague@example.com",
  "roleName": "sales_manager",
  "status": "Pending",
  "expiresAt": "2026-04-20T12:00:00Z"
}
```

### List pending invitations

**Endpoint:** `GET /api/v1/workspaces/{workspaceId}/invitations`

Returns all pending invitations for the workspace.

---

## 9. Accept an invitation

**Endpoint:** `POST /api/v1/invitations/accept`

The invited user must register (if new) and log in first, then accept the invitation using the token.

### Steps

1. Register a second user with the invited email (step 4).
2. Log in as that user (step 5).
3. Authorise the Core Scalar UI with the new user's token (step 6).
4. Find **Invitations** → **AcceptInvitation** in the sidebar.
5. Enter:

```json
{
  "token": "<the invitation token from the invite response or database>"
}
```

> **Note:** In production, the token would be delivered via email link. For testing, you can look up the token in the database: `SELECT token FROM workspace_invitations WHERE email = 'colleague@example.com';` using pgAdmin at `http://localhost:5050`.

### Expected response — `200 OK`

```json
{
  "message": "Invitation accepted."
}
```

The user is now a member of the workspace with the role specified in the invitation.

---

## 10. Manage workspace members

All member endpoints are under `/api/v1/workspaces/{workspaceId}/members`.

### List members

**Endpoint:** `GET /api/v1/workspaces/{workspaceId}/members`

Requires workspace membership. Returns:

```json
[
  {
    "userId": 4,
    "firstName": "Jane",
    "lastName": "Doe",
    "email": "jane.doe@example.com",
    "roleName": "admin",
    "joinedAt": "2026-04-13T12:00:00Z"
  },
  {
    "userId": 5,
    "firstName": "Bob",
    "lastName": "Smith",
    "email": "colleague@example.com",
    "roleName": "sales_manager",
    "joinedAt": "2026-04-13T12:05:00Z"
  }
]
```

### Change a member's role

**Endpoint:** `PUT /api/v1/workspaces/{workspaceId}/members/{userId}/role`

Requires `can_assign_roles` permission.

```json
{
  "roleId": 3
}
```

Returns `204 No Content` on success.

### Remove a member

**Endpoint:** `DELETE /api/v1/workspaces/{workspaceId}/members/{userId}`

Requires `can_assign_roles` permission, or the user can remove themselves.

Returns `204 No Content` on success.

---

## 11. Manage roles and permissions

### List available permissions

**Endpoint:** `GET /api/v1/permissions`

Any authenticated user. Returns the full list of system permissions:

```json
[
  { "id": 1, "name": "can_manage_settings" },
  { "id": 2, "name": "can_assign_roles" },
  { "id": 3, "name": "can_edit_deals" },
  { "id": 4, "name": "can_view_analytics" }
]
```

### List roles in a workspace

**Endpoint:** `GET /api/v1/workspaces/{workspaceId}/roles`

Requires workspace membership. Returns system roles (available in all workspaces) plus any custom roles created in this workspace:

```json
[
  {
    "id": 1,
    "name": "admin",
    "isSystem": true,
    "permissions": [
      { "id": 1, "name": "can_manage_settings" },
      { "id": 2, "name": "can_assign_roles" },
      { "id": 3, "name": "can_edit_deals" },
      { "id": 4, "name": "can_view_analytics" }
    ]
  },
  {
    "id": 2,
    "name": "sales_manager",
    "isSystem": true,
    "permissions": [
      { "id": 3, "name": "can_edit_deals" }
    ]
  }
]
```

### Create a custom role

**Endpoint:** `POST /api/v1/workspaces/{workspaceId}/roles`

Requires `can_manage_settings` permission.

```json
{
  "name": "team_lead",
  "permissionIds": [2, 3, 4]
}
```

### Expected response — `201 Created`

```json
{
  "id": 4,
  "name": "team_lead",
  "isSystem": false,
  "permissions": [
    { "id": 2, "name": "can_assign_roles" },
    { "id": 3, "name": "can_edit_deals" },
    { "id": 4, "name": "can_view_analytics" }
  ]
}
```

### Update a custom role

**Endpoint:** `PUT /api/v1/workspaces/{workspaceId}/roles/{roleId}`

Requires `can_manage_settings`. System roles cannot be modified.

```json
{
  "name": "senior_lead",
  "permissionIds": [1, 2, 3, 4]
}
```

Returns `204 No Content` on success.

### Delete (archive) a custom role

**Endpoint:** `DELETE /api/v1/workspaces/{workspaceId}/roles/{roleId}`

Requires `can_manage_settings`. System roles cannot be deleted. Returns `204 No Content` on success.

---

## 12. Health checks

All services expose a health check endpoint that does **not** require a token. These are useful for verifying the stack is alive before running other calls.

| Service | URL | Expected response |
|---------|-----|-------------------|
| Authentication | `http://localhost:8081/health` | `200 OK` with health status JSON |
| Core | `http://localhost:8082/health` | `200 OK` with health status JSON |
| Gateway | `http://localhost:8080/health` | `200 OK` with `{"status":"Healthy","service":"relativa-gateway"}` |
| Auth via gateway | `http://localhost:8080/auth/health` | same as auth health |
| Core via gateway | `http://localhost:8080/core/health` | same as core health |

To test a health check in Scalar: open the Gateway Scalar (`http://localhost:8080/scalar/v1`), find **Health** in the sidebar, and click **Try it** → **Send**.

---

## 13. Available endpoints reference

### Authentication service — `http://localhost:8081`

| Method | Path | Auth | Summary |
|--------|------|------|---------|
| `POST` | `/api/v1/auth/register` | None | Register a new user (no role assigned) |
| `POST` | `/api/v1/auth/login` | None | Authenticate and receive JWT |
| `GET` | `/health` | None | Health check (DB connectivity) |
| `GET` | `/scalar/v1` | None | Scalar interactive docs |
| `GET` | `/openapi/v1.json` | None | Raw OpenAPI spec |

### Core service — `http://localhost:8082`

| Method | Path | Auth | Summary |
|--------|------|------|---------|
| `POST` | `/api/v1/workspaces` | JWT | Create a workspace (creator becomes admin) |
| `GET` | `/api/v1/workspaces` | JWT | List user's workspaces |
| `GET` | `/api/v1/workspaces/{id}` | JWT + membership | Get workspace details |
| `PUT` | `/api/v1/workspaces/{id}` | JWT + `can_manage_settings` | Update workspace name |
| `DELETE` | `/api/v1/workspaces/{id}` | JWT + admin | Archive workspace |
| `GET` | `/api/v1/workspaces/{id}/members` | JWT + membership | List workspace members |
| `PUT` | `/api/v1/workspaces/{id}/members/{userId}/role` | JWT + `can_assign_roles` | Change member role |
| `DELETE` | `/api/v1/workspaces/{id}/members/{userId}` | JWT + `can_assign_roles` / self | Remove member |
| `POST` | `/api/v1/workspaces/{id}/invitations` | JWT + `can_assign_roles` | Invite user by email |
| `GET` | `/api/v1/workspaces/{id}/invitations` | JWT + `can_assign_roles` | List pending invitations |
| `DELETE` | `/api/v1/workspaces/{id}/invitations/{invId}` | JWT + `can_assign_roles` | Cancel invitation |
| `POST` | `/api/v1/invitations/accept` | JWT + matching email | Accept invitation |
| `GET` | `/api/v1/workspaces/{id}/roles` | JWT + membership | List roles (system + custom) |
| `POST` | `/api/v1/workspaces/{id}/roles` | JWT + `can_manage_settings` | Create custom role |
| `PUT` | `/api/v1/workspaces/{id}/roles/{roleId}` | JWT + `can_manage_settings` | Update custom role |
| `DELETE` | `/api/v1/workspaces/{id}/roles/{roleId}` | JWT + `can_manage_settings` | Archive custom role |
| `GET` | `/api/v1/permissions` | JWT | List all available permissions |
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
| `*` | `/auth/{**rest}` | Bearer JWT | Auth service |
| `*` | `/core/{**rest}` | Bearer JWT | Core service |
| `*` | `/graph/{**rest}` | Bearer JWT | Graph service |
| `*` | `/audit/{**rest}` | Bearer JWT | Audit service |
| `*` | `/ml/{**rest}` | Bearer JWT | ML service |
| `GET` | `/scalar/v1` | None | Gateway Scalar docs |
| `GET` | `/openapi/v1.json` | None | Raw OpenAPI spec |

---

## 14. Tips and troubleshooting

**Scalar page is blank or shows a network error**
- Confirm the service is running: `docker compose ps`.
- Check service logs: `docker compose logs -f auth` or `docker compose logs -f core`.
- Verify you are using `http://` not `https://` — the local stack does not use TLS.

**401 on login**
- Double-check email and password against what you registered.
- Passwords are case-sensitive.

**409 on register**
- That email is already in the database. Use a different email or reset the database: `docker compose down -v && docker compose up -d`.

**Token expired (401 on protected routes)**
- Log in again to get a fresh `accessToken` and re-enter it in the Scalar **Authorize** dialog.

**403 / "You do not have the '...' permission" on Core endpoints**
- Your user does not have the required permission in the target workspace. Check your membership and role with `GET /api/v1/workspaces/{id}/members`.
- Workspace admin role has all permissions by default.

**"You are not a member of this workspace"**
- The `sub` claim in your JWT does not match any `WorkspaceMember` row for that workspace. Create a workspace first, or accept an invitation.

**How do I find invitation tokens for testing?**
- Connect to pgAdmin at `http://localhost:5050` and query: `SELECT id, email, token, status FROM workspace_invitations;`.
- Or check the response body of the `POST .../invitations` call — the token is not returned in the DTO, but the invitation ID is. Query by ID for the token.

**Scalar shows no endpoints**
- The OpenAPI JSON may have failed to load. Navigate directly to `http://localhost:8082/openapi/v1.json` and confirm you get valid JSON (not an error page).

**Port already in use**
- Update the relevant port variable in `.env` (e.g. change `CLIENT_PORT`) and restart: `docker compose up -d`.

**Reset the database completely**
- `docker compose down -v && docker compose up -d` will destroy all data and re-apply migrations + seed data from scratch.
