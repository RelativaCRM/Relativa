# Project Status -- What is Done and What is Not

> **Last verified:** 2026-04-21

> **Maintenance obligation:** If you implement a feature that was listed as stub or TODO, move it to the "Implemented" section. If you introduce a new known issue or break something, add it to "Known Issues." Always update the "Last verified" date. See [AI-GUIDES-INDEX.md](../../AI-GUIDES-INDEX.md) for the full update matrix.

---

## Status Summary

| Service | Status | One-line summary |
|---|---|---|
| Gateway | **Functional** | YARP routing, JWT validation, split anonymous/auth routes, health, Scalar -- all working |
| Authentication | **Functional** | Login, register, `/me` profile endpoint, JWT (sub + email only), FluentValidation -- all working |
| Core | **Functional** (org + ws RBAC) | Organization management, workspace management, split RBAC (org roles + ws roles), members, invitations, join requests, role/permission management all implemented; entity/deal CRUD not yet |
| Graph | **Stub** | SignalR hub exists but has no logic |
| Audit | **Stub** | Returns empty array; JWT validation disabled |
| Migration | **Functional** | Applies EF migrations on startup; schema + seed data work |
| ML | **Stub** | Single endpoint returns hardcoded stub |
| Client | **Partial** | Vue 3 + PrimeVue + Tailwind. Auth flow (login/register) wired to Gateway; base layouts (AuthLayout, MainLayout) in place; typed API client |
| Persistence | **Functional** | Full entity model (20 entities), fluent configs, ModelBuilderExtensions |

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
- **Permission listing:** `GET /api/v1/permissions` -- lists all 16 permissions (both org-scoped and ws-scoped).
- Full clean-architecture layers: Domain (repository interfaces), Application (9 services, DTOs, validators), Infrastructure (repositories, WorkspaceContext).
- Authorization checked per-request via `UserRoleOrganization` or `UserRoleWorkspace` DB lookup using JWT `sub` claim.

### Gateway

- YARP reverse proxy with 5 route groups: `/auth/*`, `/core/*`, `/graph/*`, `/ml/*`, `/audit/*`.
- Path prefix stripping via `PathRemovePrefix` transforms.
- JWT Bearer authentication with full validation (issuer, audience, signing key, lifetime).
- **Split anonymous/auth routes:** `/login` and `/register` are anonymous; `/me` requires JWT.
- Anonymous exceptions for health endpoints.
- Forwarded headers (`X-Forwarded-For`, `X-Forwarded-Proto`).
- Serilog request logging.
- GlobalExceptionHandler.
- OpenAPI + Scalar docs.
- Health endpoint at `/health`.

### Migration

- `MigrationDbContext` mirrors full Persistence model (20 entities).
- `Program.cs` runs `Database.MigrateAsync()` as a generic host console app.
- Migrations in `Migration/src/Relativa.Migration/Migrations/` cover the full schema including the split RBAC tables, org management tables, and seed data for 7 default roles, 16 permissions, and demo data.
- Docker Compose runs this before auth and core start.

### Persistence library

- 20 entity classes with EF Fluent API configurations.
- Split RBAC model: separate `OrganizationRole`/`OrganizationRolePermission` and `WorkspaceRole`/`WorkspaceRolePermission` hierarchies sharing a common `Permission` table.
- `ModelBuilderExtensions.ApplyAuthEntityConfigurations()` for auth-only subset (User).
- `ModelBuilderExtensions.ApplyAllEntityConfigurations()` for full model (all 20 entities).
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
- **Typed API client** (`src/api/http.ts`): `gatewayFetch` with JWT + `X-Workspace-ID` headers, `ApiError` class, JSON helpers (`api.get/post/put/del`), auto session clear on `401`.
- **Auth service** (`src/api/auth.ts`): `authApi.register` → `/auth/api/v1/auth/register`, `authApi.login` → `/auth/api/v1/auth/login` (via Gateway, CR-96).
- **Workspaces service** (`src/api/workspaces.ts`): `workspacesApi.list` → `GET /core/api/v1/workspaces` returning `WorkspaceDto[]`.
- **Auth store** (Pinia) persists `accessToken`, `expiresAt`, and `workspaceId` in `localStorage`; exposes `login` (auto-clears stale `workspaceId`), `register`, `logout`, `clearSession`, `setWorkspace`; `isAuthenticated` respects token expiry.
- **Layouts:** `AuthLayout.vue` (centered card, brand mark, taglines, footer) and `MainLayout.vue` (top bar with logout, sidebar nav).
- **Views:** `LoginView.vue`, `RegisterView.vue` (matched to Figma login prototype, Register mirrors same style with `firstName`/`lastName`/`email`/`password`), `WorkspaceSelectView.vue` (card list fetched from `GET /workspaces`, auto-selects and redirects when exactly one workspace exists, empty-state hint when user belongs to none), `HomeView.vue` (session info card).
- **Router guards:** `meta.public`, `meta.guestOnly`, and `meta.skipWorkspaceCheck` flags; unauthenticated users are redirected to `/login` with `?redirect=<original>` query; authenticated users cannot visit `/login` or `/register`; authenticated users without a selected workspace are redirected to `/select-workspace` with `?redirect=<original>` query.
- `GraphView.vue` with vis-network placeholder (unchanged).
- Reads `VITE_GATEWAY_URL` from environment; all traffic goes through the gateway.

