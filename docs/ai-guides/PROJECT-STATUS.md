# Project Status -- What is Done and What is Not

> **Last verified:** 2026-04-15

> **Maintenance obligation:** If you implement a feature that was listed as stub or TODO, move it to the "Implemented" section. If you introduce a new known issue or break something, add it to "Known Issues." Always update the "Last verified" date. See [AI-GUIDES-INDEX.md](../../AI-GUIDES-INDEX.md) for the full update matrix.

---

## Status Summary

| Service | Status | One-line summary |
|---|---|---|
| Gateway | **Functional** | YARP routing, JWT validation, health, Scalar -- all working |
| Authentication | **Functional** | Login, register, JWT (sub + email only), FluentValidation -- all working |
| Core | **Partial** | Workspace CRUD, member management, invitations, role/permission management implemented; entity/deal CRUD not yet |
| Graph | **Stub** | SignalR hub exists but has no logic |
| Audit | **Stub** | Returns empty array; JWT validation disabled |
| Migration | **Functional** | Applies EF migrations on startup; schema + seed data work |
| ML | **Stub** | Single endpoint returns hardcoded stub |
| Client | **Partial** | Vue 3 + PrimeVue + Tailwind. Auth flow (login/register) wired to Gateway; base layouts (AuthLayout, MainLayout) in place; typed API client |
| Persistence | **Functional** | Full entity model (16 entities), fluent configs, ModelBuilderExtensions |

---

## Implemented (working)

### Authentication service

- `POST /api/v1/auth/register` -- creates user, hashes password with bcrypt, `RoleId` set to `null` (user has no role until they join a workspace), returns user DTO with 201 + Location header.
- `POST /api/v1/auth/login` -- validates credentials, issues JWT with claims: `sub`, `email`, `jti`. Role and permissions are **not** included in the JWT -- they are resolved per-request by Core using workspace membership.
- Full clean-architecture layers: Domain interfaces, Application service + DTOs + FluentValidation validators, Infrastructure (AuthDbContext, UserRepository, JwtTokenService, BcryptPasswordHasher).
- GlobalExceptionHandler maps ValidationException -> 400, UnauthorizedAccessException -> 401, duplicate email -> 409.
- EF Core health check at `/health`.
- OpenAPI + Scalar docs.

### Core service -- Workspace RBAC

- **Workspace CRUD:** `POST /api/v1/workspaces` (create, auto-adds creator as admin member), `GET` (list user's workspaces), `GET /{id}`, `PUT /{id}`, `DELETE /{id}` (archive).
- **Member management:** `GET .../members`, `PUT .../members/{userId}/role`, `DELETE .../members/{userId}`.
- **Invitation system:** `POST .../invitations` (invite by email), `GET .../invitations` (list pending), `DELETE .../invitations/{id}` (cancel), `POST /api/v1/invitations/accept` (accept by token).
- **Role management:** `GET .../roles` (list system + custom), `POST .../roles` (create custom), `PUT .../roles/{id}` (update), `DELETE .../roles/{id}` (archive). System roles cannot be modified.
- **Permission listing:** `GET /api/v1/permissions`.
- Full clean-architecture layers: Domain (repository interfaces), Application (4 services, DTOs, validators), Infrastructure (5 repositories, WorkspaceContext).
- Authorization checked per-request via `WorkspaceMember` DB lookup using JWT `sub` claim + `X-Workspace-ID` header.

### Gateway

- YARP reverse proxy with 5 route groups: `/auth/*`, `/core/*`, `/graph/*`, `/ml/*`, `/audit/*`.
- Path prefix stripping via `PathRemovePrefix` transforms.
- JWT Bearer authentication with full validation (issuer, audience, signing key, lifetime).
- Anonymous exceptions for login, register, and health endpoints.
- Forwarded headers (`X-Forwarded-For`, `X-Forwarded-Proto`).
- Serilog request logging.
- GlobalExceptionHandler.
- OpenAPI + Scalar docs.
- Health endpoint at `/health`.

### Migration

- `MigrationDbContext` mirrors full Persistence model.
- `Program.cs` runs `Database.MigrateAsync()` as a generic host console app.
- `20260412140027_InitialCreate.cs` -- full schema creation.
- `20260412140114_InitSeedData.cs` -- seeds roles, permissions, organizations, workspaces, demo entities.
- `20260413113908_AddWorkspaceMembership.cs` -- adds `workspace_members`, `workspace_invitations` tables; makes `users.role_id` nullable; adds `workspace_id` to `roles`; adds `created_by_user_id` to `workspaces`; backfills seed data.
- Docker Compose runs this before auth and core start.

### Persistence library

- 16 entity classes with EF Fluent API configurations (includes `WorkspaceMember` and `WorkspaceInvitation`).
- `ModelBuilderExtensions.ApplyAuthEntityConfigurations()` for auth-only subset (User, Role, Permission, RolePermission).
- `ModelBuilderExtensions.ApplyAllEntityConfigurations()` for full model (all 16 entities).
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
- **Auth store** (Pinia) persists `accessToken` + `expiresAt` in `localStorage`; exposes `login`, `register`, `logout`, `clearSession`; `isAuthenticated` respects token expiry.
- **Layouts:** `AuthLayout.vue` (centered card, brand mark, taglines, footer) and `MainLayout.vue` (top bar with logout, sidebar nav).
- **Views:** `LoginView.vue`, `RegisterView.vue` (matched to Figma login prototype, Register mirrors same style with `firstName`/`lastName`/`email`/`password`), `HomeView.vue` (session info card).
- **Router guards:** `meta.public` and `meta.guestOnly` flags; unauthenticated users are redirected to `/login` with `?redirect=<original>` query; authenticated users cannot visit `/login` or `/register`.
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

**What exists:** Vue 3 + PrimeVue + Tailwind scaffold. Auth flow (login/register) wired to Gateway. `AuthLayout` + `MainLayout` base layouts. Typed API client with JWT handling. Router guards.
**What is missing:** No workspace selection / management UI. No member/invitation/role management UI. No deal/client management UI. No dashboard. "Forgot password?" link is a placeholder (endpoint not in backend). D3 integration noted as "for later."

---

## Known Issues

| Issue | Severity | Details |
|---|---|---|
| **Seed passwords are placeholders** | High | `InitSeedData` migration uses `$2y$10$hashed_pwd_placeholder` -- seeded demo users cannot log in. Must be replaced with real bcrypt hashes. |
| **Authentication README outdated** | Low | `Authentication/README.md` claims endpoints return 501 stubs. In reality, login and register are fully implemented. |
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
- Workspace selection / management UI.
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
