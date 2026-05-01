# Microservices -- Service Catalog

> **Last verified:** 2026-05-01 (gateway-only CORS policy standardized; Core wildcard CORS removed)

> **Maintenance obligation:** If you add, remove, or change any endpoint or service, update this file and its "Last verified" date before finishing your task. If you add or remove an entire service, also update [DOCKER-SETUP.md](DOCKER-SETUP.md) and [PROJECT-OVERVIEW.md](PROJECT-OVERVIEW.md). See [AI-GUIDES-INDEX.md](../../AI-GUIDES-INDEX.md) for the full update matrix.

---

## Service Map (quick reference)

| Service | Port | Stack | Status |
|---|---|---|---|
| Gateway | 8080 | .NET 10, YARP | Functional |
| Authentication | 8081 | .NET 10, JWT, BCrypt, FluentValidation | Functional |
| Core | 8082 | .NET 10, EF Core | Functional (org + workspace RBAC) |
| Graph | 8083 | .NET 10, SignalR | Stub |
| Audit | 8086 | .NET 10 | Stub |
| Migration | -- | .NET 10, EF Core (console) | Functional |
| ML | 8084 | Django 5.1, DRF | Stub |
| Client | 3000 | Vue 3, Vite | Scaffold |

---

## 1. Gateway (`relativa-gateway`)

**Purpose:** YARP reverse proxy that routes all client traffic to backend services, enforces JWT authentication, and injects trusted identity headers (`X-User-Id`, `X-User-Email`) for downstream services.

**Solution:** `Gateway/Relativa.Gateway.sln`
**Project:** `Gateway/src/Relativa.Gateway/`
**Port:** 8080

### Endpoints

| Method | Path | Auth | Behavior |
|---|---|---|---|
| GET | `/health` | None | Returns `{"status":"Healthy","service":"relativa-gateway"}` |
| GET | `/scalar/v1` | None | Scalar interactive API docs |
| GET | `/openapi/v1.json` | None | Raw OpenAPI spec |
| * | `/auth/{**rest}` | Bearer JWT (anonymous exceptions below) | Proxied to Authentication (8081), prefix `/auth` stripped |
| * | `/core/{**rest}` | Bearer JWT (anonymous exceptions below) | Proxied to Core (8082), prefix `/core` stripped |
| * | `/graph/{**rest}` | Bearer JWT | Proxied to Graph (8083), prefix `/graph` stripped |
| * | `/ml/{**rest}` | Bearer JWT | Proxied to ML (8084), prefix `/ml` stripped |
| * | `/audit/{**rest}` | Bearer JWT | Proxied to Audit (8086), prefix `/audit` stripped |

**Anonymous gateway routes** (no JWT required):
- `POST /auth/api/v1/auth/login`
- `POST /auth/api/v1/auth/register`
- `GET /auth/health`
- `GET /core/health`
- `GET /core/api/v1/entity-types`

**JWT-required auth routes** (not anonymous):
- `GET /auth/api/v1/auth/me`

### Status: Functional

YARP routing with global `.RequireAuthorization()`, JWT Bearer validation (issuer, audience, signing key, lifetime), and gateway-owned CORS all working. Gateway CORS is configured via `Cors:Origins` (allowlist + credentials by default) with optional local dev override `Cors:AllowAnyOriginForDev=true` (wildcard origin without credentials). Auth routes are split: `/login` and `/register` are anonymous, `/me` requires JWT.

**Identity forwarding:** a YARP request transform runs on every proxied request. It unconditionally removes any incoming `X-User-Id` / `X-User-Email` (so clients cannot spoof identity), then, if the request is authenticated, re-adds them from the validated `ClaimsPrincipal` (`sub` → `X-User-Id`, `email` → `X-User-Email`). Downstream services (Core today; Graph/ML/Audit in the future) trust these headers and do **not** re-validate JWTs. This keeps JWT handling centralized in the Gateway (single-responsibility) and avoids duplicating JWT config across every service.

### Key Files

- `Gateway/src/Relativa.Gateway/Program.cs` -- service wiring, JWT config, YARP setup
- `Gateway/src/Relativa.Gateway/appsettings.json` -- YARP routes and clusters, JWT settings
- `Gateway/src/Relativa.Gateway/Middleware/GlobalExceptionHandler.cs`

---

## 2. Authentication (`relativa-auth`)

