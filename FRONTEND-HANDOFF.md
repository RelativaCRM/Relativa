# Frontend Developer Handoff

## Overview
The backend has been significantly refactored. Organization is now the primary multi-tenant boundary. Users must join an organization before accessing workspaces. A split RBAC model separates organization roles from workspace roles.

## Migration Notice
The database schema has been completely rebuilt. Run:
```bash
docker compose down -v
docker compose up -d
```
This destroys old data and creates fresh schema with seed data.

## Authentication

### GET /auth/api/v1/auth/me
Returns current user profile. Requires JWT Bearer token.

**Response:**
```json
{
  "id": 1,
  "email": "admin@relativa.com",
  "firstName": "Dorian",
  "lastName": "Gray"
}
```

Note: The gateway routes `/auth/api/v1/auth/login` and `/auth/api/v1/auth/register` are anonymous. `/auth/api/v1/auth/me` requires JWT.

## User Onboarding Flow

```
Register/Login → GET /auth/me → GET /organizations (empty?) 
  → YES: GET /organizations/search?q=... → POST /organizations/{id}/join-requests
  → OR: Accept invitation via POST /invitations/accept-org
  → OR: POST /organizations (create new org)
→ User is org member → gets added to workspaces → workspace RBAC applies
```

## RBAC Model

### Two-scope role system
- **Organization roles** control org-level actions (inviting, managing members, creating workspaces)
- **Workspace roles** control workspace-level actions (editing deals, managing ws members, viewing analytics)

### 16 Granular Permissions

**Organization-scoped (IDs 1-7):**
| ID | Name | Description |
|----|------|-------------|
| 1 | manage_org_settings | Edit organization name/details |
| 2 | invite_to_org | Send email invitations to join the org |
| 3 | manage_join_requests | Approve/reject join requests |
| 4 | remove_org_members | Remove members from the org |
| 5 | assign_org_roles | Change a member's org role |
| 6 | manage_org_roles | Create/edit/delete custom org roles |
| 7 | create_workspaces | Create new workspaces in the org |

**Workspace-scoped (IDs 8-16):**
| ID | Name | Description |
|----|------|-------------|
| 8 | manage_ws_settings | Edit workspace name, archive workspace |
| 9 | invite_to_workspace | Send workspace invitations |
| 10 | add_ws_members | Directly add org members to workspace |
| 11 | remove_ws_members | Remove members from workspace |
| 12 | assign_ws_roles | Change a member's workspace role |
| 13 | manage_ws_roles | Create/edit/delete custom workspace roles |
| 14 | edit_deals | Create/edit deals |
| 15 | view_deals | Read-only deal access |
| 16 | view_analytics | View reports and analytics |

### Default Roles

**Organization roles (system, shared across all orgs):**
- `org_owner` — all 7 org permissions
- `org_admin` — all except manage_org_roles
- `org_member` — no org permissions (baseline)

**Workspace roles (system, shared across all workspaces):**
- `ws_admin` — all 9 ws permissions
- `ws_manager` — invite_to_workspace, add_ws_members, edit_deals, view_deals, view_analytics
- `ws_analyst` — view_analytics, view_deals
- `ws_member` — view_deals only

## Complete Endpoint Catalog

### Organizations
```
POST   /core/api/v1/organizations                          → Create org (any authenticated user, creator becomes org_owner)
GET    /core/api/v1/organizations                          → List user's orgs
GET    /core/api/v1/organizations/search?q={query}         → Search orgs by name
GET    /core/api/v1/organizations/{id}                     → Get org details (org member)
PUT    /core/api/v1/organizations/{id}                     → Update org (manage_org_settings)
```

**Create Organization Request:**
```json
{ "name": "My Company" }
```

**Organization Response:**
```json
{
  "id": 1,
  "name": "My Company",
  "memberCount": 1,
  "userRole": "org_owner"
}
```

### Organization Members
```
GET    /core/api/v1/organizations/{id}/members             → List members (org member)
DELETE /core/api/v1/organizations/{id}/members/{userId}    → Remove member (remove_org_members)
PUT    /core/api/v1/organizations/{id}/members/{userId}/role → Change role (assign_org_roles)
```

**Change Role Request:**
```json
{ "roleId": 2 }
```

**Member Response:**
```json
{
  "userId": 1,
  "firstName": "Dorian",
  "lastName": "Gray",
  "email": "admin@relativa.com",
  "roleName": "org_owner",
  "joinedAt": "2026-04-17T00:00:00Z"
}
```

### Join Requests
```
POST   /core/api/v1/organizations/{id}/join-requests       → Submit request (any authenticated user)
GET    /core/api/v1/organizations/{id}/join-requests       → List pending (manage_join_requests)
PUT    /core/api/v1/organizations/{id}/join-requests/{reqId} → Review (manage_join_requests)
GET    /core/api/v1/join-requests/mine                     → My requests (authenticated)
```

**Submit Join Request:**
```json
{ "message": "I'd like to join your team" }
```

**Review Join Request:**
```json
{ "decision": "Approved" }
```
Decision must be "Approved" or "Rejected".

**Join Request Response:**
```json
{
  "id": 1,
  "userId": 2,
  "userName": "Ivan Franko",
  "userEmail": "ivan.f@relativa.com",
  "message": "I'd like to join your team",
  "status": "Pending",
  "createdAt": "2026-04-17T00:00:00Z",
  "reviewedByName": null,
  "reviewedAt": null
}
```

