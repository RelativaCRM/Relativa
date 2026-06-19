# Relativa CRM — Endpoint Monetization Tiers

> This document classifies all 95 API endpoints across three pricing tiers.
> Use it as the foundation for implementing rate-limiting middleware and feature gates in the API Gateway.

---

## Tier Legend

| Symbol | Tier | Description |
|--------|------|-------------|
| 🆓 | **Starter** | Fully available at no cost — no volume restrictions |
| 💎 | **Growth** | Requires a Growth subscription to access |
| ♾️ | **Enterprise** | Accessible on all tiers but rate-limited on Starter/Growth; the Enterprise plan removes all caps |

> Endpoints marked ♾️ are always reachable but return `429 Too Many Requests` once the tier quota is exceeded.

---

## 1. Authentication — 🆓 Starter

**Base path:** `/api/v1/auth`

**Rationale:** Authentication is the entry point to the product. Gating login, registration, or account recovery would kill adoption and creates legal/ethical issues (users have a right to access and delete their own data).

### Identity & Session

| Tier | Method | Path | Note |
|------|--------|------|------|
| 🆓 | `POST` | `/api/v1/auth/login` | Entry point — must always be free |
| 🆓 | `POST` | `/api/v1/auth/oauth/{provider}` | OAuth login |
| 🆓 | `GET` | `/api/v1/auth/exists` | Pre-registration email check |
| 🆓 | `POST` | `/api/v1/auth/register` | User acquisition funnel |
| 🆓 | `POST` | `/api/v1/auth/verify-email` | Required to activate account |
| 🆓 | `POST` | `/api/v1/auth/resend-verification` | Account activation support |
| 🆓 | `GET` | `/api/v1/auth/verification-channels` | Pre-registration UX metadata |

### Password Recovery

| Tier | Method | Path | Note |
|------|--------|------|------|
| 🆓 | `POST` | `/api/v1/auth/forgot-password` | Security hygiene — must be free |
| 🆓 | `GET` | `/api/v1/auth/reset-password/validate` | Password recovery flow |
| 🆓 | `POST` | `/api/v1/auth/reset-password` | Password recovery flow |

### Profile Management

| Tier | Method | Path | Note |
|------|--------|------|------|
| 🆓 | `GET` | `/api/v1/auth/me` | Core profile read |
| 🆓 | `PATCH` | `/api/v1/auth/me` | Core profile update |
| 🆓 | `DELETE` | `/api/v1/auth/me` | GDPR right-to-erasure — must be free |
| 🆓 | `GET` | `/api/v1/auth/me/settings` | User preferences |
| 🆓 | `PATCH` | `/api/v1/auth/me/settings` | User preferences |

### Two-Factor Authentication

| Tier | Method | Path | Note |
|------|--------|------|------|
| 🆓 | `GET` | `/api/v1/auth/me/2fa` | 2FA status |
| 🆓 | `POST` | `/api/v1/auth/me/2fa/setup` | Security feature — must be free |
| 🆓 | `POST` | `/api/v1/auth/me/2fa/enable` | Security feature |
| 🆓 | `POST` | `/api/v1/auth/me/2fa/disable` | Security feature |
| 🆓 | `POST` | `/api/v1/auth/me/2fa/backup-codes` | Account recovery |
| 🆓 | `POST` | `/api/v1/auth/me/2fa/master-code` | Account recovery |

### Email Management

| Tier | Method | Path | Note |
|------|--------|------|------|
| 🆓 | `GET` | `/api/v1/auth/me/emails` | Email address list |
| 🆓 | `POST` | `/api/v1/auth/me/emails` | Add email address |
| 🆓 | `POST` | `/api/v1/auth/me/emails/verify` | Email verification |
| 🆓 | `POST` | `/api/v1/auth/me/emails/resend` | Verification resend |
| 🆓 | `POST` | `/api/v1/auth/me/emails/primary` | Set primary address |
| 🆓 | `POST` | `/api/v1/auth/me/emails/remove` | Remove address |