**Purpose:** Issues JWT tokens after login/register; provides user profile via `/me`; manages users via EF Core against PostgreSQL.

**Solution:** `Authentication/Relativa.Authentication.sln`
**Project:** `Authentication/src/Relativa.Authentication/`
**Port:** 8081

### Endpoints

| Method | Path | Auth | Behavior |
|---|---|---|---|
| POST | `/api/v1/auth/register` | None | Creates user with bcrypt-hashed password, returns user DTO + Location header |
| POST | `/api/v1/auth/login` | None | Validates credentials, returns `{ accessToken, expiresAt }` |
| GET | `/api/v1/auth/me` | JWT | Returns authenticated user's profile `{ id, email, firstName, lastName }` from JWT `sub` claim |
| GET | `/health` | None | EF Core DB health check |
| GET | `/scalar/v1` | None | Scalar interactive API docs |
| GET | `/openapi/v1.json` | None | Raw OpenAPI spec |

### Status: Functional

Login, register, and `/me` work end-to-end. JWT includes `sub`, `email`, and `jti` claims (role and permissions are **not** embedded -- they are resolved per-request by Core using organization/workspace membership). FluentValidation on login and register endpoints. GlobalExceptionHandler maps `ValidationException` to 400, `UnauthorizedAccessException` to 401, duplicate email to 409.

**Not yet implemented:** token refresh, token blacklisting.

### Key Files

- `Authentication/src/Relativa.Authentication/Program.cs` -- DI, middleware, endpoint mapping
- `Authentication/src/Relativa.Authentication/Endpoints/AuthEndpoints.cs` -- route definitions
- `Authentication/src/Relativa.Authentication.Application/Services/AuthService.cs` -- business logic
- `Authentication/src/Relativa.Authentication.Application/DTOs/` -- request/response DTOs (includes `UserProfileDto`)
- `Authentication/src/Relativa.Authentication.Application/Interfaces/IAuthService.cs`
- `Authentication/src/Relativa.Authentication.Application/Validators/` -- FluentValidation rules
- `Authentication/src/Relativa.Authentication.Domain/Interfaces/` -- `IUserRepository`, `ITokenService`, `IPasswordHasher`
- `Authentication/src/Relativa.Authentication.Infrastructure/Data/AuthDbContext.cs`
- `Authentication/src/Relativa.Authentication.Infrastructure/Repositories/UserRepository.cs`
- `Authentication/src/Relativa.Authentication.Infrastructure/Services/JwtTokenService.cs`
- `Authentication/src/Relativa.Authentication.Infrastructure/Services/BcryptPasswordHasher.cs`

---

## 3. Core (`relativa-core`)

**Purpose:** The main business API for organization management, workspace management, member/invitation management, role/permission management (split RBAC for both orgs and workspaces), and entity CRUD (EAV-based; workspace-scoped).

**Solution:** `Core/Relativa.Core.sln`
**Project:** `Core/src/Relativa.Core/`
**Port:** 8082

### Endpoints -- Organizations

| Method | Path | Auth | Behavior |
|---|---|---|---|
| POST | `/api/v1/organizations` | JWT | Create organization; creator becomes org_owner |
| GET | `/api/v1/organizations` | JWT | List organizations the user belongs to |
| GET | `/api/v1/organizations/search?q=...` | JWT | Search organizations by name; returns `{ id, name, memberCount }` for each match |
| GET | `/api/v1/organizations/{id}` | JWT + org membership | Get organization details |
| PUT | `/api/v1/organizations/{id}` | JWT + `manage_org_settings` | Update organization |

### Endpoints -- Organization Members

| Method | Path | Auth | Behavior |
|---|---|---|---|
| GET | `/api/v1/organizations/{id}/members` | JWT + org membership | List organization members |
| DELETE | `/api/v1/organizations/{id}/members/{userId}` | JWT + `remove_org_members` | Remove member from organization |
| PUT | `/api/v1/organizations/{id}/members/{userId}/role` | JWT + `assign_org_roles` | Change member's organization role |

### Endpoints -- Organization Join Requests

| Method | Path | Auth | Behavior |
|---|---|---|---|
| POST | `/api/v1/organizations/{id}/join-requests` | JWT | Request to join an organization |
| GET | `/api/v1/organizations/{id}/join-requests` | JWT + `manage_join_requests` | List pending join requests |
| PUT | `/api/v1/organizations/{id}/join-requests/{reqId}` | JWT + `manage_join_requests` | Approve or reject a join request |
| GET | `/api/v1/join-requests/mine` | JWT | List own pending join requests |

