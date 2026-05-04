# Project Status -- What is Done and What is Not

> **Last verified:** 2026-05-04 (Users: partial unique index on `users.email` where `is_archived = false`; `ExistsAsync` ignores archived rows so register / org user create can reuse an email after soft-delete.)

> **Maintenance obligation:** If you implement a feature that was listed as stub or TODO, move it to the "Implemented" section. If you introduce a new known issue or break something, add it to "Known Issues." Always update the "Last verified" date. See [AI-GUIDES-INDEX.md](../../AI-GUIDES-INDEX.md) for the full update matrix.

---

## Status Summary

| Service | Status | One-line summary |
|---|---|---|
| Gateway | **Functional** | YARP routing, JWT validation, split anonymous/auth routes, health, Scalar -- all working |
| Authentication | **Functional** | Login, register, `/me` read + PATCH + DELETE, email normalization, JWT (sub + email only), FluentValidation -- all working |
| Core | **Functional** (org + ws RBAC + entity CRUD) | Organization management, workspace management, split RBAC, members, org-scoped user provisioning, **organization** invitations and join requests, permissions, entity-type listing (public), and workspace-scoped entity CRUD all implemented |
| Graph | **Stub** | SignalR hub + RabbitMQ choreography consumer broadcasts workspace lifecycle payloads; choreography idempotency via EF `GraphDbContext` on `rabbitmq_processed_delivery`; graph projection logic still absent |
| Audit | **Functional** | Consumes RabbitMQ events and persists audit logs with idempotency |
| Migration | **Functional** | Applies EF migrations on startup; schema + seed data work, including outbox/idempotency tables |
| ML | **Stub** | REST stub unchanged; **`run_domain_consumer`** processes choreography events (stub logging path) beside `runserver` in Docker |
| Client | **Functional** | Vue 3 + PrimeVue + Tailwind. Auth + org/workspace onboarding + account/profile + org members (invite, roles, join-request review by `manage_join_requests`) + invitations inbox (org only) + workspace members (**add** existing org user with `add_ws_members` or org `manage_org_workspace_members`) + entity CRUD UI + graph placeholder, typed API clients. Persisted state via `localStorage` |
| Persistence | **Functional** | Full EAV + audit/outbox/idempotency entity model, fluent configs, contracts (`Persistence/Contracts/*`), ModelBuilderExtensions; performance indexes on membership/org invitations/audit logs/outbox + unique `entity_workspace (entity_id, workspace_id)` + partial unique `users.email` where not archived |

---

## Implemented (working)

### Authentication service

- `POST /api/v1/auth/register` -- creates user, hashes password with bcrypt, stores email lowercase, returns user DTO with 201 + Location header. Newly registered users have no organization or workspace membership. **Re-registration:** after an account was soft-archived (`DELETE /me` or org admin delete), the same normalized email may be used again (new `users` row); uniqueness applies only to non-archived rows.
- `POST /api/v1/auth/login` -- validates credentials (email compared after lowercase normalization), issues JWT with claims: `sub`, `email`, `jti`. Role and permissions are **not** included in the JWT -- they are resolved per-request by Core using organization/workspace membership.
- `GET /api/v1/auth/me` -- returns authenticated user's profile (`id`, `email`, `firstName`, `lastName`) from JWT `sub` claim. Requires valid JWT.
- `PATCH /api/v1/auth/me` -- updates first and last name for the authenticated user.
- `DELETE /api/v1/auth/me` -- soft-archives the authenticated user (`users.is_archived`); the email is then free for a new registration subject to the partial unique index and `IUserRepository.ExistsAsync` (active users only).
- Full clean-architecture layers: Domain interfaces, Application services (`AuthService`, `UserProvisioningService`) + DTOs + FluentValidation validators, Infrastructure (AuthDbContext, UserRepository, JwtTokenService, BcryptPasswordHasher).
- GlobalExceptionHandler maps ValidationException -> 400, UnauthorizedAccessException -> 401, KeyNotFoundException -> 404, duplicate **active** email -> 409 (including PostgreSQL unique violation on concurrent register of two non-archived users with the same email).
- EF Core health check at `/health`.
- OpenAPI + Scalar docs.
- Fire-and-forget audit publishing: register flow writes to `audit_outbox`, dispatcher publishes to RabbitMQ.

### Core service -- Organization management