### OAuth Connections

| Tier | Method | Path | Note |
|------|--------|------|------|
| 🆓 | `POST` | `/api/v1/auth/me/connections/{provider}` | Link OAuth provider |

---

## 2. Support — 🆓 Starter

**Base path:** `/api/v1/support`

**Rationale:** All users — regardless of tier — must be able to reach support. Paid tiers may receive priority response times, but the contact channel itself must remain accessible.

| Tier | Method | Path | Note |
|------|--------|------|------|
| 🆓 | `POST` | `/api/v1/support/contact` | Support contact for all tiers |

---

## 3. Organizations — 🆓 Starter / ♾️ Enterprise

**Base path:** `/api/v1/organizations`

**Rationale:** Creating and managing an organization is the starting action in Relativa. Basic CRUD is free to eliminate friction at the onboarding step. Bulk user import (`POST /{orgId}/users`) is rate-limited because it triggers database writes and email delivery at scale.

### Core Organization CRUD

| Tier | Method | Path | Note |
|------|--------|------|------|
| 🆓 | `POST` | `/api/v1/organizations/` | Organization creation |
| 🆓 | `GET` | `/api/v1/organizations/` | User's organization list |
| 🆓 | `GET` | `/api/v1/organizations/search` | Search by name |
| 🆓 | `GET` | `/api/v1/organizations/{id}` | Organization details |
| 🆓 | `PUT` | `/api/v1/organizations/{id}` | Organization update |
| 🆓 | `GET` | `/api/v1/organizations/{id}/settings` | Settings read |
| 🆓 | `PUT` | `/api/v1/organizations/{id}/settings` | Settings update |
| ♾️ | `POST` | `/api/v1/organizations/{organizationId}/users` | Bulk user import — Starter: 50/day · Growth: 500/day · Enterprise: no cap |
| 🆓 | `PATCH` | `/api/v1/organizations/{organizationId}/users/{userId}` | User profile update |
| 🆓 | `DELETE` | `/api/v1/organizations/{organizationId}/users/{userId}` | User removal |

### 3.1 Organization Members — 🆓 Starter

| Tier | Method | Path | Note |
|------|--------|------|------|
| 🆓 | `GET` | `/api/v1/organizations/{organizationId}/members` | Member list |
| 🆓 | `DELETE` | `/api/v1/organizations/{organizationId}/members/{userId}` | Remove member |
| 🆓 | `PUT` | `/api/v1/organizations/{organizationId}/members/{userId}/role` | Change member role |

### 3.2 Organization Roles — 🆓 Starter

**Rationale:** Listing available roles is required by the UI for any user to configure member access.

| Tier | Method | Path | Note |
|------|--------|------|------|
| 🆓 | `GET` | `/api/v1/organizations/{organizationId}/roles` | List built-in roles |

### 3.3 Organization Invitations — 🆓 Starter

| Tier | Method | Path | Note |
|------|--------|------|------|
| 🆓 | `POST` | `/api/v1/organizations/{organizationId}/invitations` | Invite user — team growth funnel |
| 🆓 | `GET` | `/api/v1/organizations/{organizationId}/invitations` | List pending invitations |
| 🆓 | `DELETE` | `/api/v1/organizations/{organizationId}/invitations/{invitationId}` | Cancel invitation |
| 🆓 | `POST` | `/api/v1/organizations/{organizationId}/invitations/{invitationId}/resend` | Resend invitation |

### 3.4 Organization Join Requests — 🆓 Starter

| Tier | Method | Path | Note |
|------|--------|------|------|
| 🆓 | `POST` | `/api/v1/organizations/{organizationId}/join-requests` | Submit join request |
| 🆓 | `GET` | `/api/v1/organizations/{organizationId}/join-requests` | Admin view of requests |
| 🆓 | `PUT` | `/api/v1/organizations/{organizationId}/join-requests/{requestId}` | Approve or decline |

---

## 4. Workspaces — 🆓 Starter / ♾️ Enterprise

**Base path:** `/api/v1/workspaces`