### Endpoints -- Organization Invitations

| Method | Path | Auth | Behavior |
|---|---|---|---|
| POST | `/api/v1/organizations/{id}/invitations` | JWT + `invite_to_org` | Invite user to organization by email |
| GET | `/api/v1/organizations/{id}/invitations` | JWT + `invite_to_org` | List pending org invitations |
| DELETE | `/api/v1/organizations/{id}/invitations/{invId}` | JWT + `invite_to_org` | Cancel org invitation |
| POST | `/api/v1/invitations/accept-org` | JWT + matching email | Accept organization invitation |

### Endpoints -- Organization Roles

| Method | Path | Auth | Behavior |
|---|---|---|---|
| GET | `/api/v1/organizations/{id}/roles` | JWT + org membership | List org roles (system + custom) |
| POST | `/api/v1/organizations/{id}/roles` | JWT + `manage_org_roles` | Create custom org role |
| PUT | `/api/v1/organizations/{id}/roles/{roleId}` | JWT + `manage_org_roles` | Update custom org role |
| DELETE | `/api/v1/organizations/{id}/roles/{roleId}` | JWT + `manage_org_roles` | Delete custom org role |

### Endpoints -- Combined Invitations

| Method | Path | Auth | Behavior |
|---|---|---|---|
| GET | `/api/v1/invitations/mine` | JWT | List all pending invitations (both workspace + org) for the user |

### Endpoints -- Workspaces

| Method | Path | Auth | Behavior |
|---|---|---|---|
| POST | `/api/v1/workspaces` | JWT + `create_workspaces` (org perm) | Create workspace within an organization (requires `organizationId`) |
| GET | `/api/v1/workspaces` | JWT | List workspaces for authenticated user |
| GET | `/api/v1/workspaces/{id}` | JWT + ws membership | Get workspace details |
| PUT | `/api/v1/workspaces/{id}` | JWT + `manage_ws_settings` | Update workspace name |
| DELETE | `/api/v1/workspaces/{id}` | JWT + ws_admin role | Archive workspace |

### Endpoints -- Workspace Members

| Method | Path | Auth | Behavior |
|---|---|---|---|
| GET | `/api/v1/workspaces/{id}/members` | JWT + ws membership | List workspace members |
| POST | `/api/v1/workspaces/{id}/members` | JWT + `add_ws_members` | Add an org member directly to the workspace |
| PUT | `/api/v1/workspaces/{id}/members/{userId}/role` | JWT + `assign_ws_roles` | Change a member's workspace role |
| DELETE | `/api/v1/workspaces/{id}/members/{userId}` | JWT + `remove_ws_members` (or self) | Remove a member |

### Endpoints -- Workspace Invitations

| Method | Path | Auth | Behavior |
|---|---|---|---|
| POST | `/api/v1/workspaces/{id}/invitations` | JWT + `invite_to_workspace` | Invite user by email (returns token in response) |
| GET | `/api/v1/workspaces/{id}/invitations` | JWT + `invite_to_workspace` | List pending invitations |
| DELETE | `/api/v1/workspaces/{id}/invitations/{invId}` | JWT + `invite_to_workspace` | Cancel invitation |
| POST | `/api/v1/invitations/accept` | JWT + matching email | Accept workspace invitation by token |

### Endpoints -- Workspace Roles

| Method | Path | Auth | Behavior |
|---|---|---|---|
| GET | `/api/v1/workspaces/{id}/roles` | JWT + ws membership | List roles (system + custom) |
| POST | `/api/v1/workspaces/{id}/roles` | JWT + `manage_ws_roles` | Create custom role |
| PUT | `/api/v1/workspaces/{id}/roles/{roleId}` | JWT + `manage_ws_roles` | Update custom role |
| DELETE | `/api/v1/workspaces/{id}/roles/{roleId}` | JWT + `manage_ws_roles` | Archive custom role |

### Endpoints -- Permissions

| Method | Path | Auth | Behavior |
|---|---|---|---|
| GET | `/api/v1/permissions` | JWT | List all available permissions (org + ws) |

### Endpoints -- Entity Types