---

## Stubs / Partially Implemented

### Core service -- remaining gaps

**What is missing:**
- No CRUD for entities, deals, or clients.
- No workspace isolation for entity queries (multi-tenant data scoping).
- No business rules (BP-01 through BP-06).
- No domain events.
- No email notifications for invitations or join request outcomes.

### Graph service

**What exists:** SignalR hub mapped at `/hubs/graph`, service identity endpoint.
**What is missing:** `OnConnectedAsync` only calls `base`. No graph data queries, no recursive CTE queries, no RBAC filtering, no live update logic, no ML score integration.

### Audit service

**What exists:** `/audit-log` endpoint with `AuditReaders` authorization policy (requires any authenticated user -- stub).
**What is missing:** Endpoint returns empty array `[]`. JWT signature validation is deliberately disabled (all checks set to `false`). No actual audit event storage or ingestion. No domain event consumer.

### ML service

**What exists:** `POST /api/ml/recalculate/` endpoint.
**What is missing:** Returns `{"status":"accepted","detail":"stub"}`. No ML models (closure_score, churn_score). No Celery tasks defined. No Redis broker in Docker Compose. Beat schedule is commented out in `settings.py`.

### Client

**What exists:** Vue 3 + PrimeVue + Tailwind scaffold. Auth flow (login/register) wired to Gateway. Workspace selection after login (`/select-workspace`) with auto-select for single-workspace users. `AuthLayout` + `MainLayout` base layouts. Typed API client with JWT and `X-Workspace-ID` handling. Router guards for auth + workspace selection.
**What is missing:** No organization selection / management UI. No workspace creation or management UI (selection only). No member/invitation/role management UI. No deal/client management UI. No dashboard. "Forgot password?" link is a placeholder (endpoint not in backend). D3 integration noted as "for later."

---

## Known Issues

| Issue | Severity | Details |
|---|---|---|
| **Seed passwords are placeholders** | High | `InitSeedData` migration uses `$2y$10$hashed_pwd_placeholder` -- seeded demo users cannot log in. Must be replaced with real bcrypt hashes. |
| **Authentication README outdated** | Low | `Authentication/README.md` claims endpoints return 501 stubs. In reality, login, register, and `/me` are fully implemented. |
| **Migration README outdated** | Low | `Migration/README.md` describes an `entrypoint.sh` flow. Actual code uses `MigrateAsync` in `Program.cs`. |
| **Gateway README partially outdated** | Low | `Gateway/README.md` says JWT validation is a stub. Gateway now fully validates JWT. |
| **Unused package reference** | Trivial | `Asp.Versioning.Http` is referenced in `Authentication/src/Relativa.Authentication/Relativa.Authentication.csproj` but never used in code. |
| **CORS only on Core** | Medium | CORS (`AllowAnyOrigin/Header/Method`) is only registered on Core. Other services behind the gateway may need it if accessed directly during development. |
| **No test projects** | High | Zero test projects across the entire solution. No xUnit, NUnit, or MSTest references anywhere. |
| **No CI/CD pipeline** | Medium | No `.github/workflows`, no `azure-pipelines.yml`, no CI configuration of any kind. |
| **Audit JWT not validated** | Medium | Audit service has JWT Bearer registered but all validation parameters set to `false`. `SignatureValidator` parses tokens without cryptographic verification. Acceptable as a stub but must be fixed before real audit data flows through. |
| **Unused Auth dependencies** | Low | `IRoleRepository`, `RoleRepository`, and `AuthOptions` remain in the Auth codebase but are no longer registered in DI or used. Can be removed in a cleanup pass. |

---

## Roadmap (from READMEs and code comments)

### Core service -- business API

- CRUD endpoints for entities, clients, deals.
- Workspace isolation for entity queries (multi-tenant data scoping via `EntityWorkspace`).
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

- Recursive CTE queries for entity-relationship traversal.
- Dynamic RBAC-based filtering of graph data.
- Live SignalR push updates when entities change.
- ML score integration (display closure_score on graph nodes).

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
- ~~Workspace selection UI after login.~~ *(done in CR-96)*
- Organization selection / management UI.
- Workspace creation / management UI.
- Member and invitation management UI.
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