### Organization Invitations
```
POST   /core/api/v1/organizations/{id}/invitations         → Invite by email (invite_to_org)
GET    /core/api/v1/organizations/{id}/invitations         → List pending (invite_to_org)
DELETE /core/api/v1/organizations/{id}/invitations/{invId} → Cancel (invite_to_org)
POST   /core/api/v1/invitations/accept-org                 → Accept by token (email must match)
```

**Invite Request:**
```json
{ "email": "new.user@example.com" }
```

**Accept Org Invitation:**
```json
{ "token": "abc123def456..." }
```

**Invitation Response (token is now included!):**
```json
{
  "id": 1,
  "email": "new.user@example.com",
  "organizationName": "Relativa Global",
  "status": "Pending",
  "token": "abc123def456...",
  "expiresAt": "2026-04-24T00:00:00Z"
}
```

### Organization Roles (Custom)
```
GET    /core/api/v1/organizations/{id}/roles               → List roles (org member)
POST   /core/api/v1/organizations/{id}/roles               → Create custom role (manage_org_roles)
PUT    /core/api/v1/organizations/{id}/roles/{roleId}      → Update role (manage_org_roles)
DELETE /core/api/v1/organizations/{id}/roles/{roleId}      → Archive role (manage_org_roles)
```

**Create Role Request:**
```json
{
  "name": "Support Manager",
  "permissionIds": [2, 3, 4]
}
```

**Role Response:**
```json
{
  "id": 4,
  "name": "Support Manager",
  "isSystem": false,
  "permissions": [
    { "id": 2, "name": "invite_to_org" },
    { "id": 3, "name": "manage_join_requests" },
    { "id": 4, "name": "remove_org_members" }
  ]
}
```

### Combined Invitations (My Inbox)
```
GET    /core/api/v1/invitations/mine → All pending invitations for current user (both workspace + org)
```

**Response:**
```json
{
  "workspaceInvitations": [
    { "id": 1, "email": "...", "roleName": "ws_member", "status": "Pending", "token": "...", "expiresAt": "..." }
  ],
  "organizationInvitations": [
    { "id": 1, "email": "...", "organizationName": "...", "status": "Pending", "token": "...", "expiresAt": "..." }
  ]
}
```

### Workspaces (Modified)
```
POST   /core/api/v1/workspaces                            → Create workspace (organizationId required, create_workspaces org perm)
GET    /core/api/v1/workspaces                            → List user's workspaces
GET    /core/api/v1/workspaces/{id}                       → Get workspace details (ws member)
PUT    /core/api/v1/workspaces/{id}                       → Update (manage_ws_settings)
DELETE /core/api/v1/workspaces/{id}                       → Archive (ws_admin role required)
```

**Create Workspace Request (CHANGED - organizationId now required):**
```json
{
  "name": "EU Sales",
  "organizationId": 1
}
```

### Workspace Members (NEW: direct add)
```
GET    /core/api/v1/workspaces/{id}/members                → List members
POST   /core/api/v1/workspaces/{id}/members                → Add org member directly (add_ws_members) ← NEW
PUT    /core/api/v1/workspaces/{id}/members/{userId}/role   → Change role (assign_ws_roles)
DELETE /core/api/v1/workspaces/{id}/members/{userId}        → Remove member (remove_ws_members)
```

**Add Member Request:**
```json
{ "userId": 3, "roleId": 4 }
```
User must be in the same organization as the workspace.

### Workspace Invitations (token now returned)
```
POST   /core/api/v1/workspaces/{id}/invitations            → Invite by email (invite_to_workspace)
GET    /core/api/v1/workspaces/{id}/invitations            → List pending (invite_to_workspace)
DELETE /core/api/v1/workspaces/{id}/invitations/{invId}    → Cancel (invite_to_workspace)
POST   /core/api/v1/invitations/accept                     → Accept by token
```

**Invitation response now includes token.**

### Workspace Roles + Permissions (unchanged routes)
```
GET    /core/api/v1/workspaces/{id}/roles
POST   /core/api/v1/workspaces/{id}/roles
PUT    /core/api/v1/workspaces/{id}/roles/{roleId}
DELETE /core/api/v1/workspaces/{id}/roles/{roleId}
GET    /core/api/v1/permissions
```

## Key Changes from Previous Version

1. **`organizationId` is now required** when creating a workspace
2. **Token is now returned** in invitation responses (no more pgAdmin to get tokens)
3. **Old permission names replaced**: `can_manage_settings` → `manage_ws_settings`, etc.
4. **Users no longer have a global role** — roles are per-organization and per-workspace membership
5. **GET /invitations/mine** — frontend can now show user's pending invitations
6. **GET /auth/me** — frontend can get user profile without parsing JWT
7. **All gateway auth routes split**: only login/register are anonymous, everything else needs JWT

## Seed Data Users
| Email | Password | Role |
|-------|----------|------|
| admin@relativa.com | (placeholder hash - register fresh users) | org_owner @ Relativa Global, ws_admin @ EU Sales |
| ivan.f@relativa.com | (placeholder hash) | org_member @ Relativa Global, ws_manager @ EU Sales |
| lesya.u@relativa.com | (placeholder hash) | org_member @ Relativa Global, ws_analyst @ EU Sales |

Note: Seed users have placeholder password hashes. Register new users via POST /auth/register for testing.