| Method | Path | Auth | Behavior |
|---|---|---|---|
| GET | `/api/v1/entity-types` | **None** (anonymous via Gateway) | List all entity types with their property definitions. Returns `[{ id, name, properties: [{ propertyId, name, dataType, isRequired }] }]`. |

### Endpoints -- Entities

| Method | Path | Auth | Behavior |
|---|---|---|---|
| GET | `/api/v1/workspaces/{workspaceId}/entities` | JWT + `view_entities` | List non-archived entities in the workspace with all property values. |
| GET | `/api/v1/workspaces/{workspaceId}/entities/{entityId}` | JWT + `view_entities` | Get entity detail; **404** if entity does not belong to this workspace. |
| POST | `/api/v1/workspaces/{workspaceId}/entities` | JWT + `manage_entities` | Create entity + property values + workspace link in one atomic transaction. FluentValidation + required-property enforcement. |
| PUT | `/api/v1/workspaces/{workspaceId}/entities/{entityId}` | JWT + `manage_entities` | Replace all property values for an entity (not allowed if archived). |
| DELETE | `/api/v1/workspaces/{workspaceId}/entities/{entityId}` | JWT + `manage_entities` | Soft-delete: sets `is_archived = true`. |

### Endpoints -- Infrastructure

| Method | Path | Auth | Behavior |
|---|---|---|---|
| GET | `/health` | None | EF Core DB health check |
| GET | `/scalar/v1` | None | Scalar interactive API docs (dev only) |
| GET | `/openapi/v1.json` | None | Raw OpenAPI spec |

### Status: Functional (organization + workspace RBAC + entity CRUD implemented)

Full clean-architecture layers are implemented: Domain (repository interfaces), Application (services, DTOs, validators), Infrastructure (EF repositories, contexts). Organization management (CRUD, members, join requests, invitations, roles), workspace management (CRUD, members, invitations, roles), the split RBAC permission model, and workspace-scoped entity CRUD are all functional. Business rules and domain events are not yet implemented.

**Identity handling:** Core has no JWT/authentication middleware (no `AddJwtBearer`, no `UseAuthentication`) and no local CORS policy; browser CORS is enforced at Gateway. Core reads the caller's user id from the `X-User-Id` request header that the Gateway injects after validating the JWT, and the email from `X-User-Email` on invitation-accept flows. `WorkspaceEndpoints.GetUserId(HttpContext)` / `GetUserEmail(HttpContext)` are the shared helpers; a missing header throws `UnauthorizedAccessException` → 401. Core must therefore only be reachable through the Gateway.

### Key Files

- `Core/src/Relativa.Core/Program.cs` -- DI wiring, endpoint mapping
- `Core/src/Relativa.Core/Endpoints/` -- `WorkspaceEndpoints`, `MemberEndpoints`, `InvitationEndpoints`, `RoleEndpoints`, `OrganizationEndpoints`, `OrgMemberEndpoints`, `OrgInvitationEndpoints`, `OrgRoleEndpoints`, `JoinRequestEndpoints`, `EntityTypeEndpoints`, `EntityEndpoints`
- `Core/src/Relativa.Core.Application/Services/` -- `WorkspaceService`, `WorkspaceMemberService`, `InvitationService`, `RoleService`, `OrganizationService`, `OrgMemberService`, `OrgInvitationService`, `OrgRoleService`, `JoinRequestService`, `EntityTypeService`, `EntityService`
- `Core/src/Relativa.Core.Application/DTOs/` -- request/response DTOs organized by feature
- `Core/src/Relativa.Core.Application/Validators/` -- FluentValidation rules
- `Core/src/Relativa.Core.Domain/Interfaces/` -- repository interfaces
- `Core/src/Relativa.Core.Infrastructure/Data/RelativaDbContext.cs` -- full DbSet registration
- `Core/src/Relativa.Core.Infrastructure/Repositories/` -- EF repository implementations

---

## 4. Graph (`relativa-graph`)

**Purpose:** Real-time graph visualization service using SignalR. Will serve entity-relationship graph data and push live updates.

**Solution:** `Graph/Relativa.Graph.sln`
**Project:** `Graph/src/Relativa.Graph/`
**Port:** 8083

### Endpoints

| Method | Path | Auth | Behavior |
|---|---|---|---|
| GET | `/` | None | Returns `{"service":"relativa-graph"}` |
| WebSocket | `/hubs/graph` | None | SignalR hub (`GraphHub`) |
| GET | `/scalar/v1` | None | Scalar (dev only) |

