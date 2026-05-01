# Project Status -- What is Done and What is Not

> **Last verified:** 2026-05-01 (RabbitMQ audit pipeline implemented with outbox + consumer)

> **Maintenance obligation:** If you implement a feature that was listed as stub or TODO, move it to the "Implemented" section. If you introduce a new known issue or break something, add it to "Known Issues." Always update the "Last verified" date. See [AI-GUIDES-INDEX.md](../../AI-GUIDES-INDEX.md) for the full update matrix.

---

## Status Summary

| Service | Status | One-line summary |
|---|---|---|
| Gateway | **Functional** | YARP routing, JWT validation, split anonymous/auth routes, health, Scalar -- all working |
| Authentication | **Functional** | Login, register, `/me` profile endpoint, JWT (sub + email only), FluentValidation -- all working |
| Core | **Functional** (org + ws RBAC + entity CRUD) | Organization management, workspace management, split RBAC, members, invitations, join requests, permissions, entity-type listing (public), and workspace-scoped entity CRUD all implemented |
| Graph | **Stub** | SignalR hub exists but has no logic |
| Audit | **Functional** | Consumes RabbitMQ events and persists audit logs with idempotency |
| Migration | **Functional** | Applies EF migrations on startup; schema + seed data work, including outbox/idempotency tables |
| ML | **Stub** | Single endpoint returns hardcoded stub |
| Client | **Partial** | Vue 3 + PrimeVue + Tailwind. Auth flow + org onboarding + members/invitations wired to Gateway; base layouts in place; typed API client for auth + org endpoints |
| Persistence | **Functional** | Full EAV entity model (21 entities), fluent configs, ModelBuilderExtensions |

---

## Implemented (working)

### Authentication service

- `POST /api/v1/auth/register` -- creates user, hashes password with bcrypt, returns user DTO with 201 + Location header. Newly registered users have no organization or workspace membership.
- `POST /api/v1/auth/login` -- validates credentials, issues JWT with claims: `sub`, `email`, `jti`. Role and permissions are **not** included in the JWT -- they are resolved per-request by Core using organization/workspace membership.
- `GET /api/v1/auth/me` -- returns authenticated user's profile (`id`, `email`, `firstName`, `lastName`) from JWT `sub` claim. Requires valid JWT.
- Full clean-architecture layers: Domain interfaces, Application service + DTOs + FluentValidation validators, Infrastructure (AuthDbContext, UserRepository, JwtTokenService, BcryptPasswordHasher).
- GlobalExceptionHandler maps ValidationException -> 400, UnauthorizedAccessException -> 401, duplicate email -> 409.
- EF Core health check at `/health`.
- OpenAPI + Scalar docs.
- Fire-and-forget audit publishing: register flow writes to `audit_outbox`, dispatcher publishes to RabbitMQ.

### Core service -- Organization management