**Rationale:** Workspaces are the primary collaboration unit. Navigating and configuring workspaces must be free to support the core product loop. Creating additional workspaces is a Growth feature — Starter plans are limited to a single shared workspace, aligned with the pricing tier (Multi-workspaces unlock at $19/user/month Growth).

| Tier | Method | Path | Note |
|------|--------|------|------|
| ♾️ | `POST` | `/api/v1/workspaces/` | Create workspace — Starter: 1 workspace · Growth: unlimited · Enterprise: unlimited |
| 🆓 | `GET` | `/api/v1/workspaces/` | List workspaces |
| 🆓 | `GET` | `/api/v1/workspaces/{id}` | Workspace details |
| 🆓 | `PUT` | `/api/v1/workspaces/{id}` | Update workspace |
| 🆓 | `DELETE` | `/api/v1/workspaces/{id}` | Archive workspace |
| 🆓 | `GET` | `/api/v1/workspaces/{id}/settings` | Settings read |
| 🆓 | `PUT` | `/api/v1/workspaces/{id}/settings` | Settings update |

### 4.1 Workspace Members — 🆓 Starter

| Tier | Method | Path | Note |
|------|--------|------|------|
| 🆓 | `GET` | `/api/v1/workspaces/{workspaceId}/members` | Member list |
| 🆓 | `POST` | `/api/v1/workspaces/{workspaceId}/members` | Add member |
| 🆓 | `PUT` | `/api/v1/workspaces/{workspaceId}/members/{userId}/role` | Update member role |
| 🆓 | `DELETE` | `/api/v1/workspaces/{workspaceId}/members/{userId}` | Remove member |

### 4.2 Workspace Roles — 🆓 Starter

**Rationale:** Listing available roles is required by the UI for any user to configure workspace member access.

| Tier | Method | Path | Note |
|------|--------|------|------|
| 🆓 | `GET` | `/api/v1/workspaces/{workspaceId}/roles` | List roles |

---

## 5. Invitations (user-scoped) — 🆓 Starter

**Base path:** `/api/v1/invitations`

| Tier | Method | Path | Note |
|------|--------|------|------|
| 🆓 | `POST` | `/api/v1/invitations/accept-org` | Onboarding flow |
| 🆓 | `POST` | `/api/v1/invitations/decline-org` | Onboarding flow |
| 🆓 | `GET` | `/api/v1/invitations/mine` | User invitation inbox |
| 🆓 | `GET` | `/api/v1/invitations/mine/organization` | Organization invitations |

---

## 6. Join Requests (user-scoped) — 🆓 Starter

**Base path:** `/api/v1/join-requests`

| Tier | Method | Path | Note |
|------|--------|------|------|
| 🆓 | `GET` | `/api/v1/join-requests/mine` | User's submitted requests |
| 🆓 | `DELETE` | `/api/v1/join-requests/mine/{requestId}` | Cancel join request |

---

## 7. Entities — 🆓 Starter / ♾️ Enterprise

**Base path:** `/api/v1/workspaces/{workspaceId}/entities`

**Rationale:** Viewing, editing, and archiving records must be free — these are the core CRM operations. Entity creation is rate-limited because each record consumes server-side storage and triggers graph index updates. High-volume data import is an enterprise use case.

| Tier | Method | Path | Note |
|------|--------|------|------|
| 🆓 | `GET` | `/api/v1/workspaces/{workspaceId}/entities/` | List entities with filtering and pagination |
| 🆓 | `GET` | `/api/v1/workspaces/{workspaceId}/entities/{entityId}` | Entity detail view |
| ♾️ | `POST` | `/api/v1/workspaces/{workspaceId}/entities/` | Create entity — Starter: 100/day · Growth: 1,000/day · Enterprise: no cap |
| 🆓 | `PATCH` | `/api/v1/workspaces/{workspaceId}/entities/{entityId}` | Partial entity update |
| 🆓 | `DELETE` | `/api/v1/workspaces/{workspaceId}/entities/{entityId}` | Archive entity |