### Status: Stub

`GraphHub` exists but `OnConnectedAsync` only calls `base`. No graph data queries, no RBAC filtering, no real-time push logic.

### Key Files

- `Graph/src/Relativa.Graph/Program.cs`
- `Graph/src/Relativa.Graph/Hubs/GraphHub.cs`

---

## 5. Audit (`relativa-audit`)

**Purpose:** Audit log API. In the full architecture, this is the single write-target for all domain event audit entries.

**Solution:** `Audit/Relativa.Audit.sln`
**Project:** `Audit/src/Relativa.Audit/`
**Port:** 8086

### Endpoints

| Method | Path | Auth | Behavior |
|---|---|---|---|
| GET | `/` | None | Returns `{"service":"relativa-audit"}` |
| GET | `/audit-log` | Policy `AuditReaders` (role = Admin or Analyst) | Returns empty array `[]` |

### Status: Stub

JWT Bearer is registered but **signature validation is disabled** (issuer, audience, signing key, lifetime checks all `false`). The `SignatureValidator` parses the token without cryptographic verification. The `/audit-log` endpoint returns an empty array. No actual audit event ingestion exists.

### Key Files

- `Audit/src/Relativa.Audit/Program.cs` -- JWT stub config, authorization policy, endpoints

---

## 6. Migration (`relativa-migration`)

**Purpose:** EF Core migration runner. Runs as a one-shot console host in Docker Compose before auth and core start.

**Solution:** `Migration/Relativa.Migration.sln`
**Project:** `Migration/src/Relativa.Migration/`
**Port:** None (exits after completion)

### Endpoints

None -- this is a console application, not a web service.

### Status: Functional

`MigrationDbContext` mirrors the full Persistence entity model. `Program.cs` builds a generic host, resolves the context, and calls `Database.MigrateAsync()`. Migrations live in `Migration/src/Relativa.Migration/Migrations/`:
- `20260416224419_InitialCreate.cs` -- full schema (split RBAC tables, org management tables, entity/property tables)
- `20260416224514_SeedData.cs` -- seed roles, permissions, orgs, workspaces, demo entities (raw SQL)

### Key Files

- `Migration/src/Relativa.Migration/Program.cs`
- `Migration/src/Relativa.Migration/Data/MigrationDbContext.cs`
- `Migration/src/Relativa.Migration/Migrations/` -- migration files

---

## 7. ML (`relativa-ml`)

**Purpose:** Machine learning service for scoring deals (closure probability, churn risk). Built on Django + DRF with planned Celery task scheduling.

**Stack:** Python 3.11, Django 5.1, Django REST Framework, scikit-learn (planned), Celery + Redis (configured but inactive)
**Project:** `ML/`
**Port:** 8084

### Endpoints

| Method | Path | Auth | Behavior |
|---|---|---|---|
| POST | `/api/ml/recalculate/` | None | Returns `{"status":"accepted","detail":"stub"}` |

### Status: Stub

The endpoint exists but returns a hardcoded stub. No ML models, no Celery tasks, no Redis broker in Docker Compose. `settings.py` has Celery config with a commented-out beat schedule for nightly recalculation at 02:00 UTC.

### Key Files

- `ML/ml_api/views.py` -- stub endpoint
- `ML/ml_api/urls.py` -- URL routing
- `ML/relativa_ml/` -- future ML model package
- `ML/pyproject.toml` -- dependencies

---

## 8. Client (`relativa-client`)

**Purpose:** Vue 3 single-page application. The user-facing frontend that communicates exclusively through the Gateway.

**Stack:** Vue 3, Vite, Node 20, vis-network (graph placeholder)
**Project:** `Client/`
**Port:** 3000 (configurable via `CLIENT_PORT` in `.env`)

### Endpoints

Not applicable -- this is a client-side SPA served by Vite dev server.

### Status: Scaffold

Vue 3 project with routing set up. `GraphView.vue` contains a vis-network placeholder. The app reads `VITE_GATEWAY_URL` from environment to know where the Gateway lives. D3 integration noted as "for later" in the code.

### Key Files

- `Client/src/` -- Vue source
- `Client/.env.example` -- `VITE_GATEWAY_URL`
- `Client/README.md`
