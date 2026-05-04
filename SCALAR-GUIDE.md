# Relativa API Reference — Scalar Guide

This guide walks through every step needed to open the Scalar interactive API docs and execute the live endpoints that are currently available in the application.

---

## Table of contents

1. [Prerequisites](#1-prerequisites)
2. [Start the stack](#2-start-the-stack)
3. [Open Scalar](#3-open-scalar)
4. [Register a new user](#4-register-a-new-user)
5. [Log in and obtain a JWT](#5-log-in-and-obtain-a-jwt)
6. [Get your profile (/me)](#6-get-your-profile-me)
7. [Authorise subsequent requests](#7-authorise-subsequent-requests)
8. [Create an organization](#8-create-an-organization)
9. [Organization join requests](#9-organization-join-requests)
10. [Organization invitations](#10-organization-invitations)
11. [Manage organization members and roles](#11-manage-organization-members-and-roles)
12. [Create a workspace](#12-create-a-workspace)
13. [Add members to a workspace](#13-add-members-to-a-workspace)
14. [Adding people to a workspace (current flow)](#14-adding-people-to-a-workspace-current-flow)
15. [Manage workspace members](#15-manage-workspace-members)
16. [Manage workspace roles and permissions](#16-manage-workspace-roles-and-permissions)
17. [Combined invitations](#17-combined-invitations)
18. [Health checks](#18-health-checks)
19. [Available endpoints reference](#19-available-endpoints-reference)
20. [Tips and troubleshooting](#20-tips-and-troubleshooting)

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

> **Recommended starting point:** open the **Authentication** Scalar (`http://localhost:8081/scalar/v1`) to register, log in, and check your profile. Then switch to the **Core** Scalar (`http://localhost:8082/scalar/v1`) to test organization, workspace, and RBAC endpoints.

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

> **Note:** Newly registered users have no organization or workspace membership. A user must create or join an organization first, then create or join a workspace.

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

The JWT contains `sub` (user ID) and `email` claims. Role and permissions are **not** in the token — they are resolved per-request by Core based on organization/workspace membership.

### Error responses

| Status | When |
|--------|------|
| `400 Validation Problem` | Missing or malformed fields |
| `401 Unauthorized` | Wrong email or password |

---

## 6. Get your profile (/me)

**Endpoint:** `GET /api/v1/auth/me`

Requires a valid JWT. Returns the authenticated user's profile.

### Steps in Scalar (Authentication)

1. Open the Authentication Scalar UI: `http://localhost:8081/scalar/v1`.
2. Click the **Authorize** button and paste your `accessToken`.
3. Find **Authentication** → **GetProfile** (or **Me**) in the sidebar.
4. Click **Try it** → **Send**.

### Expected response — `200 OK`

```json
{
  "id": 4,
  "email": "jane.doe@example.com",
  "firstName": "Jane",
  "lastName": "Doe"
}
```

### Via Gateway

The `/me` endpoint requires JWT even through the gateway (unlike login/register):

```
GET /auth/api/v1/auth/me
Authorization: Bearer <token>
```

### Error responses

| Status | When |
|--------|------|
| `401 Unauthorized` | Missing or expired JWT |

---

## 7. Authorise subsequent requests

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
| `GET /api/v1/auth/me` | `GET /auth/api/v1/auth/me` |
| `PATCH /api/v1/auth/me` | `PATCH /auth/api/v1/auth/me` |
| `DELETE /api/v1/auth/me` | `DELETE /auth/api/v1/auth/me` |
| `POST /api/v1/organizations` | `POST /core/api/v1/organizations` |
| `POST /api/v1/workspaces` | `POST /core/api/v1/workspaces` |
| `POST /api/v1/organizations/{id}/users` | `POST /core/api/v1/organizations/{id}/users` |

The `/auth/api/v1/auth/login` and `/auth/api/v1/auth/register` paths are marked **Anonymous** in the gateway — no JWT needed. **`GET` / `PATCH` / `DELETE`** `/auth/api/v1/auth/me` and all `/core/...` paths require a valid JWT.

---

## 8. Create an organization

**Endpoint:** `POST /api/v1/organizations`

Requires a valid JWT. Any authenticated user can create an organization.

### Steps in Scalar (Core)

1. Open the Core Scalar UI: `http://localhost:8082/scalar/v1`.
2. Make sure you have authorised (step 7).
3. Find **Organizations** → **CreateOrganization** in the sidebar.
4. Click **Try it** and enter:

```json
{
  "name": "Acme Corp"
}
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `name` | string | yes | Organization name |

5. Click **Send**.

### Expected response — `201 Created`

```json
{
  "id": 1,
  "name": "Acme Corp"
}
```

The creating user is automatically added as a member with the **org_owner** system role (has all 7 org permissions).

### List your organizations

**Endpoint:** `GET /api/v1/organizations`

Returns all organizations where the authenticated user is a member.

### Search organizations

**Endpoint:** `GET /api/v1/organizations/search?q=acme`

Search organizations by name. Any authenticated user can search.

---

## 9. Organization join requests

Users can request to join an organization they found via search.

### Request to join

**Endpoint:** `POST /api/v1/organizations/{organizationId}/join-requests`

Any authenticated user who is not already a member.

1. Find **JoinRequests** → **CreateJoinRequest** in the sidebar.
2. Set the `organizationId` path parameter.
3. Click **Send** (no body needed, or provide an optional message).

### Expected response — `201 Created`

```json
{
  "id": 1,
  "organizationId": 1,
  "userId": 5,
  "status": "Pending"
}
```

### Review join requests (org admin)

**Endpoint:** `GET /api/v1/organizations/{organizationId}/join-requests`

Requires the `manage_join_requests` permission.

### Approve or reject

**Endpoint:** `PUT /api/v1/organizations/{organizationId}/join-requests/{requestId}`

Requires the `manage_join_requests` permission.

```json
{
  "status": "Approved",
  "roleId": 3
}
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `status` | string | yes | `"Approved"` or `"Rejected"` |
| `roleId` | integer | conditional | Required when approving — the org role to assign |

### List your own join requests

**Endpoint:** `GET /api/v1/join-requests/mine`

Returns all join requests made by the authenticated user.

---

## 10. Organization invitations

Org admins can invite users by email to join the organization.

### Invite to organization

**Endpoint:** `POST /api/v1/organizations/{organizationId}/invitations`

Requires the `invite_to_org` permission.

```json
{
  "email": "bob@example.com",
  "roleId": 3
}
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `email` | string | yes | Email of the person to invite |
| `roleId` | integer | yes | Org role to assign on acceptance. Use `GET .../roles` to see available role IDs |

### Expected response — `201 Created`

```json
{
  "id": 1,
  "email": "bob@example.com",
  "roleName": "org_member",
  "status": "Pending",
  "expiresAt": "2026-04-24T12:00:00Z"
}
```

### List pending org invitations

**Endpoint:** `GET /api/v1/organizations/{organizationId}/invitations`

Requires the `invite_to_org` permission.

### Cancel an org invitation

**Endpoint:** `DELETE /api/v1/organizations/{organizationId}/invitations/{invitationId}`

Requires the `invite_to_org` permission. Returns `204 No Content`.

### Accept an org invitation

**Endpoint:** `POST /api/v1/invitations/accept-org`

The invited user must register (if new) and log in first. The JWT email must match the invitation email.

```json
{
  "token": "<the invitation token>"
}
```

> **Note:** For testing, look up the token in the database: `SELECT token FROM organization_invitations WHERE email = 'bob@example.com';` using pgAdmin at `http://localhost:5050`.

### Expected response — `200 OK`

```json
{
  "message": "Invitation accepted."
}
```

---

## 11. Manage organization members and roles

### List organization members

**Endpoint:** `GET /api/v1/organizations/{organizationId}/members`

Requires org membership.

### Change a member's org role

**Endpoint:** `PUT /api/v1/organizations/{organizationId}/members/{userId}/role`

Requires `assign_org_roles` permission.

```json
{
  "roleId": 2
}
```

Returns `204 No Content` on success.

### Remove a member from the organization

**Endpoint:** `DELETE /api/v1/organizations/{organizationId}/members/{userId}`

Requires `remove_org_members` permission. Returns `204 No Content`.

### List org roles

**Endpoint:** `GET /api/v1/organizations/{organizationId}/roles`

Requires org membership. Returns system roles plus custom org roles:

```json
[
  {
    "id": 1,
    "name": "org_owner",
    "isSystem": true,
    "permissions": [
      { "id": 1, "name": "manage_org_settings" },
      { "id": 2, "name": "invite_to_org" },
      { "id": 3, "name": "manage_join_requests" },
      { "id": 4, "name": "remove_org_members" },
      { "id": 5, "name": "assign_org_roles" },
      { "id": 6, "name": "manage_org_roles" },
      { "id": 7, "name": "create_workspaces" }
    ]
  }
]
```

### Create a custom org role

**Endpoint:** `POST /api/v1/organizations/{organizationId}/roles`

Requires `manage_org_roles` permission.

```json
{
  "name": "org_moderator",
  "permissionIds": [2, 3]
}
```

### Update / delete custom org roles

- `PUT /api/v1/organizations/{organizationId}/roles/{roleId}` — update (requires `manage_org_roles`)
- `DELETE /api/v1/organizations/{organizationId}/roles/{roleId}` — delete (requires `manage_org_roles`)

System roles cannot be modified or deleted.

---

## 12. Create a workspace

**Endpoint:** `POST /api/v1/workspaces`

Requires a valid JWT and the `create_workspaces` **org-scoped permission** in the target organization.

### Steps in Scalar (Core)

1. Open the Core Scalar UI: `http://localhost:8082/scalar/v1`.
2. Make sure you have authorised (step 7).
3. Find **Workspaces** → **CreateWorkspace** in the sidebar.
4. Click **Try it** and enter:

```json
{
  "name": "My Sales Team",
  "organizationId": 1
}
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `name` | string | yes | Max 200 characters |
| `organizationId` | integer | yes | The organization this workspace belongs to. You must have `create_workspaces` permission in this org. |

5. Click **Send**.

### Expected response — `201 Created`

```json
{
  "id": 3,
  "name": "My Sales Team",
  "memberCount": 1,
  "userRole": "ws_admin"
}
```

The creating user is automatically added as a member with the **ws_admin** system role (has all 9 ws permissions).

### List your workspaces

**Endpoint:** `GET /api/v1/workspaces`

Returns all workspaces where the authenticated user is a member:

```json
[
  {
    "id": 3,
    "name": "My Sales Team",
    "memberCount": 1,
    "userRole": "ws_admin"
  }
]
```

---

## 13. Add members to a workspace

**Endpoint:** `POST /api/v1/workspaces/{workspaceId}/members`

Requires workspace `add_ws_members` **or** organization `manage_org_workspace_members` on the parent org. The target user must already be a member of the workspace's parent organization.

```json
{
  "userId": 5,
  "roleId": 4
}
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `userId` | integer | yes | ID of the org member to add |
| `roleId` | integer | yes | Workspace role to assign. Use `GET .../roles` to see available role IDs |

### Expected response — `201 Created`

This directly adds an existing org member to the workspace without requiring an invitation flow.

---

## 14. Adding people to a workspace (current flow)

Workspace-scoped **email invitations** and **workspace join requests** were removed.

1. **Organization invitation:** invite the person to the **organization** (`POST /api/v1/organizations/{organizationId}/invitations`). They accept with `POST /api/v1/invitations/accept-org`.
2. **Add to workspace:** call `POST /api/v1/workspaces/{workspaceId}/members` with `{ "userId", "roleId" }` as in [section 13](#13-add-members-to-a-workspace). The caller needs `add_ws_members` on the workspace **or** `manage_org_workspace_members` on the parent organization.

---

## 15. Manage workspace members

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
    "roleName": "ws_admin",
    "joinedAt": "2026-04-13T12:00:00Z"
  },
  {
    "userId": 5,
    "firstName": "Bob",
    "lastName": "Smith",
    "email": "colleague@example.com",
    "roleName": "ws_manager",
    "joinedAt": "2026-04-13T12:05:00Z"
  }
]
```

### Change a member's role

**Endpoint:** `PUT /api/v1/workspaces/{workspaceId}/members/{userId}/role`

Requires `assign_ws_roles` permission.

```json
{
  "roleId": 3
}
```

Returns `204 No Content` on success.

### Remove a member

**Endpoint:** `DELETE /api/v1/workspaces/{workspaceId}/members/{userId}`

Requires workspace `remove_ws_members` **or** organization `manage_org_workspace_members` on the parent org (unless removing yourself).

Returns `204 No Content` on success.

---

## 16. Manage workspace roles and permissions

### List available permissions

**Endpoint:** `GET /api/v1/permissions`

Any authenticated user. Returns all permissions (org- and workspace-scoped). After the latest migrations, **19** rows remain (numeric ids keep gaps where retired permissions were removed):

```json
[
  { "id": 1, "name": "manage_org_settings" },
  { "id": 2, "name": "invite_to_org" },
  { "id": 3, "name": "manage_join_requests" },
  { "id": 4, "name": "remove_org_members" },
  { "id": 5, "name": "assign_org_roles" },
  { "id": 6, "name": "manage_org_roles" },
  { "id": 7, "name": "create_workspaces" },
  { "id": 8, "name": "manage_ws_settings" },
  { "id": 10, "name": "add_ws_members" },
  { "id": 11, "name": "remove_ws_members" },
  { "id": 12, "name": "assign_ws_roles" },
  { "id": 13, "name": "manage_ws_roles" },
  { "id": 14, "name": "edit_deals" },
  { "id": 15, "name": "view_deals" },
  { "id": 16, "name": "view_analytics" },
  { "id": 17, "name": "create_org_users" },
  { "id": 18, "name": "edit_other_org_users_profile" },
  { "id": 19, "name": "delete_org_users" },
  { "id": 21, "name": "manage_org_workspace_members" }
]
```

### List roles in a workspace

**Endpoint:** `GET /api/v1/workspaces/{workspaceId}/roles`

Requires workspace membership. Returns system roles (available in all workspaces) plus any custom roles created in this workspace:

```json
[
  {
    "id": 1,
    "name": "ws_admin",
    "isSystem": true,
    "permissions": [
      { "id": 8, "name": "manage_ws_settings" },
      { "id": 10, "name": "add_ws_members" },
      { "id": 11, "name": "remove_ws_members" },
      { "id": 12, "name": "assign_ws_roles" },
      { "id": 13, "name": "manage_ws_roles" },
      { "id": 14, "name": "edit_deals" },
      { "id": 15, "name": "view_deals" },
      { "id": 16, "name": "view_analytics" }
    ]
  },
  {
    "id": 2,
    "name": "ws_manager",
    "isSystem": true,
    "permissions": [
      { "id": 14, "name": "edit_deals" },
      { "id": 15, "name": "view_deals" },
      { "id": 16, "name": "view_analytics" }
    ]
  }
]
```

### Create a custom workspace role

**Endpoint:** `POST /api/v1/workspaces/{workspaceId}/roles`

Requires `manage_ws_roles` permission.

```json
{
  "name": "team_lead",
  "permissionIds": [12, 14, 15, 16]
}
```

### Expected response — `201 Created`

```json
{
  "id": 5,
  "name": "team_lead",
  "isSystem": false,
  "permissions": [
    { "id": 12, "name": "assign_ws_roles" },
    { "id": 14, "name": "edit_deals" },
    { "id": 15, "name": "view_deals" },
    { "id": 16, "name": "view_analytics" }
  ]
}
```

### Update a custom role

**Endpoint:** `PUT /api/v1/workspaces/{workspaceId}/roles/{roleId}`

Requires `manage_ws_roles`. System roles cannot be modified.

```json
{
  "name": "senior_lead",
  "permissionIds": [8, 12, 14, 15, 16]
}
```

Returns `204 No Content` on success.

### Delete (archive) a custom role

**Endpoint:** `DELETE /api/v1/workspaces/{workspaceId}/roles/{roleId}`

Requires `manage_ws_roles`. System roles cannot be deleted. Returns `204 No Content` on success.

---

## 17. Combined invitations

**Endpoint:** `GET /api/v1/invitations/mine`

Returns pending **organization** invitations for the authenticated user’s email (same payload shape as before, but workspace invitations are no longer returned). **`GET /api/v1/invitations/mine/organization`** returns the same list without the wrapper DTO.

---

## 18. Health checks

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

## 19. Available endpoints reference

### Authentication service — `http://localhost:8081`

| Method | Path | Auth | Summary |
|--------|------|------|---------|
| `POST` | `/api/v1/auth/register` | None | Register a new user |
| `POST` | `/api/v1/auth/login` | None | Authenticate and receive JWT |
| `GET` | `/api/v1/auth/me` | JWT | Get authenticated user's profile |
| `PATCH` | `/api/v1/auth/me` | JWT | Update own first and last name |
| `DELETE` | `/api/v1/auth/me` | JWT | Archive own account |
| `GET` | `/health` | None | Health check (DB connectivity) |
| `GET` | `/scalar/v1` | None | Scalar interactive docs |
| `GET` | `/openapi/v1.json` | None | Raw OpenAPI spec |

### Core service — `http://localhost:8082`

**Organizations:**

| Method | Path | Auth | Summary |
|--------|------|------|---------|
| `POST` | `/api/v1/organizations` | JWT | Create organization (creator becomes org_owner) |
| `GET` | `/api/v1/organizations` | JWT | List user's organizations |
| `GET` | `/api/v1/organizations/search?q=...` | JWT | Search organizations by name |
| `GET` | `/api/v1/organizations/{id}` | JWT + org membership | Get organization details |
| `PUT` | `/api/v1/organizations/{id}` | JWT + `manage_org_settings` | Update organization |

**Organization members:**

| Method | Path | Auth | Summary |
|--------|------|------|---------|
| `GET` | `/api/v1/organizations/{id}/members` | JWT + org membership | List org members |
| `DELETE` | `/api/v1/organizations/{id}/members/{userId}` | JWT + `remove_org_members` | Remove member |
| `PUT` | `/api/v1/organizations/{id}/members/{userId}/role` | JWT + `assign_org_roles` | Change member's org role |

**Organization users (admin provisioning):**

| Method | Path | Auth | Summary |
|--------|------|------|---------|
| `POST` | `/api/v1/organizations/{id}/users` | JWT + `create_org_users` | Create user account and add as org member |
| `PATCH` | `/api/v1/organizations/{id}/users/{userId}` | JWT + `edit_other_org_users_profile` | Update another member's name |
| `DELETE` | `/api/v1/organizations/{id}/users/{userId}` | JWT + `delete_org_users` | Archive user account |

**Join requests:**

| Method | Path | Auth | Summary |
|--------|------|------|---------|
| `POST` | `/api/v1/organizations/{id}/join-requests` | JWT | Request to join organization |
| `GET` | `/api/v1/organizations/{id}/join-requests` | JWT + `manage_join_requests` | List pending join requests |
| `PUT` | `/api/v1/organizations/{id}/join-requests/{reqId}` | JWT + `manage_join_requests` | Approve or reject request |
| `GET` | `/api/v1/join-requests/mine` | JWT | List own join requests |

**Organization invitations:**

| Method | Path | Auth | Summary |
|--------|------|------|---------|
| `POST` | `/api/v1/organizations/{id}/invitations` | JWT + `invite_to_org` | Invite user by email |
| `GET` | `/api/v1/organizations/{id}/invitations` | JWT + `invite_to_org` | List pending invitations |
| `DELETE` | `/api/v1/organizations/{id}/invitations/{invId}` | JWT + `invite_to_org` | Cancel invitation |
| `POST` | `/api/v1/invitations/accept-org` | JWT + matching email | Accept org invitation |

**Organization roles:**

| Method | Path | Auth | Summary |
|--------|------|------|---------|
| `GET` | `/api/v1/organizations/{id}/roles` | JWT + org membership | List org roles |
| `POST` | `/api/v1/organizations/{id}/roles` | JWT + `manage_org_roles` | Create custom org role |
| `PUT` | `/api/v1/organizations/{id}/roles/{roleId}` | JWT + `manage_org_roles` | Update custom org role |
| `DELETE` | `/api/v1/organizations/{id}/roles/{roleId}` | JWT + `manage_org_roles` | Delete custom org role |

**Workspaces:**

| Method | Path | Auth | Summary |
|--------|------|------|---------|
| `POST` | `/api/v1/workspaces` | JWT + `create_workspaces` (org perm) | Create workspace (requires organizationId) |
| `GET` | `/api/v1/workspaces` | JWT | List user's workspaces |
| `GET` | `/api/v1/workspaces/{id}` | JWT + ws membership | Get workspace details |
| `PUT` | `/api/v1/workspaces/{id}` | JWT + `manage_ws_settings` | Update workspace name |
| `DELETE` | `/api/v1/workspaces/{id}` | JWT + ws_admin | Archive workspace |

**Workspace members:**

| Method | Path | Auth | Summary |
|--------|------|------|---------|
| `GET` | `/api/v1/workspaces/{id}/members` | JWT + ws membership | List workspace members |
| `POST` | `/api/v1/workspaces/{id}/members` | JWT + `add_ws_members` **or** org `manage_org_workspace_members` | Add org member to workspace |
| `PUT` | `/api/v1/workspaces/{id}/members/{userId}/role` | JWT + `assign_ws_roles` | Change member role |
| `DELETE` | `/api/v1/workspaces/{id}/members/{userId}` | JWT + `remove_ws_members` **or** org `manage_org_workspace_members` / self | Remove member |

**Workspace roles:**

| Method | Path | Auth | Summary |
|--------|------|------|---------|
| `GET` | `/api/v1/workspaces/{id}/roles` | JWT + ws membership | List roles (system + custom) |
| `POST` | `/api/v1/workspaces/{id}/roles` | JWT + `manage_ws_roles` | Create custom role |
| `PUT` | `/api/v1/workspaces/{id}/roles/{roleId}` | JWT + `manage_ws_roles` | Update custom role |
| `DELETE` | `/api/v1/workspaces/{id}/roles/{roleId}` | JWT + `manage_ws_roles` | Archive custom role |

**Invitation inbox & permissions:**

| Method | Path | Auth | Summary |
|--------|------|------|---------|
| `GET` | `/api/v1/invitations/mine` | JWT | Pending org invitations (wrapped DTO) |
| `GET` | `/api/v1/invitations/mine/organization` | JWT | Pending org invitations (raw list) |
| `GET` | `/api/v1/permissions` | JWT | List all permissions |

**Infrastructure:**

| Method | Path | Auth | Summary |
|--------|------|------|---------|
| `GET` | `/health` | None | Health check (DB connectivity) |
| `GET` | `/scalar/v1` | None | Scalar interactive docs |
| `GET` | `/openapi/v1.json` | None | Raw OpenAPI spec |

### Gateway — `http://localhost:8080`

| Method | Path | Auth | Proxied to |
|--------|------|------|------------|
| `POST` | `/auth/api/v1/auth/login` | None | Auth service |
| `POST` | `/auth/api/v1/auth/register` | None | Auth service |
| `GET` | `/auth/api/v1/auth/me` | Bearer JWT | Auth service |
| `PATCH` | `/auth/api/v1/auth/me` | Bearer JWT | Auth service |
| `DELETE` | `/auth/api/v1/auth/me` | Bearer JWT | Auth service |
| `GET` | `/auth/health` | None | Auth service |
| `POST` | `/core/api/v1/organizations/{id}/users` | Bearer JWT | Core service |
| `PATCH` | `/core/api/v1/organizations/{id}/users/{userId}` | Bearer JWT | Core service |
| `DELETE` | `/core/api/v1/organizations/{id}/users/{userId}` | Bearer JWT | Core service |
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

## 20. Tips and troubleshooting

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
- Your user does not have the required permission. For org endpoints, check your org role with `GET /api/v1/organizations/{id}/members`. For workspace endpoints, check with `GET /api/v1/workspaces/{id}/members`.
- The `org_owner` role has all org permissions; `ws_admin` has all **workspace** permissions (not org-only permissions such as `manage_org_workspace_members` unless also granted on an org role).

**"You are not a member of this organization"**
- The `sub` claim in your JWT does not match any `user_role_organization` row for that org. Create an org, request to join, or accept an invitation first.

**"You are not a member of this workspace"**
- The `sub` claim in your JWT does not match any `user_role_workspace` row for that workspace. Join the parent organization (invite or join request), then have someone with `add_ws_members` or org `manage_org_workspace_members` add you via `POST .../workspaces/{id}/members`.

**"User is not a member of the organization" when adding to workspace**
- The `POST .../workspaces/{id}/members` endpoint requires the target user to be a member of the workspace's parent organization first.

**How do I find invitation tokens for testing?**
- **Organization invitations:** The token is returned in the `POST .../organizations/{id}/invitations` response body. For manual lookup: `SELECT id, email, token, status FROM organization_invitations;` in pgAdmin at `http://localhost:5050`.

**Scalar shows no endpoints**
- The OpenAPI JSON may have failed to load. Navigate directly to `http://localhost:8082/openapi/v1.json` and confirm you get valid JSON (not an error page).

**Port already in use**
- Update the relevant port variable in `.env` (e.g. change `CLIENT_PORT`) and restart: `docker compose up -d`.

**Reset the database completely**
- `docker compose down -v && docker compose up -d` will destroy all data and re-apply migrations + seed data from scratch.