- **Organization CRUD:** `POST /api/v1/organizations` (create, creator becomes `org_owner`), `GET` (list user's orgs), `GET /search?q=...` (search by name), `GET /{id}` (details, requires org membership), `PUT /{id}` (update, requires `manage_org_settings`).
- **Organization members:** `GET .../members` (list, requires org membership), `DELETE .../members/{userId}` (remove membership, requires `remove_org_members`), `PUT .../members/{userId}/role` (change role, requires `assign_org_roles`).
- **Organization users (account provisioning):** `POST .../users` (create user + add as `org_member`, requires `create_org_users`), `PATCH .../users/{userId}` (edit another member's name, requires `edit_other_org_users_profile`), `DELETE .../users/{userId}` (archive user account, requires `delete_org_users`). Implemented via Core orchestration + shared `UserProvisioningService` from Authentication.Application. Creating a user with an email that exists only on archived accounts succeeds (same rules as self-register).
- **Join requests:** `POST .../join-requests` (request to join), `GET .../join-requests` (list pending, requires `manage_join_requests`), `PUT .../join-requests/{reqId}` (approve/reject, requires `manage_join_requests`), `GET /api/v1/join-requests/mine` (own requests). Protected by a partial unique index `(organization_id, user_id) WHERE status='Pending'` so a user cannot file two simultaneous requests for the same org.
- **Organization invitations:** `POST .../invitations` (invite by email + optional `orgRoleId`, requires `invite_to_org`; non-default role additionally requires `assign_org_roles`), `GET .../invitations` (list pending, non-expired, includes `roleName`), `DELETE .../invitations/{invId}` (cancel), `POST .../invitations/{invId}/resend` (rotate token + extend expiry), `POST /api/v1/invitations/accept-org` (accept — adds user with the invitation's recorded role). Protected by a partial unique index `(organization_id, lower(email)) WHERE status='Pending'` and an in-service check that the email does not already resolve to an existing org member.
- **Organization roles:** `GET .../roles` (list system + custom, requires org membership), `POST .../roles` (create custom, requires `manage_org_roles`), `PUT .../roles/{roleId}` (update), `DELETE .../roles/{roleId}` (delete). System roles cannot be modified.

### Core service -- Workspace RBAC

- **Workspace CRUD:** `POST /api/v1/workspaces` (create within an org, requires `create_workspaces` org perm + `organizationId`; creator becomes `ws_admin`), `GET` (list user's workspaces), `GET /{id}`, `PUT /{id}` (requires `manage_ws_settings`), `DELETE /{id}` (archive, requires `ws_admin` role).
- **Member management:** `GET .../members`, `POST .../members` (add org member; requires workspace `add_ws_members` **or** org `manage_org_workspace_members` on the parent organization), `PUT .../members/{userId}/role` (requires `assign_ws_roles`), `DELETE .../members/{userId}` (requires `remove_ws_members` or org `manage_org_workspace_members`, or self-remove).
- **Role management:** `GET .../roles` (list system + custom), `POST .../roles` (create custom, requires `manage_ws_roles`), `PUT .../roles/{id}` (update), `DELETE .../roles/{id}` (archive). System roles cannot be modified.
- **Combined invitations inbox:** `GET /api/v1/invitations/mine` — pending **organization** invitations for the caller's email (`{ organizationInvitations }`); `GET /api/v1/invitations/mine/organization` — same as flat list. `POST /api/v1/invitations/accept-org` — accept org invite (documented under organization invitations).
- **Permission listing:** `GET /api/v1/permissions` — lists rows in `permissions` (migration `RemoveWorkspaceInvitationFlows` removes `invite_to_workspace` and `manage_ws_join_requests`, adds org `manage_org_workspace_members`). Workspace permissions **14 / 15** remain `manage_entities` / `view_entities` from `ReseedPermissions`.
- Full clean-architecture layers: Domain (repository interfaces), Application (10 services, DTOs, validators), Infrastructure (repositories, WorkspaceContext).
- Authorization checked per-request via `UserRoleOrganization` or `UserRoleWorkspace` DB lookup. **Core does not parse JWTs**; it reads the caller identity from the `X-User-Id` header that the Gateway injects after JWT validation (see Gateway entry below). `X-User-Email` is read on invitation-accept flows. Missing headers are treated as a 401.

### Core service -- Entity CRUD

- **Entity types (public):** `GET /api/v1/entity-types` — anonymous (no JWT required via Gateway); returns all entity types with their EAV property definitions (`id`, `name`, `properties: [{ propertyId, name, dataType, isRequired }]`).
- **Entity CRUD (workspace-scoped):** all endpoints under `/api/v1/workspaces/{workspaceId}/entities`:
  - `GET /` — list non-archived entities with full property values; requires `view_entities`.
  - `GET /{entityId}` — entity detail; **404** if entity not linked to this workspace; requires `view_entities`.
  - `POST /` — atomic transaction: insert `entity` row, insert `entity_property_value` rows, insert `entity_workspace` link; requires `manage_entities`. FluentValidation (structural) + service-level EAV validation (required properties, allowed property ids, typed value parsing).
  - `PUT /{entityId}` — replace all property values; requires `manage_entities`.
  - `DELETE /{entityId}` — soft-delete (`is_archived = true`); requires `manage_entities`.
- **GlobalExceptionHandler extended:** `KeyNotFoundException` → 404, `ValidationException` → 400 with error detail.
- Fire-and-forget audit publishing for core write flows via `audit_outbox` + RabbitMQ dispatcher, including organizations/workspaces/entities, join requests, invitations, membership updates, and role lifecycle changes.

### Gateway

- YARP reverse proxy with 5 route groups: `/auth/*`, `/core/*`, `/graph/*`, `/ml/*`, `/audit/*`.
- Path prefix stripping via `PathRemovePrefix` transforms.
- JWT Bearer authentication with full validation (issuer, audience, signing key, lifetime). **Core** does not re-validate tokens (uses forwarded `X-User-Id`). **Authentication** validates its own tokens for `/me`. **Audit** re-validates JWTs for its read API (configuration aligned with Gateway). **Graph/ML** are stubs.
- **Global authorization via `MapReverseProxy().RequireAuthorization()`.** Every proxied route requires a valid JWT unless it is explicitly marked `AuthorizationPolicy: Anonymous` in `appsettings.json`.
- **Identity forwarding via YARP request transform:** on every proxied request the Gateway unconditionally strips any incoming `X-User-Id` / `X-User-Email` headers, then re-adds them from the validated `ClaimsPrincipal` (`sub` and `email` claims). **Core** trusts these headers and does not parse JWTs. **Audit** validates JWTs (defense in depth) and still accepts forwarded headers when resolving identity. Client-supplied header values are always overwritten at the Gateway.
- **Split anonymous/auth routes:** `/login` and `/register` are anonymous; `/me` (GET/PATCH/DELETE) requires JWT.
- Anonymous exceptions for health endpoints.
- **CORS:** named-origin allowlist with credentials, reading `Cors:Origins` from config (defaults to `http://localhost:5173` and `http://localhost:3000`).
- Forwarded headers (`X-Forwarded-For`, `X-Forwarded-Proto`).
- Serilog request logging.
- GlobalExceptionHandler.
- OpenAPI + Scalar docs.
- Health endpoint at `/health`.

### Migration

- `MigrationDbContext` mirrors full Persistence model (20 entities after removal of workspace invitation entities).
- `Program.cs` runs `Database.MigrateAsync()` as a generic host console app.
- Migrations in `Migration/src/Relativa.Migration/Migrations/` include (non-exhaustive):
  - `20260416224419_InitialCreate.cs` — full initial schema (RBAC, org management, old polymorphic entity tables).
  - `20260416224514_SeedData.cs` — seeds all reference data. Permission ids 14/15 are `manage_entities`/`view_entities`.
  - `20260423000000_EavSchemaReplace.cs` — EAV schema migration: drops old property tables, renames entity tables to singular, creates new EAV tables.
  - `20260423100000_ReseedPermissions.cs` — FK-safe wipe and full re-insert of `permissions`, `organization_role_permissions`, `workspace_role_permissions`; replaces `edit_deals`/`view_deals` with `manage_entities`/`view_entities` for existing databases.
  - `20260503194004_AddPerformanceIndexes.cs` — composite/DESC indexes for RBAC/invitations/join requests/audit tables/outbox; drops redundant single-column indexes; unique constraint on `entity_workspace (entity_id, workspace_id)`.
  - `20260503235000_InvitationSystemProdGrade.cs` — adds `organization_invitations.org_role_id` (FK, backfilled to `org_member`); creates `workspace_join_requests` table; adds partial unique indexes on pending org/workspace invitations and join requests; seeds `manage_ws_join_requests` (id 20) for `ws_admin`. **Superseded for fresh DBs** by `20260504180000_RemoveWorkspaceInvitationFlows.cs`, which drops workspace invitation/join-request tables and permission ids 9/20 and adds `manage_org_workspace_members` (id 21).
- Docker Compose runs this before auth and core start.

### Persistence library

- 20 CRM-facing entity classes in the Core/Migration model (workspace invitation / workspace join-request entities removed; performance indexes unchanged for remaining tables).
- Split RBAC model: separate `OrganizationRole`/`OrganizationRolePermission` and `WorkspaceRole`/`WorkspaceRolePermission` hierarchies sharing a common `Permission` table.
- **EAV entity model:** `Property`, `EntityTypeProperty`, `EntityPropertyValue`, `EntityRelationshipType`, `EntityRelationship` replace the old hard-coded `EntityProperty` / `PersonalDataPropertyValue` / `LocationPropertyValue` / `DealPropertyValue` tables.
- `ModelBuilderExtensions.ApplyAuthEntityConfigurations()` for auth-only subset (User).
- `ModelBuilderExtensions.ApplyAllEntityConfigurations()` for full model mapped by Core/Migration contexts.
- Referenced by Core, Authentication, and Migration via ProjectReference.

### Docker Compose

- Full 10-service stack with dependency ordering.
- Single bridge network `relativa_net`.
- Named volume `postgres_data` for persistent DB.
- Environment variable injection for DB, JWT, and YARP config.
- Migration runs to completion before dependent services start.

### Client

- Vue 3 + Vite scaffold with TypeScript, Pinia, Vue Router.
- **UI stack:** PrimeVue 4 (Aura preset) + Tailwind CSS 3 (`tailwindcss-primeui` bridge) + Inter font.
- **Typed API client** (`src/api/http.ts`): `gatewayFetch` with JWT + `X-Workspace-ID` headers (workspace id read from the workspace store), `ApiError` class, JSON helpers (`api.get/post/put/patch/del`). Auto session clear on `401` is **scoped to auth endpoints only** (`/auth/me`, `/auth/refresh`) — generic `401`s on business endpoints surface as `ApiError` so a transient permission change does not log the user out.
- **Centralized error handling** (CR-127, `src/api/errors.ts` + `src/api/errorToast.ts`): `normalizeError(err, fallback)` consumes any `ApiError` / `Error` / network failure and returns a `NormalizedError` with friendly `message`, status flags (`isValidation`, `isUnauthorized`, `isForbidden`, `isNotFound`, `isConflict`, `isServer`, `isNetwork`), and a `fieldErrors` map parsed from the backend `{ status, title, detail }` shape (FluentValidation `detail` of the form `"Field: msg; Field2: msg"` is split into per-field arrays; field names are lower-cased to match camelCase form bindings). `useApiErrorHandler()` is a thin composable over PrimeVue's `useToast()` — `notify(err, { fallback, summary, silent })` shows a categorized red error toast and returns the same `NormalizedError` so callers can also react to specific statuses. Forms render server `fieldErrors` inline under the corresponding inputs (red text + `pi pi-exclamation-circle`); each input clears its own server error on `update:model-value`. List/dialog views display non-form errors via toasts (replaces previous silent `catch {}` blocks).
- **Auth service** (`src/api/auth.ts`): `authApi.register`, `authApi.login`, `authApi.me`, `authApi.updateMe` (PATCH `/me`), `authApi.deleteMe` (DELETE `/me`).
- **Organization service** (`src/api/organizations.ts`): org CRUD, members, invitations, join requests, roles, combined invitations (`/invitations/mine`), `orgApi.updateOrgUserProfile` (PATCH `.../organizations/{id}/users/{userId}` — other users only; requires org permission `edit_other_org_users_profile` on the backend).
- **Workspace service** (`src/api/workspaces.ts`): workspace CRUD, members, roles, invitations.
- **Entity service** (`src/api/entities.ts`): typed DTOs for `EntityTypeDto` / `EntityTypePropertyDto` / `EntityDetailDto`; `entityApi.listTypes()` (`GET /entity-types`), `entityApi.list/get/create/update/archive` (workspace-scoped under `/workspaces/{id}/entities`). `update` uses the shared `api.patch` helper.
- **Audit service** (`src/api/audit.ts`): typed DTOs mirroring the BE `AuditLogListResponse` (data + total + page + perPage + filterContext) and `AuditLogEntryDto` (actor, contextual entity/workspace/organization/targetUser, propertyChanges); `auditApi.list(query)` builds a query string for `GET /audit/audit-log` with `entity_type` (scope), `workspace_id`/`organization_id`, `date_from/date_to`, `action`, 1-based `index`, `page_size`, etc. Audit Pinia store (`src/stores/audit.ts`) caches `rows`, `total`, `page`, `perPage` and exposes `fetchRows(query)`.
- **Auth store** (Pinia) persists `accessToken` + `expiresAt` in `localStorage`; stores `user` profile from `/me`; exposes `login`, `register`, `logout`, `fetchProfile`, `updateProfile`, `deleteAccount`; `isAuthenticated` respects token expiry.
- **Organization store** (Pinia) manages current org selection (persisted in `localStorage`), members, roles, invitations; exposes `createOrganization`, `inviteMember`, `changeMemberRole`, `removeMember`, `fetchOrganizations`, `hasOrganization`.
- **Workspace store** (Pinia) manages current workspace selection (persisted in `localStorage` under `relativa_ws_id`), workspaces list, members, roles, invitations; exposes `setCurrentWorkspace`, `fetchWorkspaces`, `createWorkspace`, `updateWorkspace`, `archiveWorkspace`, member/role/invitation actions, `clear`.
- **Layouts:** `AuthLayout.vue` (centered card, brand mark) and `MainLayout.vue` (top bar: org name, user name as link to `/account`, sign out; sidebar: Home, Account, Workspaces, Invitations, Graph, role-gated *Audit log* for `ws_admin`/`ws_analyst` or `org_owner`/`org_admin`). Home uses `exact-active-class` so it does not stay highlighted when navigating to nested routes. The standalone top-level *Members* link was removed — workspace member management is under *Workspaces → Manage members*; org-level `MembersView` remains on route `/members` without a sidebar entry.
- **Views:** `LoginView.vue`, `RegisterView.vue` (matched to Figma login prototype), `OnboardingView.vue` (create org, search & join, pending org invitations), `WorkspaceSelectorView.vue` (post-login workspace gate: lists workspaces as cards, auto-selects when exactly one exists, offers inline workspace creation when the user has none), `AccountSettingsView.vue` (`/account`: read-only email, edit first/last name via `PATCH /me` with inline validation errors, danger-zone delete account via `DELETE /me` then clear stores + redirect to login), `MembersView.vue` (org members, invitations, join-request approve/reject for org admins; pencil action + dialog to edit another member’s name when the caller’s org role includes `edit_other_org_users_profile`), `WorkspacesView.vue` (cards: *Manage members* and *Entities*), `WorkspaceMembersView.vue` (header *Entities* + *Invite member*; ws roles `ws_admin` / `ws_manager` / `ws_analyst` / `ws_member`), `InvitationsView.vue`, `EntitiesView.vue`, `EntityCreateForm.vue`, `HomeView.vue`, `GraphView.vue` (vis-network placeholder).
- **Routes:** `/account` → `AccountSettingsView.vue` (`account`); `/workspaces/:id/entities` → `EntitiesView.vue` (`workspace-entities`); `/workspaces/:id/entities/new` → `EntityCreateForm.vue` (`workspace-entity-create`); `/audit-log` → `AuditLogView.vue` (`audit-log`).
- **AuditLogView** (`src/views/AuditLogView.vue`, CR-186): PrimeVue `DataTable` in `lazy` mode driven by the audit store; columns Date / Type / Action / Author (email) / Target (clickable entity link) / Old/New value (per-cell collapsible JSON `<pre>` blocks). Filters bar with scope `Select` (entity / workspace / organization / users — options pruned by caller's role), `DatePicker` range (serialized to ISO via `toISOString()`), action `InputText`, *Apply* / *Reset*. Server-side pagination via the DataTable `@page` event (PrimeVue zero-based → BE one-based `index`). Page itself is gated with `v-if` on the same role check used by the sidebar; unauthorized callers see a locked placeholder instead of the table.
- **Router guards:** `meta.public`, `meta.guestOnly`, `meta.skipOrgCheck`, and `meta.skipWorkspaceCheck` flags. Unauthenticated users are redirected to `/login` with `?redirect=<original>` query; authenticated users cannot visit `/login` or `/register`; authenticated users without an organization are sent to `/onboarding`; authenticated users with an organization but no current workspace are sent to `/workspace-select` (which auto-selects when only one workspace is available, preventing a needless extra screen). On a hard refresh, when `auth.isAuthenticated` is true but `auth.user` is null, the guard now eagerly calls `auth.fetchProfile()` so views like `HomeView.vue` always have an email/name to render.
- `GraphView.vue` with vis-network placeholder (unchanged).
- Reads `VITE_GATEWAY_URL` from environment; all traffic goes through the gateway.

---

## Stubs / Partially Implemented

### Core service -- remaining gaps

**What is missing:**
- No business rules (BP-01 through BP-06).
- No generalized domain-event catalog beyond workspace choreography pilot + audit payloads.
- **No email notifications for invitations or join request outcomes (intentional).** Invitation tokens are returned in the POST response and surfaced as a copy-link in the UI. The token-in-response / copy-link UX is intentional for this iteration — integrating a real SMTP/transactional-email provider is out of scope. Resend rotates the token and bumps `ExpiresAt` but still returns the new token inline.
- No property management endpoints (list/create/update org-scoped custom properties).
- Relationship management endpoints (`entity_relationship`) deferred — the table and seed data exist but no API surface yet.

### Graph service

**What exists:** SignalR hub mapped at `/hubs/graph`, identity endpoint, `DomainEventConsumerHostedService` (workspace choreography), DLQ wiring, Postgres idempotency receipts via EF `GraphDbContext` (`rabbitmq_processed_delivery`), broadcast event `domain.workspace.lifecycle.v1`.
**What is missing:** `OnConnectedAsync` only calls `base`. No graph-domain queries, recursive CTE traversal, RBAC-scoped hub groups, or ML score integration.

### Audit service

**What exists:** RabbitMQ consumer (`audit.#`) with idempotency; persistence into all four audit tables. **`GET /audit-log`** and **`GET /entities/{entityId}/audit-log`**: `date_from` / `date_to`, `action`, `index`, `page_size`, `entity_id`, `domain_entity_type`, `actor_user_id`, `target_user_id`; response `{ data, total, page, perPage, filterContext }` with actor/workspace/org/entity/target user context and optional `propertyChanges` / `propertyDefinitionsForEntityType` for entity events. **RBAC:** `ws_admin`/`ws_analyst` (entity + workspace), `org_owner`/`org_admin` (organization), user-scope visibility rules. **GlobalExceptionHandler** + **FluentValidation**; **full JWT validation** (issuer, audience, key, lifetime). See [AUDIT-LOG-API.md](AUDIT-LOG-API.md).
**What is missing:** Scalar/OpenAPI browser for Audit (only `MapOpenApi` in dev); no automated tests.

### ML service

**What exists:** `POST /api/ml/recalculate/` endpoint; `run_domain_consumer` management command (blocking pika loop) started next to Django in Docker Compose; deduplicates via `rabbitmq_processed_delivery`.
**What is missing:** Returns `{"status":"accepted","detail":"stub"}`. No ML models (closure_score, churn_score). No Celery tasks defined. No Redis broker in Docker Compose. Beat schedule is commented out in `settings.py`.

### Client

**What exists:** Vue 3 + PrimeVue + Tailwind scaffold. Auth (login/register) + org onboarding + workspace selection + `/account` profile + member management (invite, role change, remove, join-request review for org admins, org-permission-based edit of another member’s profile) + workspace CRUD via Gateway. Typed API clients. Router guards with org and workspace checks.
**What is missing:** No custom role creation UI. Entity list is minimal — entity detail / edit / archive UIs still pending. No dashboard. "Forgot password?" is a placeholder (no backend). D3 integration noted for later.

---

## Known Issues

| Issue | Severity | Details |
|---|---|---|
| **Seed passwords are placeholders** | High | `InitSeedData` migration uses `$2y$10$hashed_pwd_placeholder` -- seeded demo users cannot log in. Must be replaced with real bcrypt hashes. |
| **Authentication README outdated** | Low | `Authentication/README.md` claims endpoints return 501 stubs. In reality, login, register, and `/me` are fully implemented. |
| **Migration README outdated** | Low | `Migration/README.md` describes an `entrypoint.sh` flow. Actual code uses `MigrateAsync` in `Program.cs`. |
| **Gateway README partially outdated** | Low | `Gateway/README.md` says JWT validation is a stub. Gateway now fully validates JWT. |
| **Unused package reference** | Trivial | `Asp.Versioning.Http` is referenced in `Authentication/src/Relativa.Authentication/Relativa.Authentication.csproj` but never used in code. |
| **Core CORS is `AllowAnyOrigin`** | Low | Gateway now has a proper named-origin CORS allowlist (reads `Cors:Origins` from config). Core retains `AllowAnyOrigin/Header/Method` as a dev convenience since Core is only reached via the gateway in deployed environments; tighten for production. |
| **Sparse automated tests** | Medium | Core + Authentication both include xUnit suites; `Messaging/tests/Relativa.Messaging.Tests` validates outbox routing helpers + a Testcontainers RabbitMQ smoke test. Graph, Audit, ML, and Integration/E2E suites are still absent. |
| **No CI/CD pipeline** | Medium | No `.github/workflows`, no `azure-pipelines.yml`, no CI configuration of any kind. |
| **Unused Auth dependencies** | Low | `IRoleRepository`, `RoleRepository`, and `AuthOptions` remain in the Auth codebase but are no longer registered in DI or used. Can be removed in a cleanup pass. |

---

## Roadmap (from READMEs and code comments)

### Core service -- business API

- ~~CRUD endpoints for entities (EAV-based).~~ *(done — entity-type listing + full CRUD under workspaces)*
- ~~EAV validation service (required properties, typed value parsing).~~ *(done — enforced in EntityService)*
- ~~Workspace isolation for entity queries.~~ *(done — EntityWorkspace join enforced on every read)*
- Property management endpoints: list/create/update org-scoped custom properties.
- Entity relationship management endpoints (`entity_relationship` table exists; API surface deferred).
- Business rules BP-01 through BP-06 (referenced in `Core/README.md`, not defined in code).
- Domain events published to Audit after entity mutations.
- Email notifications for invitation sends, join request approvals/rejections. *(Intentionally deferred — see Stubs section; token is returned in the response and surfaced as a copy-link in the UI.)*

### Authentication

- Token refresh flow (`POST /refresh`).
- Token blacklisting.
- `Jwt:RefreshTokenDays` is configured (7 days) but not used.

### Gateway

- Per-route permission checks (currently authorization is delegated to Core).

### Graph service

- Recursive CTE queries for entity-relationship traversal using `entity_relationship` and `entity_relationship_type`.
- Dynamic RBAC-based filtering of graph data (workspace-scoped via `entity_workspace`).
- ~~Live SignalR push updates when workspaces change via choreography envelope.~~ *(partial — choreography consumer broadcasts workspace lifecycle but no entity-graph projection yet)*.
- ML score integration (display `closure_score` property values on graph nodes — stored as `entity_property_value` rows).

### Audit service

- ~~Query/filter + pagination on audit log.~~ *(done)*
- ~~JWT validation aligned with Gateway.~~ *(done)*
- Deeper report export (CSV/PDF) and dedicated UI.

### ML service

- scikit-learn models for `closure_score` and `churn_score`.
- Celery task implementation for batch recalculation.
- Redis broker added to Docker Compose.
- Celery beat nightly schedule (02:00 UTC) enabled.
- Integration with Core (read deal data) and Graph (push updated scores).

### Client

- ~~Login and register forms wired to Gateway auth endpoints.~~ *(done in CR-96)*
- ~~Organization onboarding (create / search & join).~~ *(done in CR-96)*
- ~~Organization member management (list, invite, role change, remove).~~ *(done in CR-96)*
- ~~Workspace selection (post-login gate with auto-select-when-one).~~ *(done in CR-133)*
- ~~Entity create form (Sprint 1 ENT.5 — dropdown of types + dynamic property fields driven by `GET /entity-types`).~~ *(done in CR-138)*
- ~~Minimal entity list view as the landing place after a successful create.~~ *(done in CR-138 follow-up)*
- Workspace management UI (rename, archive from list).
- ~~Join request review UI (approve/reject pending requests).~~ *(done — org-scoped in `MembersView.vue` with `manage_join_requests` gating; workspace join requests removed.)*
- Role and permission management UI.
- Entity detail / edit / archive pages (Sprint 2).
- Dashboard with analytics.
- D3-based graph visualization (replacing vis-network placeholder).
- Password reset flow (requires new backend endpoint).

### Infrastructure

- CI/CD pipeline (GitHub Actions or similar).
- Test projects (at minimum: unit tests for Application services, integration tests for API endpoints).
- Production-ready CORS configuration.
- TLS/HTTPS for production deployment.