---

## 8. Entity Types — 🆓 Starter

**Base path:** `/api/v1/entity-types`

| Tier | Method | Path | Note |
|------|--------|------|------|
| 🆓 | `GET` | `/api/v1/entity-types/` | Schema metadata required by the UI |

---

## 9. Entity Relationships — 🆓 Starter

**Base path:** `/api/v1/workspaces/{workspaceId}/entity-relationships`

**Rationale:** Linking entities (e.g., connecting a Contact to a Deal) is core CRM behaviour that must be accessible on the free tier.

| Tier | Method | Path | Note |
|------|--------|------|------|
| 🆓 | `POST` | `/api/v1/workspaces/{workspaceId}/entity-relationships/` | Create relationship |
| 🆓 | `PUT` | `/api/v1/workspaces/{workspaceId}/entity-relationships/{relationshipId}` | Reassign relationship |
| 🆓 | `DELETE` | `/api/v1/workspaces/{workspaceId}/entity-relationships/{relationshipId}` | Remove relationship |

---

## 10. Entity Graph RPC — ♾️ Enterprise

**Base path:** `/api/v1/workspaces/{workspaceId}/entity-graph`

**Rationale:** The Graph RPC endpoint performs a composite transaction: it creates an entity and its relationships in a single database operation, updating the graph index atomically. This is significantly more compute-intensive than a simple entity creation. Rate-limited across all tiers; the Enterprise plan removes the cap for teams doing bulk data migrations.

| Tier | Method | Path | Note |
|------|--------|------|------|
| ♾️ | `POST` | `/api/v1/workspaces/{workspaceId}/entity-graph/create` | Composite creation — Starter: 20/day · Growth: 200/day · Enterprise: no cap |

---

## 11. Graph Query — 💎 Growth

**Base path:** `/api/v1/graph`

**Rationale:** Visual network analysis — seeing the full entity graph with risk-level filters — is an advanced intelligence feature. It requires real-time graph traversal across potentially thousands of nodes and edges. This is the core visual differentiator of Relativa vs. conventional CRMs, and belongs in the Growth tier.

| Tier | Method | Path | Note |
|------|--------|------|------|
| 💎 | `GET` | `/api/v1/graph/` | Graph visualization with risk-level filtering — Growth feature |

---

## 12. Dashboard — Organization — 💎 Growth

**Base path:** `/api/v1/dashboard`

**Rationale:** Dashboard endpoints aggregate and compute KPIs across the entire organization's dataset. Cross-workspace comparison is particularly valuable for managers and executives who need portfolio-level visibility. This is business intelligence functionality that power users pay for.

| Tier | Method | Path | Note |
|------|--------|------|------|
| 💎 | `GET` | `/api/v1/dashboard/summary` | Org-wide KPI summary |
| 💎 | `GET` | `/api/v1/dashboard/pipeline` | Deal pipeline funnel |
| 💎 | `GET` | `/api/v1/dashboard/risk-distribution` | Risk portfolio breakdown |
| 💎 | `GET` | `/api/v1/dashboard/trends` | 6-month metric trends |
| 💎 | `GET` | `/api/v1/dashboard/top-entities` | Top deals and clients |
| 💎 | `GET` | `/api/v1/dashboard/workspaces-comparison` | Cross-workspace benchmarking — highest-value insight for leadership |

---

## 13. Dashboard — Workspace — 💎 Growth

**Base path:** `/api/v1/dashboard/workspace/{workspaceId}`

**Rationale:** Workspace-level analytics including member activity tracking directly supports team performance management. These six endpoints together form a complete operational dashboard for team leads.

