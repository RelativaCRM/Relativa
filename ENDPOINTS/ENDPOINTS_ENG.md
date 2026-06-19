# Relativa CRM — API Endpoint Reference

> **Architecture:** Microservices (.NET Minimal API) behind an API Gateway
> **Base prefix:** All external routes are proxied through the Gateway
> **Auth:** JWT Bearer — the Gateway validates tokens and injects `X-User-Id` / `X-User-Email` headers before forwarding

---

## Table of Contents

1. [Authentication](#1-authentication)
2. [Support](#2-support)
3. [Organizations](#3-organizations)
   - [Members](#31-organization-members)
   - [Roles](#32-organization-roles)
   - [Invitations](#33-organization-invitations)
   - [Join Requests (org-scoped)](#34-organization-join-requests)
4. [Workspaces](#4-workspaces)
   - [Members](#41-workspace-members)
   - [Roles](#42-workspace-roles)
5. [Permissions](#5-permissions)
6. [Invitations (user-scoped)](#6-invitations-user-scoped)
7. [Join Requests (user-scoped)](#7-join-requests-user-scoped)
8. [Entities](#8-entities)
9. [Entity Types](#9-entity-types)
10. [Entity Relationships](#10-entity-relationships)
11. [Entity Graph (RPC)](#11-entity-graph-rpc)
12. [Graph Query](#12-graph-query)
13. [Dashboard — Organization](#13-dashboard--organization)
14. [Dashboard — Workspace](#14-dashboard--workspace)
15. [Audit Log](#15-audit-log)
16. [ML Scoring](#16-ml-scoring)
17. [Real-time Hubs (SignalR)](#17-real-time-hubs-signalr)
18. [Gateway Routing](#18-gateway-routing)

---

## Legend

| Symbol | Meaning |
|--------|---------|
| 🔓 | No authentication required |
| 🔒 | JWT Bearer token required |
| `{id}` | Integer path parameter |
| `?param` | Optional query parameter |

---

## 1. Authentication

**Service file:** [Authentication/src/Relativa.Authentication/Endpoints/AuthEndpoints.cs](../Authentication/src/Relativa.Authentication/Endpoints/AuthEndpoints.cs)
**Client file:** [Client/src/api/auth.ts](../Client/src/api/auth.ts)
**Base path:** `/api/v1/auth`

### Identity & Session

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `POST` | `/login` | 🔓 | Authenticate with credentials |
| `POST` | `/oauth/{provider}` | 🔓 | OAuth login via external provider |
| `GET` | `/exists` | 🔓 | Check whether an email account exists |
| `POST` | `/register` | 🔓 | Register a new user account |
| `POST` | `/verify-email` | 🔓 | Verify email address with code |
| `POST` | `/resend-verification` | 🔓 | Resend email verification code |
| `GET` | `/verification-channels` | 🔓 | List available verification methods |

### Password Recovery

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `POST` | `/forgot-password` | 🔓 | Request a password-reset email |
| `GET` | `/reset-password/validate` | 🔓 | Validate a password-reset token |
| `POST` | `/reset-password` | 🔓 | Reset password using a valid token |

### Current User — Profile

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/me` | 🔒 | Get current user profile |
| `PATCH` | `/me` | 🔒 | Update current user profile |
| `DELETE` | `/me` | 🔒 | Archive (soft-delete) current user account |
| `GET` | `/me/settings` | 🔒 | Get user-level settings |
| `PATCH` | `/me/settings` | 🔒 | Update user-level settings |

### Current User — Two-Factor Authentication

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/me/2fa` | 🔒 | Get 2FA status |
| `POST` | `/me/2fa/setup` | 🔒 | Begin 2FA setup (returns TOTP secret/QR) |
| `POST` | `/me/2fa/enable` | 🔒 | Enable 2FA after confirming TOTP code |
| `POST` | `/me/2fa/disable` | 🔒 | Disable 2FA |
| `POST` | `/me/2fa/backup-codes` | 🔒 | Regenerate backup codes |
| `POST` | `/me/2fa/master-code` | 🔒 | Regenerate master recovery code |

### Current User — Email Management

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/me/emails` | 🔒 | List all email addresses on the account |
| `POST` | `/me/emails` | 🔒 | Add a new email address |
| `POST` | `/me/emails/verify` | 🔒 | Verify a newly added email |
| `POST` | `/me/emails/resend` | 🔒 | Resend email verification code |
| `POST` | `/me/emails/primary` | 🔒 | Set a verified address as primary |
| `POST` | `/me/emails/remove` | 🔒 | Remove a non-primary email address |

### Current User — OAuth Connections

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `POST` | `/me/connections/{provider}` | 🔒 | Link an OAuth provider to the account |

---

## 2. Support

**Service file:** [Authentication/src/Relativa.Authentication/Endpoints/SupportEndpoints.cs](../Authentication/src/Relativa.Authentication/Endpoints/SupportEndpoints.cs)
**Client file:** [Client/src/api/support.ts](../Client/src/api/support.ts)
**Base path:** `/api/v1/support`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `POST` | `/contact` | 🔓 | Send a support message |

---

## 3. Organizations

**Service files:**
- [Core/src/Relativa.Core/Endpoints/OrganizationEndpoints.cs](../Core/src/Relativa.Core/Endpoints/OrganizationEndpoints.cs)
- [Core/src/Relativa.Core/Endpoints/OrganizationUserEndpoints.cs](../Core/src/Relativa.Core/Endpoints/OrganizationUserEndpoints.cs)

**Client file:** [Client/src/api/organizations.ts](../Client/src/api/organizations.ts)
**Base path:** `/api/v1/organizations`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `POST` | `/` | 🔒 | Create a new organization |
| `GET` | `/` | 🔒 | List organizations for the current user |
| `GET` | `/search` | 🔒 | Search organizations by name |
| `GET` | `/{id}` | 🔒 | Get organization details |
| `PUT` | `/{id}` | 🔒 | Update organization |
| `GET` | `/{id}/settings` | 🔒 | Get organization settings |
| `PUT` | `/{id}/settings` | 🔒 | Update organization settings |
| `POST` | `/{organizationId}/users` | 🔒 | Create a user directly inside an organization |
| `PATCH` | `/{organizationId}/users/{userId}` | 🔒 | Update an org user's profile |
| `DELETE` | `/{organizationId}/users/{userId}` | 🔒 | Delete (hard-remove) an org user |

### 3.1 Organization Members

**Service file:** [Core/src/Relativa.Core/Endpoints/OrgMemberEndpoints.cs](../Core/src/Relativa.Core/Endpoints/OrgMemberEndpoints.cs)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/{organizationId}/members` | 🔒 | List all organization members |
| `DELETE` | `/{organizationId}/members/{userId}` | 🔒 | Remove a member from the organization |
| `PUT` | `/{organizationId}/members/{userId}/role` | 🔒 | Change a member's organization role |

### 3.2 Organization Roles

**Service file:** [Core/src/Relativa.Core/Endpoints/OrgRoleEndpoints.cs](../Core/src/Relativa.Core/Endpoints/OrgRoleEndpoints.cs)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/{organizationId}/roles` | 🔒 | List roles defined in this organization |
| `POST` | `/{organizationId}/roles` | 🔒 | Create a custom organization role |
| `PUT` | `/{organizationId}/roles/{roleId}` | 🔒 | Update an organization role |
| `DELETE` | `/{organizationId}/roles/{roleId}` | 🔒 | Archive an organization role |

### 3.3 Organization Invitations

**Service file:** [Core/src/Relativa.Core/Endpoints/OrgInvitationEndpoints.cs](../Core/src/Relativa.Core/Endpoints/OrgInvitationEndpoints.cs)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `POST` | `/{organizationId}/invitations` | 🔒 | Invite a user to the organization |
| `GET` | `/{organizationId}/invitations` | 🔒 | List pending invitations |
| `DELETE` | `/{organizationId}/invitations/{invitationId}` | 🔒 | Cancel an invitation |
| `POST` | `/{organizationId}/invitations/{invitationId}/resend` | 🔒 | Resend an invitation email |

### 3.4 Organization Join Requests

**Service file:** [Core/src/Relativa.Core/Endpoints/JoinRequestEndpoints.cs](../Core/src/Relativa.Core/Endpoints/JoinRequestEndpoints.cs)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `POST` | `/{organizationId}/join-requests` | 🔒 | Submit a join request to the organization |
| `GET` | `/{organizationId}/join-requests` | 🔒 | List join requests (admin view) |
| `PUT` | `/{organizationId}/join-requests/{requestId}` | 🔒 | Approve or decline a join request |

---

## 4. Workspaces

**Service files:**
- [Core/src/Relativa.Core/Endpoints/WorkspaceEndpoints.cs](../Core/src/Relativa.Core/Endpoints/WorkspaceEndpoints.cs)

**Client file:** [Client/src/api/workspaces.ts](../Client/src/api/workspaces.ts)
**Base path:** `/api/v1/workspaces`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `POST` | `/` | 🔒 | Create a new workspace |
| `GET` | `/` | 🔒 | List workspaces (`?organizationId` filter) |
| `GET` | `/{id}` | 🔒 | Get workspace details |
| `PUT` | `/{id}` | 🔒 | Update workspace |
| `DELETE` | `/{id}` | 🔒 | Archive workspace |
| `GET` | `/{id}/settings` | 🔒 | Get workspace settings |
| `PUT` | `/{id}/settings` | 🔒 | Update workspace settings |

### 4.1 Workspace Members

**Service file:** [Core/src/Relativa.Core/Endpoints/MemberEndpoints.cs](../Core/src/Relativa.Core/Endpoints/MemberEndpoints.cs)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/{workspaceId}/members` | 🔒 | List workspace members |
| `POST` | `/{workspaceId}/members` | 🔒 | Add a member to the workspace |
| `PUT` | `/{workspaceId}/members/{userId}/role` | 🔒 | Update a member's workspace role |
| `DELETE` | `/{workspaceId}/members/{userId}` | 🔒 | Remove a member from the workspace |

### 4.2 Workspace Roles

**Service file:** [Core/src/Relativa.Core/Endpoints/RoleEndpoints.cs](../Core/src/Relativa.Core/Endpoints/RoleEndpoints.cs)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/{workspaceId}/roles` | 🔒 | List roles defined in this workspace |
| `POST` | `/{workspaceId}/roles` | 🔒 | Create a custom workspace role |
| `PUT` | `/{workspaceId}/roles/{roleId}` | 🔒 | Update a workspace role |
| `DELETE` | `/{workspaceId}/roles/{roleId}` | 🔒 | Archive a workspace role |

---

## 5. Permissions

**Service file:** [Core/src/Relativa.Core/Endpoints/RoleEndpoints.cs](../Core/src/Relativa.Core/Endpoints/RoleEndpoints.cs)
**Base path:** `/api/v1/permissions`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/` | 🔒 | List all available permission definitions |

---

## 6. Invitations (user-scoped)

**Service file:** [Core/src/Relativa.Core/Endpoints/InvitationEndpoints.cs](../Core/src/Relativa.Core/Endpoints/InvitationEndpoints.cs)
**Base path:** `/api/v1/invitations`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `POST` | `/accept-org` | 🔒 | Accept an organization invitation |
| `POST` | `/decline-org` | 🔒 | Decline an organization invitation |
| `GET` | `/mine` | 🔒 | Get all pending invitations for the current user |
| `GET` | `/mine/organization` | 🔒 | Get pending organization invitations for the current user |

---

## 7. Join Requests (user-scoped)

**Service file:** [Core/src/Relativa.Core/Endpoints/JoinRequestEndpoints.cs](../Core/src/Relativa.Core/Endpoints/JoinRequestEndpoints.cs)
**Base path:** `/api/v1/join-requests`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/mine` | 🔒 | Get join requests submitted by the current user |
| `DELETE` | `/mine/{requestId}` | 🔒 | Cancel a join request |

---

## 8. Entities

**Service file:** [Core/src/Relativa.Core/Endpoints/EntityEndpoints.cs](../Core/src/Relativa.Core/Endpoints/EntityEndpoints.cs)
**Client file:** [Client/src/api/entities.ts](../Client/src/api/entities.ts)
**Base path:** `/api/v1/workspaces/{workspaceId}/entities`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/` | 🔒 | List entities — supports filtering, sorting, and pagination |
| `GET` | `/{entityId}` | 🔒 | Get full entity details |
| `POST` | `/` | 🔒 | Create a new entity |
| `PATCH` | `/{entityId}` | 🔒 | Update entity properties (partial update) |
| `DELETE` | `/{entityId}` | 🔒 | Archive an entity |

**Filter operators supported in `GET /`:** `eq`, `neq`, `gt`, `lt`, `gte`, `lte`, `contains`, `startsWith`
**Pagination:** skip / take with total count in response

---

## 9. Entity Types

**Service file:** [Core/src/Relativa.Core/Endpoints/EntityTypeEndpoints.cs](../Core/src/Relativa.Core/Endpoints/EntityTypeEndpoints.cs)
**Base path:** `/api/v1/entity-types`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/` | 🔒 | List all entity types with their property definitions |

---

## 10. Entity Relationships

**Service file:** [Core/src/Relativa.Core/Endpoints/EntityRelationshipEndpoints.cs](../Core/src/Relativa.Core/Endpoints/EntityRelationshipEndpoints.cs)
**Base path:** `/api/v1/workspaces/{workspaceId}/entity-relationships`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `POST` | `/` | 🔒 | Create a relationship between two entities |
| `PUT` | `/{relationshipId}` | 🔒 | Reassign the source or target of a relationship |
| `DELETE` | `/{relationshipId}` | 🔒 | Delete a relationship |

---

## 11. Entity Graph (RPC)

**Service file:** [Graph/src/Relativa.Graph/EntityGraphEndpoints.cs](../Graph/src/Relativa.Graph/EntityGraphEndpoints.cs)
**Client file:** [Client/src/api/entityGraph.ts](../Client/src/api/entityGraph.ts)
**Base path:** `/api/v1/workspaces/{workspaceId}/entity-graph`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `POST` | `/create` | 🔒 | Create an entity via the Graph RPC (composite creation) |

---

## 12. Graph Query

**Service file:** [Graph/src/Relativa.Graph/Graph/GraphQueryEndpoints.cs](../Graph/src/Relativa.Graph/Graph/GraphQueryEndpoints.cs)
**Client file:** [Client/src/api/graph.ts](../Client/src/api/graph.ts)
**Base path:** `/api/v1/graph`

| Method | Path | Auth | Query Parameters | Description |
|--------|------|------|-----------------|-------------|
| `GET` | `/` | 🔒 | `organizationId` *(required)*, `?riskLevel` (high \| medium \| low) | Get graph nodes and edges |

---

## 13. Dashboard — Organization

**Service file:** [Graph/src/Relativa.Graph/Dashboard/DashboardEndpoints.cs](../Graph/src/Relativa.Graph/Dashboard/DashboardEndpoints.cs)
**Client file:** [Client/src/api/dashboard.ts](../Client/src/api/dashboard.ts)
**Base path:** `/api/v1/dashboard`

> All endpoints require `?organizationId` query parameter.

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/summary` | 🔒 | Organization-level KPI summary |
| `GET` | `/pipeline` | 🔒 | Deal pipeline funnel |
| `GET` | `/risk-distribution` | 🔒 | Entity risk distribution |
| `GET` | `/trends` | 🔒 | 6-month metric trends |
| `GET` | `/top-entities` | 🔒 | Top deals and clients |
| `GET` | `/workspaces-comparison` | 🔒 | Cross-workspace KPI comparison |

---

## 14. Dashboard — Workspace

**Service file:** [Graph/src/Relativa.Graph/Dashboard/WorkspaceDashboardEndpoints.cs](../Graph/src/Relativa.Graph/Dashboard/WorkspaceDashboardEndpoints.cs)
**Client file:** [Client/src/api/workspaceDashboard.ts](../Client/src/api/workspaceDashboard.ts)
**Base path:** `/api/v1/dashboard/workspace/{workspaceId}`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/summary` | 🔒 | Workspace-level KPI summary |
| `GET` | `/pipeline` | 🔒 | Workspace deal pipeline funnel |
| `GET` | `/risk-distribution` | 🔒 | Workspace risk distribution |
| `GET` | `/trends` | 🔒 | Workspace 6-month metric trends |
| `GET` | `/top-entities` | 🔒 | Workspace top deals and clients |
| `GET` | `/member-activity` | 🔒 | Workspace member activity statistics |

---

## 15. Audit Log

**Service file:** [Audit/src/Relativa.Audit/Endpoints/AuditEndpoints.cs](../Audit/src/Relativa.Audit/Endpoints/AuditEndpoints.cs)
**Client file:** [Client/src/api/audit.ts](../Client/src/api/audit.ts)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/audit-log` | 🔒 | Paginated global audit log |
| `GET` | `/entities/{entityId}/audit-log` | 🔒 | Audit log scoped to a specific entity |

**Supported query parameters:**

| Parameter | Description |
|-----------|-------------|
| `entity_type` | Filter by entity type |
| `scope` | Log scope filter |
| `date_from` / `from` | Start of date range |
| `date_to` / `to` | End of date range |
| `action` | Action type filter |
| `index` | Page index |
| `page_size` | Page size |
| `entity_id` | Filter by entity |
| `targetId` | Filter by target |
| `domain_entity_type` | Domain-level entity type |
| `workspace_id` | Filter by workspace |
| `organization_id` | Filter by organization |
| `actor_user_id` | Filter by actor user |
| `target_user_id` | Filter by target user |

---

## 16. ML Scoring

**Client file:** [Client/src/api/ml.ts](../Client/src/api/ml.ts)
**Base path:** `/api/ml`

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `POST` | `/score/batch` | 🔒 | Batch-score entities for deal closure / churn probability |

---

## 17. Real-time Hubs (SignalR)

**Client files:**
- [Client/src/api/graphHub.ts](../Client/src/api/graphHub.ts)
- [Client/src/api/coreHub.ts](../Client/src/api/coreHub.ts)

| Hub URL | Client File | Purpose |
|---------|-------------|---------|
| `/graph/hubs/graph` | `graphHub.ts` | Real-time graph node / edge updates |
| `/core/hubs/core` | `coreHub.ts` | Real-time entity and core data updates |

> SignalR connections require a valid JWT token passed during the handshake.

---

## 18. Gateway Routing

**File:** [Gateway/src/Relativa.Gateway/OpenApi/AggregatedOpenApiEndpoint.cs](../Gateway/src/Relativa.Gateway/OpenApi/AggregatedOpenApiEndpoint.cs)

The API Gateway is the single entry point for all external traffic. It:

1. **Validates** JWT Bearer tokens on every protected route
2. **Injects** `X-User-Id` and `X-User-Email` headers before forwarding
3. **Routes** requests to the appropriate downstream service:

| Path Prefix | Downstream Service |
|-------------|-------------------|
| `/auth/*` | Authentication service |
| `/core/*` | Core service |
| `/graph/*` | Graph service |
| `/audit/*` | Audit service |
| `/ml/*` | ML scoring service |

---

## Summary

| Domain | Endpoints | Auth-free |
|--------|-----------|-----------|
| Authentication | 28 | 10 |
| Support | 1 | 1 |
| Organizations | 10 | 0 |
| Org Members | 3 | 0 |
| Org Roles | 4 | 0 |
| Org Invitations | 4 | 0 |
| Org Join Requests | 3 | 0 |
| Workspaces | 7 | 0 |
| Workspace Members | 4 | 0 |
| Workspace Roles | 4 | 0 |
| Permissions | 1 | 0 |
| Invitations (user) | 4 | 0 |
| Join Requests (user) | 2 | 0 |
| Entities | 5 | 0 |
| Entity Types | 1 | 0 |
| Entity Relationships | 3 | 0 |
| Entity Graph RPC | 1 | 0 |
| Graph Query | 1 | 0 |
| Dashboard (org) | 6 | 0 |
| Dashboard (workspace) | 6 | 0 |
| Audit Log | 2 | 0 |
| ML Scoring | 1 | 0 |
| **Total** | **101** | **11** |
