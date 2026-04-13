# Microservices -- Service Catalog

> **Last verified:** 2026-04-13 (workspace RBAC update)

> **Maintenance obligation:** If you add, remove, or change any endpoint or service, update this file and its "Last verified" date before finishing your task. If you add or remove an entire service, also update [DOCKER-SETUP.md](DOCKER-SETUP.md) and [PROJECT-OVERVIEW.md](PROJECT-OVERVIEW.md). See [AI-GUIDES-INDEX.md](../../AI-GUIDES-INDEX.md) for the full update matrix.

---

## Service Map (quick reference)

| Service | Port | Stack | Status |
|---|---|---|---|
| Gateway | 8080 | .NET 10, YARP | Functional |
| Authentication | 8081 | .NET 10, JWT, BCrypt, FluentValidation | Functional |
| Core | 8082 | .NET 10, EF Core | Stub (infra only) |
| Graph | 8083 | .NET 10, SignalR | Stub |
| Audit | 8086 | .NET 10 | Stub |
| Migration | -- | .NET 10, EF Core (console) | Functional |
| ML | 8084 | Django 5.1, DRF | Stub |
| Client | 3000 | Vue 3, Vite | Scaffold |

---

## 1. Gateway (`relativa-gateway`)

**Purpose:** YARP reverse proxy that routes all client traffic to backend services and enforces JWT authentication.

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

### Status: Functional

YARP routing, JWT Bearer validation (issuer, audience, signing key, lifetime), forwarded headers, Serilog, global exception handler, OpenAPI + Scalar all working.

### Key Files

- `Gateway/src/Relativa.Gateway/Program.cs` -- service wiring, JWT config, YARP setup
- `Gateway/src/Relativa.Gateway/appsettings.json` -- YARP routes and clusters, JWT settings
- `Gateway/src/Relativa.Gateway/Middleware/GlobalExceptionHandler.cs`

---

## 2. Authentication (`relativa-auth`)

**Purpose:** Issues JWT tokens after login/register; manages users and roles via EF Core against PostgreSQL.

**Solution:** `Authentication/Relativa.Authentication.sln`
**Project:** `Authentication/src/Relativa.Authentication/`
**Port:** 8081

### Endpoints

| Method | Path | Auth | Behavior |
|---|---|---|---|
| POST | `/api/v1/auth/register` | None | Creates user with bcrypt-hashed password, assigns role, returns user DTO + Location header |
| POST | `/api/v1/auth/login` | None | Validates credentials, returns `{ accessToken, expiresAt }` |
| GET | `/health` | None | EF Core DB health check |
| GET | `/scalar/v1` | None | Scalar interactive API docs |
| GET | `/openapi/v1.json` | None | Raw OpenAPI spec |

### Status: Functional

Login and register work end-to-end. JWT includes `sub`, `email`, and `jti` claims (role and permissions are **not** embedded -- they are resolved per-request by Core using workspace membership). FluentValidation on both endpoints. GlobalExceptionHandler maps `ValidationException` to 400, `UnauthorizedAccessException` to 401, duplicate email to 409. Registration no longer assigns a role -- `User.RoleId` is set to `null` on registration.

**Not yet implemented:** token refresh, token blacklisting.

### Key Files

- `Authentication/src/Relativa.Authentication/Program.cs` -- DI, middleware, endpoint mapping
- `Authentication/src/Relativa.Authentication/Endpoints/AuthEndpoints.cs` -- route definitions
- `Authentication/src/Relativa.Authentication.Application/Services/AuthService.cs` -- business logic
- `Authentication/src/Relativa.Authentication.Application/DTOs/` -- request/response DTOs
- `Authentication/src/Relativa.Authentication.Application/Validators/` -- FluentValidation rules
- `Authentication/src/Relativa.Authentication.Domain/Interfaces/` -- `IUserRepository`, `ITokenService`, `IPasswordHasher`
- `Authentication/src/Relativa.Authentication.Infrastructure/Data/AuthDbContext.cs`
- `Authentication/src/Relativa.Authentication.Infrastructure/Repositories/UserRepository.cs`
- `Authentication/src/Relativa.Authentication.Infrastructure/Services/JwtTokenService.cs`
- `Authentication/src/Relativa.Authentication.Infrastructure/Services/BcryptPasswordHasher.cs`