| Tier | Method | Path | Note |
|------|--------|------|------|
| 💎 | `GET` | `/api/v1/dashboard/workspace/{workspaceId}/summary` | Workspace KPI summary |
| 💎 | `GET` | `/api/v1/dashboard/workspace/{workspaceId}/pipeline` | Workspace pipeline funnel |
| 💎 | `GET` | `/api/v1/dashboard/workspace/{workspaceId}/risk-distribution` | Workspace risk breakdown |
| 💎 | `GET` | `/api/v1/dashboard/workspace/{workspaceId}/trends` | Workspace 6-month trends |
| 💎 | `GET` | `/api/v1/dashboard/workspace/{workspaceId}/top-entities` | Top workspace records |
| 💎 | `GET` | `/api/v1/dashboard/workspace/{workspaceId}/member-activity` | Team productivity stats |

---

## 14. Audit Log — ♾️ Enterprise

**Rationale:** A compliance-grade audit trail with 13 filter parameters (date range, actor, target, workspace, organization, action type, etc.) is a governance feature aligned with the Enterprise tier ($49/user/month). Regulated industries and larger teams need this for security audits, compliance reviews, and incident investigations. The Enterprise plan delivers the full extended log with all filter capabilities.

| Tier | Method | Path | Note |
|------|--------|------|------|
| ♾️ | `GET` | `/audit-log` | Extended audit log — 13 filter parameters — Enterprise feature |

---

## 15. ML Scoring — 💎 Growth / ♾️ Enterprise

**Base path:** `/api/ml`

**Rationale:** ML inference is compute-intensive and uses a dedicated ML microservice. The feature is unlocked at Growth to cover infrastructure costs. Batch size is capped on Growth (≤ 100 entities per call) and unlimited on the Enterprise plan to support large-scale scoring pipelines.

| Tier | Method | Path | Note |
|------|--------|------|------|
| 💎/♾️ | `POST` | `/api/ml/score/batch` | Deal closure + churn scoring — Growth: batch ≤ 100 · Enterprise: no batch cap |

---

## 16. Real-time Hubs (SignalR) — ♾️ Enterprise

**Rationale:** Real-time updates are a baseline collaborative feature — the product works noticeably worse without them. Available on all tiers, but concurrent WebSocket connection count is capped. Large teams with many browser sessions simultaneously open benefit from the Enterprise plan.

| Tier | Hub URL | Note |
|------|---------|------|
| ♾️ | `/graph/hubs/graph` | Real-time graph updates — Starter: 3 concurrent · Growth: 20 concurrent · Enterprise: no cap |
| ♾️ | `/core/hubs/core` | Real-time entity updates — Starter: 3 concurrent · Growth: 20 concurrent · Enterprise: no cap |

---

## Summary

| Domain | Endpoints | 🆓 Starter | 💎 Growth | ♾️ Enterprise |
|--------|-----------|---------|--------|-------------|
| Authentication | 28 | 28 | — | — |
| Support | 1 | 1 | — | — |
| Organizations | 10 | 9 | — | 1 |
| Org Members | 3 | 3 | — | — |
| Org Roles | 1 | 1 | — | — |
| Org Invitations | 4 | 4 | — | — |
| Org Join Requests | 3 | 3 | — | — |
| Workspaces | 7 | 6 | — | 1 |
| Workspace Members | 4 | 4 | — | — |
| Workspace Roles | 1 | 1 | — | — |
| Invitations (user) | 4 | 4 | — | — |
| Join Requests (user) | 2 | 2 | — | — |
| Entities | 5 | 4 | — | 1 |
| Entity Types | 1 | 1 | — | — |
| Entity Relationships | 3 | 3 | — | — |
| Entity Graph RPC | 1 | — | — | 1 |
| Graph Query | 1 | — | 1 | — |
| Dashboard (org) | 6 | — | 6 | — |
| Dashboard (workspace) | 6 | — | 6 | — |
| Audit Log | 1 | — | — | 1 |
| ML Scoring | 1 | — | 1* | — |
| SignalR Hubs | 2 | — | — | 2 |
| **Total** | **95** | **74** | **14** | **7** |

> \* ML Scoring is unlocked at **Growth** but the Enterprise plan removes the batch size cap.
> ♾️ Enterprise endpoints are accessible at lower tiers but subject to daily/concurrent limits shown in each section.