- **Organization CRUD:** `POST /api/v1/organizations` (create, creator becomes `org_owner`), `GET` (list user's orgs), `GET /search?q=...` (search by name), `GET /{id}` (details, requires org membership), `PUT /{id}` (update, requires `manage_org_settings`).
- **Organization members:** `GET .../members` (list, requires org membership), `DELETE .../members/{userId}` (remove, requires `remove_org_members`), `PUT .../members/{userId}/role` (change role, requires `assign_org_roles`).
- **Join requests:** `POST .../join-requests` (request to join), `GET .../join-requests` (list pending, requires `manage_join_requests`), `PUT .../join-requests/{reqId}` (approve/reject, requires `manage_join_requests`), `GET /api/v1/join-requests/mine` (own requests).
- **Organization invitations:** `POST .../invitations` (invite by email, requires `invite_to_org`), `GET .../invitations` (list pending), `DELETE .../invitations/{invId}` (cancel), `POST /api/v1/invitations/accept-org` (accept, requires matching email).
- **Organization roles:** `GET .../roles` (list system + custom, requires org membership), `POST .../roles` (create custom, requires `manage_org_roles`), `PUT .../roles/{roleId}` (update), `DELETE .../roles/{roleId}` (delete). System roles cannot be modified.

### Core service -- Workspace RBAC

- **Workspace CRUD:** `POST /api/v1/workspaces` (create within an org, requires `create_workspaces` org perm + `organizationId`; creator becomes `ws_admin`), `GET` (list user's workspaces), `GET /{id}`, `PUT /{id}` (requires `manage_ws_settings`), `DELETE /{id}` (archive, requires `ws_admin` role).
- **Member management:** `GET .../members`, `POST .../members` (add org member directly, requires `add_ws_members`), `PUT .../members/{userId}/role` (requires `assign_ws_roles`), `DELETE .../members/{userId}` (requires `remove_ws_members` or self).
- **Invitation system:** `POST .../invitations` (invite by email, requires `invite_to_workspace`, returns token in response), `GET .../invitations` (list pending), `DELETE .../invitations/{id}` (cancel), `POST /api/v1/invitations/accept` (accept by token).
- **Role management:** `GET .../roles` (list system + custom), `POST .../roles` (create custom, requires `manage_ws_roles`), `PUT .../roles/{id}` (update), `DELETE .../roles/{id}` (archive). System roles cannot be modified.
- **Combined invitations:** `GET /api/v1/invitations/mine` -- lists all pending invitations (both workspace + org) for the authenticated user.
- **Permission listing:** `GET /api/v1/permissions` -- lists all 16 permissions (both org-scoped and ws-scoped). **Workspace permissions 14 and 15 are `manage_entities` / `view_entities`** (previously `edit_deals` / `view_deals`; re-seeded via `ReseedPermissions` migration).
- Full clean-architecture layers: Domain (repository interfaces), Application (11 services, DTOs, validators), Infrastructure (repositories, WorkspaceContext).
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
- Fire-and-forget audit publishing for core write flows (organization/workspace/entity) via `audit_outbox` + RabbitMQ dispatcher.

### Gateway

- YARP reverse proxy with 5 route groups: `/auth/*`, `/core/*`, `/graph/*`, `/ml/*`, `/audit/*`.
- Path prefix stripping via `PathRemovePrefix` transforms.
- JWT Bearer authentication with full validation (issuer, audience, signing key, lifetime). **The Gateway is the only component that validates JWTs for downstream services** (Authentication still validates its own tokens because `/me` needs the claims).
- **Global authorization via `MapReverseProxy().RequireAuthorization()`.** Every proxied route requires a valid JWT unless it is explicitly marked `AuthorizationPolicy: Anonymous` in `appsettings.json`.
- **Identity forwarding via YARP request transform:** on every proxied request the Gateway unconditionally strips any incoming `X-User-Id` / `X-User-Email` headers, then re-adds them from the validated `ClaimsPrincipal` (`sub` and `email` claims). Downstream services (Core, future Graph/ML/Audit) trust these headers instead of re-validating tokens. Client-supplied values are always overwritten, so identity cannot be spoofed through the Gateway. The trust boundary assumes downstream services are not reachable from outside the Gateway's network (enforced by docker-compose today; network policy / service mesh in production).
- **Split anonymous/auth routes:** `/login` and `/register` are anonymous; `/me` requires JWT.
- Anonymous exceptions for health endpoints.
- **CORS:** named-origin allowlist with credentials, reading `Cors:Origins` from config (defaults to `http://localhost:5173` and `http://localhost:3000`).
- Forwarded headers (`X-Forwarded-For`, `X-Forwarded-Proto`).
- Serilog request logging.
- GlobalExceptionHandler.
- OpenAPI + Scalar docs.
- Health endpoint at `/health`.

### Migration

- `MigrationDbContext` mirrors full Persistence model (21 entities).
- `Program.cs` runs `Database.MigrateAsync()` as a generic host console app.
- Four migrations in `Migration/src/Relativa.Migration/Migrations/`:
  - `20260416224419_InitialCreate.cs` — full initial schema (RBAC, org management, old polymorphic entity tables).
  - `20260416224514_SeedData.cs` — seeds all reference data. Permission ids 14/15 are `manage_entities`/`view_entities`.
  - `20260423000000_EavSchemaReplace.cs` — EAV schema migration: drops old property tables, renames entity tables to singular, creates new EAV tables.
  - `20260423100000_ReseedPermissions.cs` — FK-safe wipe and full re-insert of `permissions`, `organization_role_permissions`, `workspace_role_permissions`; replaces `edit_deals`/`view_deals` with `manage_entities`/`view_entities` for existing databases.
- Docker Compose runs this before auth and core start.

### Persistence library

- 21 entity classes with EF Fluent API configurations.
- Split RBAC model: separate `OrganizationRole`/`OrganizationRolePermission` and `WorkspaceRole`/`WorkspaceRolePermission` hierarchies sharing a common `Permission` table.
- **EAV entity model:** `Property`, `EntityTypeProperty`, `EntityPropertyValue`, `EntityRelationshipType`, `EntityRelationship` replace the old hard-coded `EntityProperty` / `PersonalDataPropertyValue` / `LocationPropertyValue` / `DealPropertyValue` tables.
- `ModelBuilderExtensions.ApplyAuthEntityConfigurations()` for auth-only subset (User).
- `ModelBuilderExtensions.ApplyAllEntityConfigurations()` for full model (all 21 entities).
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
- **Typed API client** (`src/api/http.ts`): `gatewayFetch` with JWT + `X-Workspace-ID` headers (workspace id read from the workspace store), `ApiError` class, JSON helpers (`api.get/post/put/del`), auto session clear on `401`.
- **Auth service** (`src/api/auth.ts`): `authApi.register`, `authApi.login`, `authApi.me` (via Gateway, CR-96).
- **Organization service** (`src/api/organizations.ts`): org CRUD, members, invitations, join requests, roles, combined invitations (`/invitations/mine`).
- **Workspace service** (`src/api/workspaces.ts`): workspace CRUD, members, roles, invitations.
- **Auth store** (Pinia) persists `accessToken` + `expiresAt` in `localStorage`; stores `user` profile from `/me`; exposes `login`, `register`, `logout`, `fetchProfile`; `isAuthenticated` respects token expiry.
- **Organization store** (Pinia) manages current org selection (persisted in `localStorage`), members, roles, invitations; exposes `createOrganization`, `inviteMember`, `changeMemberRole`, `removeMember`, `fetchOrganizations`, `hasOrganization`.
- **Workspace store** (Pinia) manages current workspace selection (persisted in `localStorage` under `relativa_ws_id`), workspaces list, members, roles, invitations; exposes `setCurrentWorkspace`, `fetchWorkspaces`, `createWorkspace`, `updateWorkspace`, `archiveWorkspace`, member/role/invitation actions, `clear`.
- **Layouts:** `AuthLayout.vue` (centered card, brand mark) and `MainLayout.vue` (top bar with user name + org name + logout, sidebar nav with Home/Members/Workspaces/Invitations/Graph).
- **Views:** `LoginView.vue`, `RegisterView.vue` (matched to Figma login prototype), `OnboardingView.vue` (create org, search & join, pending org invitations), `WorkspaceSelectorView.vue` (post-login workspace gate: lists workspaces as cards, auto-selects when exactly one exists, offers inline workspace creation when the user has none), `MembersView.vue`, `WorkspacesView.vue`, `WorkspaceMembersView.vue`, `InvitationsView.vue`, `HomeView.vue` (session + org info cards), `GraphView.vue` (vis-network placeholder, unchanged).
- **Router guards:** `meta.public`, `meta.guestOnly`, `meta.skipOrgCheck`, and `meta.skipWorkspaceCheck` flags. Unauthenticated users are redirected to `/login` with `?redirect=<original>` query; authenticated users cannot visit `/login` or `/register`; authenticated users without an organization are sent to `/onboarding`; authenticated users with an organization but no current workspace are sent to `/workspace-select` (which auto-selects when only one workspace is available, preventing a needless extra screen).
- `GraphView.vue` with vis-network placeholder (unchanged).
- Reads `VITE_GATEWAY_URL` from environment; all traffic goes through the gateway.

---

## Stubs / Partially Implemented

### Core service -- remaining gaps

**What is missing:**
- No business rules (BP-01 through BP-06).
- No domain events.
- No email notifications for invitations or join request outcomes.
- No property management endpoints (list/create/update org-scoped custom properties).
- Relationship management endpoints (`entity_relationship`) deferred — the table and seed data exist but no API surface yet.

### Graph service

**What exists:** SignalR hub mapped at `/hubs/graph`, service identity endpoint.
**What is missing:** `OnConnectedAsync` only calls `base`. No graph data queries, no recursive CTE queries, no RBAC filtering, no live update logic, no ML score integration.

### Audit service

**What exists:** RabbitMQ consumer (`audit.#`) with idempotency tracking (`audit_processed_event`), persistence into all four audit tables, and filtered `/audit-log` endpoint.
**What is missing:** JWT validation hardening (signature/issuer/audience/lifetime checks are still disabled for now).

### ML service

**What exists:** `POST /api/ml/recalculate/` endpoint.
**What is missing:** Returns `{"status":"accepted","detail":"stub"}`. No ML models (closure_score, churn_score). No Celery tasks defined. No Redis broker in Docker Compose. Beat schedule is commented out in `settings.py`.

### Client

**What exists:** Vue 3 + PrimeVue + Tailwind scaffold. Auth flow (login/register) + org onboarding (create/join) + post-login workspace selection (`/workspace-select`, auto-select for single-workspace users, inline create when user has none) + member management (invite, role change, remove) + workspace CRUD wired to Gateway. Typed API client for auth + org + workspace endpoints. Router guards with org and workspace checks.
**What is missing:** No join request review UI (admin can approve/reject). No custom role creation UI. No deal/client management UI. No dashboard. "Forgot password?" link is a placeholder (endpoint not in backend). D3 integration noted as "for later."

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
| **No test projects** | High | Zero test projects across the entire solution. No xUnit, NUnit, or MSTest references anywhere. |
| **No CI/CD pipeline** | Medium | No `.github/workflows`, no `azure-pipelines.yml`, no CI configuration of any kind. |
| **Audit JWT not validated** | Medium | Audit service has JWT Bearer registered but all validation parameters set to `false`. `SignatureValidator` parses tokens without cryptographic verification. Acceptable as a stub but must be fixed before real audit data flows through. |
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
- Email notifications for invitation sends, join request approvals/rejections.

### Authentication

- Token refresh flow (`POST /refresh`).
- Token blacklisting.
- `Jwt:RefreshTokenDays` is configured (7 days) but not used.

### Gateway

- Per-route permission checks (currently authorization is delegated to Core).

### Graph service

- Recursive CTE queries for entity-relationship traversal using `entity_relationship` and `entity_relationship_type`.
- Dynamic RBAC-based filtering of graph data (workspace-scoped via `entity_workspace`).
- Live SignalR push updates when entities change.
- ML score integration (display `closure_score` property values on graph nodes — stored as `entity_property_value` rows).

### Audit service

- Real audit event ingestion (consumer for domain events from Core).
- Persistent audit log storage (likely in PostgreSQL).
- Proper JWT validation (re-enable all checks).
- Query/filter capabilities on audit log.

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
- Workspace management UI (rename, archive from list).
- Join request review UI (approve/reject pending requests).
- Role and permission management UI.
- Deal/client management pages.
- Dashboard with analytics.
- D3-based graph visualization (replacing vis-network placeholder).
- Password reset flow (requires new backend endpoint).

### Infrastructure

- CI/CD pipeline (GitHub Actions or similar).
- Test projects (at minimum: unit tests for Application services, integration tests for API endpoints).
- Production-ready CORS configuration.
- TLS/HTTPS for production deployment.