---

## 3. Core (`relativa-core`)

**Purpose:** The main business API for workspace management, member/invitation management, role/permission management, and (future) CRUD on entities, deals, and business rules.

**Solution:** `Core/Relativa.Core.sln`
**Project:** `Core/src/Relativa.Core/`
**Port:** 8082

### Endpoints

| Method | Path | Auth | Behavior |
|---|---|---|---|
| GET | `/health` | None | EF Core DB health check |
| GET | `/scalar/v1` | None | Scalar interactive API docs (dev only) |
| GET | `/openapi/v1.json` | None | Raw OpenAPI spec |
| POST | `/api/v1/workspaces` | JWT | Create workspace; creator becomes admin member |
| GET | `/api/v1/workspaces` | JWT | List workspaces for authenticated user |
| GET | `/api/v1/workspaces/{id}` | JWT + membership | Get workspace details |
| PUT | `/api/v1/workspaces/{id}` | JWT + `can_manage_settings` | Update workspace name |
| DELETE | `/api/v1/workspaces/{id}` | JWT + admin role | Archive workspace |
| GET | `/api/v1/workspaces/{id}/members` | JWT + membership | List workspace members |
| PUT | `/api/v1/workspaces/{id}/members/{userId}/role` | JWT + `can_assign_roles` | Change a member's role |
| DELETE | `/api/v1/workspaces/{id}/members/{userId}` | JWT + `can_assign_roles` (or self) | Remove a member |
| POST | `/api/v1/workspaces/{id}/invitations` | JWT + `can_assign_roles` | Invite user by email |
| GET | `/api/v1/workspaces/{id}/invitations` | JWT + `can_assign_roles` | List pending invitations |
| DELETE | `/api/v1/workspaces/{id}/invitations/{invId}` | JWT + `can_assign_roles` | Cancel invitation |
| POST | `/api/v1/invitations/accept` | JWT + matching email | Accept invitation by token |
| GET | `/api/v1/workspaces/{id}/roles` | JWT + membership | List roles (system + custom) |
| POST | `/api/v1/workspaces/{id}/roles` | JWT + `can_manage_settings` | Create custom role |
| PUT | `/api/v1/workspaces/{id}/roles/{roleId}` | JWT + `can_manage_settings` | Update custom role |
| DELETE | `/api/v1/workspaces/{id}/roles/{roleId}` | JWT + `can_manage_settings` | Archive custom role |
| GET | `/api/v1/permissions` | JWT | List all available permissions |

### Status: Functional (workspace RBAC implemented)

Full clean-architecture layers are implemented: Domain (repository interfaces), Application (services, DTOs, validators), Infrastructure (EF repositories, WorkspaceContext), Host (endpoint mapping, DI). Workspace creation, member management, invitation system, and custom role management are all functional. Entity/deal CRUD and business rules are not yet implemented.

### Key Files

- `Core/src/Relativa.Core/Program.cs` -- DI wiring, endpoint mapping
- `Core/src/Relativa.Core/Endpoints/` -- `WorkspaceEndpoints`, `MemberEndpoints`, `InvitationEndpoints`, `RoleEndpoints`
- `Core/src/Relativa.Core.Application/Services/` -- `WorkspaceService`, `WorkspaceMemberService`, `InvitationService`, `RoleService`
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
- `20260412140027_InitialCreate.cs` -- full schema
- `20260412140114_InitSeedData.cs` -- seed roles, permissions, orgs, workspaces, demo entities (raw SQL)

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
