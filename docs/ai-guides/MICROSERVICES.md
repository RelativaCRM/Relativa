# Microservices -- Service Catalog

> **Last verified:** 2026-05-08 (Graph service now serves full RBAC-filtered graph via `GET /api/v1/graph`; graph route moved to org scope; `GraphQueryDbContext` added to Graph service; client GraphView fully rewritten.)

> **Maintenance obligation:** If you add, remove, or change any endpoint or service, update this file and its "Last verified" date before finishing your task. If you add or remove an entire service, also update [DOCKER-SETUP.md](DOCKER-SETUP.md) and [PROJECT-OVERVIEW.md](PROJECT-OVERVIEW.md). See [AI-GUIDES-INDEX.md](../../AI-GUIDES-INDEX.md) for the full update matrix.

---

## Service Map (quick reference)

| Service | Port | Stack | Status |
|---|---|---|---|
| Gateway | 8080 | .NET 10, YARP | Functional |
| Authentication | 8081 | .NET 10, JWT, BCrypt, FluentValidation | Functional |
| Core | 8082 | .NET 10, EF Core | Functional (org + workspace RBAC) |
| Graph | 8083 | .NET 10, SignalR, RabbitMQ | Functional — `GET /api/v1/graph` (RBAC-filtered user-centric graph), `POST .../entity-graph/create` (RPC to Core), SignalR hub, workspace choreography consumer |
| Audit | 8086 | .NET 10, EF Core, RabbitMQ | Functional |
| Migration | -- | .NET 10, EF Core (console) | Functional |
| ML | 8084 | Django 5.1, DRF, pika | Functional batch scoring API + choreography subscriber |
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
| GET | `/scalar/v1` | None | Scalar interactive API docs (uses merged `GET /openapi/aggregated.json`, including ML `POST /ml/api/ml/recalculate/` and `POST /ml/api/ml/score/batch`) |
| GET | `/openapi/v1.json` | None | Raw OpenAPI spec |
| GET | `/openapi/aggregated.json` | None | Merged Auth + Core + Audit + manual ML paths for Scalar |
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
- `PATCH /auth/api/v1/auth/me`
- `DELETE /auth/api/v1/auth/me`

### Status: Functional

YARP routing with global `.RequireAuthorization()`, JWT Bearer validation (issuer, audience, signing key, lifetime), and gateway-owned CORS all working. Gateway CORS is configured via `Cors:Origins` (allowlist + credentials by default) with optional local dev override `Cors:AllowAnyOriginForDev=true` (wildcard origin without credentials). Auth routes are split: `/login` and `/register` are anonymous, `/me` requires JWT.

**Identity forwarding:** a YARP request transform runs on every proxied request. It unconditionally removes any incoming `X-User-Id` / `X-User-Email` (so clients cannot spoof identity), then, if the request is authenticated, re-adds them from the validated `ClaimsPrincipal` (`sub` → `X-User-Id`, `email` → `X-User-Email`). **Core** trusts these headers and does **not** parse JWTs. The **Audit** service validates JWTs independently (same issuer/audience/key as Gateway) and resolves the caller from `sub` or `X-User-Id`. Graph/ML may follow either pattern.

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
| POST | `/api/v1/auth/register` | None | Creates user with bcrypt-hashed password (email stored lowercase), returns user DTO + Location header. Same normalized email is allowed if the only prior row is soft-archived (`is_archived`). |
| POST | `/api/v1/auth/login` | None | Validates credentials (email matched lowercase), returns `{ accessToken, expiresAt }` |
| GET | `/api/v1/auth/me` | JWT | Returns authenticated user's profile `{ id, email, firstName, lastName }` from JWT `sub` claim |
| PATCH | `/api/v1/auth/me` | JWT | Updates authenticated user's first and last name |
| DELETE | `/api/v1/auth/me` | JWT | Soft-archives the authenticated user (`is_archived`) |
| GET | `/health` | None | EF Core DB health check |
| GET | `/scalar/v1` | None | Scalar interactive API docs |
| GET | `/openapi/v1.json` | None | Raw OpenAPI spec |

### Status: Functional

Login, register, profile read/update/delete (`/me`) work end-to-end. Emails are normalized to lowercase on register and login. JWT includes `sub`, `email`, and `jti` claims (role and permissions are **not** embedded -- they are resolved per-request by Core using organization/workspace membership). FluentValidation on login and register endpoints. GlobalExceptionHandler maps `ValidationException` to 400, `UnauthorizedAccessException` to 401, `KeyNotFoundException` to 404, duplicate **active** user email to 409 (including PostgreSQL unique violations on concurrent insert of two non-archived rows with the same email).

**Not yet implemented:** token refresh, token blacklisting.

**Audit publishing:** Authentication writes payloads to `audit_outbox`; `AuditOutboxDispatcher` multiplexes publishes to **`audit.events`** routing keys prefixed with `audit.*` (`RabbitMqAudit:Exchange`). Domain choreography publishing is delegated to **`relativa.domain`**, but Auth only emits audit payloads today (`IOutboxWriter.EnqueueDomainAsync` intentionally no-op).

### Key Files

- `Authentication/src/Relativa.Authentication/Program.cs` -- DI, middleware, endpoint mapping
- `Authentication/src/Relativa.Authentication/Endpoints/AuthEndpoints.cs` -- route definitions
- `Authentication/src/Relativa.Authentication.Application/Services/AuthService.cs` -- login/profile; register delegates to `UserProvisioningService`
- `Authentication/src/Relativa.Authentication.Application/Services/UserProvisioningService.cs` -- shared user create/update/archive + audit
- `Authentication/src/Relativa.Authentication.Application/DTOs/` -- request/response DTOs (includes `UserProfileDto`)
- `Authentication/src/Relativa.Authentication.Application/Interfaces/IAuthService.cs`
- `Authentication/src/Relativa.Authentication.Application/Validators/` -- FluentValidation rules
- `Authentication/src/Relativa.Authentication.Domain/Interfaces/` -- `IUserRepository`, `ITokenService`, `IPasswordHasher`
- `Authentication/src/Relativa.Authentication.Infrastructure/Data/AuthDbContext.cs`
- `Authentication/src/Relativa.Authentication.Infrastructure/Repositories/UserRepository.cs`
- `Authentication/src/Relativa.Authentication.Infrastructure/Services/JwtTokenService.cs`
- `Authentication/src/Relativa.Authentication.Infrastructure/Services/BcryptPasswordHasher.cs`
- `Authentication/src/Relativa.Authentication.Infrastructure/Services/Audit/OutboxWriter.cs` — transactional outbox enqueue (audit payloads)
- `Authentication/src/Relativa.Authentication.Infrastructure/Services/Audit/AuditOutboxDispatcher.cs` — RabbitMQ dispatcher using `Messaging/src/Relativa.Messaging` helpers (`RabbitMqPublishingOptions.ConfigurationSectionKey = "RabbitMqAudit"`)

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
| DELETE | `/api/v1/organizations/{id}/members/{userId}` | JWT + `remove_org_members` | Remove another member only if caller's org role **strictly outranks** the target by `organization_roles.priority` (lower number = stronger); self-remove allowed without permission |
| PUT | `/api/v1/organizations/{id}/members/{userId}/role` | JWT + `assign_org_roles` | Change member's organization role |

### Endpoints -- Organization users (admin provisioning)

| Method | Path | Auth | Behavior |
|---|---|---|---|
| POST | `/api/v1/organizations/{id}/users` | JWT + `create_org_users` (and `assign_org_roles` when non-default role requested) | Create user account (bcrypt password), add to org with selected role (`orgRoleId?`, default `org_member`); 201 + Location |
| PATCH | `/api/v1/organizations/{id}/users/{userId}` | JWT + `edit_other_org_users_profile` | Update another member's first/last name (not self; use Auth `/me`) |
| DELETE | `/api/v1/organizations/{id}/users/{userId}` | JWT + `delete_org_users` | Archive user (soft-delete) when caller and target share email domain **and** caller's org role **strictly outranks** target's role by `priority`; **403** if targeting self (use Auth account deletion) |

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
| POST | `/api/v1/organizations/{id}/invitations` | JWT + `invite_to_org` (+ `assign_org_roles` for non-default role) | Invite user by email. Body: `{ email, orgRoleId? }`. Default role is `org_member`; specifying any other role additionally requires `assign_org_roles`. Token returned in response (no real email). |
| GET | `/api/v1/organizations/{id}/invitations` | JWT + `invite_to_org` | List pending, non-expired org invitations (includes `roleName`) |
| DELETE | `/api/v1/organizations/{id}/invitations/{invId}` | JWT + `invite_to_org` | Cancel org invitation (sets status `Cancelled`) |
| POST | `/api/v1/organizations/{id}/invitations/{invId}/resend` | JWT + `invite_to_org` | Rotate the token and extend `expires_at` on a pending invitation; returns the refreshed DTO |
| POST | `/api/v1/invitations/accept-org` | JWT + matching email | Accept organization invitation. Adds user with the `OrgRoleId` recorded on the invitation. |

### Endpoints -- Organization Roles

| Method | Path | Auth | Behavior |
|---|---|---|---|
| GET | `/api/v1/organizations/{id}/roles` | JWT + org membership | List org roles (system + custom); each item includes numeric **`priority`** (lower = stronger) |
| POST | `/api/v1/organizations/{id}/roles` | JWT + `manage_org_roles` | Create custom org role (`name`, `permissionIds`, **`priority`** ≥ 1) |
| PUT | `/api/v1/organizations/{id}/roles/{roleId}` | JWT + `manage_org_roles` | Update custom org role (optional `priority`, etc.) |
| DELETE | `/api/v1/organizations/{id}/roles/{roleId}` | JWT + `manage_org_roles` | Delete custom org role |

### Endpoints -- Combined Invitations (inbox)

| Method | Path | Auth | Behavior |
|---|---|---|---|
| GET | `/api/v1/invitations/mine` | JWT | Returns `{ organizationInvitations: [...] }` — pending org invitations for the caller's email. |
| GET | `/api/v1/invitations/mine/organization` | JWT | Same data as flat `OrgInvitationDto[]` (convenience alias). |

### Endpoints -- Workspaces

| Method | Path | Auth | Behavior |
|---|---|---|---|
| POST | `/api/v1/workspaces` | JWT + `create_workspaces` (org perm) | Create workspace within an organization (requires `organizationId`) |
| GET | `/api/v1/workspaces` | JWT | List workspaces for the authenticated user (each item includes `organizationId`). Optional query **`organizationId`**: restrict to workspaces in that org; caller must be an org member or **403 Forbidden**. Omit the query to list all workspaces the user belongs to (any org). |
| GET | `/api/v1/workspaces/{id}` | JWT + ws membership | Get workspace details |
| PUT | `/api/v1/workspaces/{id}` | JWT + `manage_ws_settings` | Update workspace name |
| DELETE | `/api/v1/workspaces/{id}` | JWT + ws_admin role | Archive workspace |

### Endpoints -- Workspace Members

| Method | Path | Auth | Behavior |
|---|---|---|---|
| GET | `/api/v1/workspaces/{id}/members` | JWT + ws membership | List workspace members |
| POST | `/api/v1/workspaces/{id}/members` | JWT + `add_ws_members` **or** org `manage_org_workspace_members` on parent org | Add an existing org member to the workspace (`{ userId, roleId }`). Caller need not be a workspace member when using the org-level permission. |
| PUT | `/api/v1/workspaces/{id}/members/{userId}/role` | JWT + `assign_ws_roles` | Change a member's workspace role |
| DELETE | `/api/v1/workspaces/{id}/members/{userId}` | JWT + `remove_ws_members` **or** org `manage_org_workspace_members` (or self) | Remove a member |

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
| GET | `/api/v1/entity-types` | **None** (anonymous via Gateway) | List all entity types with property definitions, `isStandalone`, `outgoingRelationships` (incl. `isRequired`, `relationshipCardinality`), and per-property `isReadonly`. |

### Endpoints -- Entities

| Method | Path | Auth | Behavior |
|---|---|---|---|
| GET | `/api/v1/workspaces/{workspaceId}/entities` | JWT + `view_entities` | List non-archived entities with property values. Optional query: `entityTypeId`, `q` (search string/number values), `take` (default 500, max 500). |
| GET | `/api/v1/workspaces/{workspaceId}/entities/{entityId}` | JWT + `view_entities` | Get entity detail (includes archived); DTO includes `isArchived`, `isReadonly` per property value, inbound/outbound relationship refs with previews. |
| POST | `/api/v1/workspaces/{workspaceId}/entities` | JWT + `create_entities` | Create entity + optional relationship **links** in one atomic transaction (standalone + readonly + required-outgoing rules enforced server-side). |
| PATCH | `/api/v1/workspaces/{workspaceId}/entities/{entityId}` | JWT + `edit_entities` | Merge-update writable property values (omitted keys unchanged; readonly properties rejected if changed). |
| DELETE | `/api/v1/workspaces/{workspaceId}/entities/{entityId}` | JWT + `delete_entities` | Soft-delete: sets `is_archived = true`. |

### Endpoints -- Infrastructure

| Method | Path | Auth | Behavior |
|---|---|---|---|
| GET | `/health` | None | EF Core DB health check |
| GET | `/scalar/v1` | None | Scalar interactive API docs (dev only) |
| GET | `/openapi/v1.json` | None | Raw OpenAPI spec |

### Status: Functional (organization + workspace RBAC + entity CRUD + transactional outbox for audit **and choreography** implemented)

Full clean-architecture layers are implemented: Domain (repository interfaces), Application (services, DTOs, validators), Infrastructure (EF repositories, contexts). Organization management (CRUD, members, join requests, invitations, roles), workspace management (CRUD, members, roles), the split RBAC permission model, workspace-scoped entity CRUD are functional.

**Outbox choreography pilot:** workspace create/update/archive now fan out **`DomainMessageEnvelope`** + `WorkspaceLifecyclePayloadV1` to routing keys `core.workspace.created|updated|archived`. Audit rows continue to enqueue in parallel (`AuditEventContract`).

**Identity handling:** Core has no JWT/authentication middleware (no `AddJwtBearer`, no `UseAuthentication`) and no local CORS policy; browser CORS is enforced at Gateway. Core reads the caller's user id from the `X-User-Id` request header that the Gateway injects after validating the JWT, and the email from `X-User-Email` on invitation-accept flows. `WorkspaceEndpoints.GetUserId(HttpContext)` / `GetUserEmail(HttpContext)` are the shared helpers; a missing header throws `UnauthorizedAccessException` → 401. Core must therefore only be reachable through the Gateway.

**Error contract:** `Core/src/Relativa.Core/Middleware/GlobalExceptionHandler.cs` returns JSON `{ status, title, detail }`. Map: `ValidationException` / `ArgumentException` → **400**; `UnauthorizedAccessException` → **401**; `ForbiddenAccessException` → **403**; `KeyNotFoundException` → **404**; `InvalidOperationException` → **409**; other → **500**. Application services (including `WorkspaceMemberService`, `OrgInvitationService`, `JoinRequestService`, `OrganizationUserAdminService`, `OrganizationService`) throw only those types for handled paths so clients never get opaque 500s for permission or conflict cases. Organization permission denials are now raised as **403** (`ForbiddenAccessException`) rather than **401**. The Vue client uses `normalizeError` / `useApiErrorHandler().notify` and `gatewayFetch` prefers **`detail`** over `title` for the thrown `ApiError.message` so toast text matches the server message.

### Key Files

- `Core/src/Relativa.Core/Program.cs` -- DI wiring, endpoint mapping
- `Core/src/Relativa.Core/Middleware/GlobalExceptionHandler.cs` — maps application exceptions to HTTP status + `{ status, title, detail }` JSON
- `Core/src/Relativa.Core/Endpoints/` -- `WorkspaceEndpoints`, `MemberEndpoints`, `InvitationEndpoints` (accept-org + my inbox only), `RoleEndpoints`, `OrganizationEndpoints`, `OrgMemberEndpoints`, `OrgInvitationEndpoints`, `OrgRoleEndpoints`, `JoinRequestEndpoints`, `EntityTypeEndpoints`, `EntityEndpoints`
- `Core/src/Relativa.Core.Application/Services/` -- `WorkspaceService`, `WorkspaceMemberService`, `RoleService`, `OrganizationService`, `OrgMemberService`, `OrgInvitationService`, `OrgRoleService`, `JoinRequestService`, `EntityTypeService`, `EntityService`
- `Core/src/Relativa.Core.Application/DTOs/` -- request/response DTOs organized by feature
- `Core/src/Relativa.Core.Application/Validators/` -- FluentValidation rules
- `Core/src/Relativa.Core.Domain/Interfaces/` -- repository interfaces
- `Core/src/Relativa.Core.Infrastructure/Data/RelativaDbContext.cs` -- full DbSet registration
- `Core/src/Relativa.Core.Infrastructure/Repositories/` -- EF repository implementations
- `Core/src/Relativa.Core.Infrastructure/Services/Audit/OutboxWriter.cs` — persists `audit_outbox` rows for audit **and choreography**
- `Core/src/Relativa.Core.Infrastructure/Messaging/EntityGraphCommandConsumerHostedService.cs` — consumes graph create RPC; runs `EntityService.CreateAsync` inside scoped DI (audit + same validation as HTTP create)

---

## 4. Graph (`relativa-graph`)

**Purpose:** Real-time graph visualization service. Serves RBAC-filtered user-centric graph data via HTTP and pushes live workspace lifecycle updates via SignalR.

**Solution:** `Graph/Relativa.Graph.sln`
**Project:** `Graph/src/Relativa.Graph/`
**Port:** 8083

### Endpoints

| Method | Path | Auth | Behavior |
|---|---|---|---|
| GET | `/` | None | Returns `{"service":"relativa-graph"}` |
| GET | `/api/v1/graph?organizationId={int}` | Gateway JWT → forwarded `X-User-Id` | Returns full RBAC-filtered graph for the authenticated user in the given organization. Response: `{ nodes: GraphNodeDto[], edges: GraphEdgeDto[] }`. Nodes represent: focal user (`user_self`), accessible workspaces (`workspace`), entities in those workspaces (`entity`), and manageable org members (`user`). Edges represent membership, containment, EAV relationships, and user-user management links. Each node carries `permissions` (e.g. `["view","edit","delete"]`) computed from the caller's workspace/org role permissions. Requires `view_entities` per workspace for entity nodes; `remove_org_members` or `manage_org_workspace_members` for user nodes. |
| POST | `/api/v1/workspaces/{workspaceId}/entity-graph/create` | Gateway JWT → forwarded `X-User-Id` | Publishes **`EntityGraphCreateRpcV1`** to exchange **`relativa.entity_graph`** with routing key **`entity_graph.create`**; waits on private reply queue (timeout → **504**). Body is JSON matching Core **`CreateEntityRequest`** (MVP single-node). Response body is Core **`EntityDetailDto`** JSON. |
| WebSocket | `/hubs/graph` | None | SignalR hub (`GraphHub`) |
| GET | `/scalar/v1` | None | Scalar (dev only) |

### Status: Functional

`GET /api/v1/graph` is served by `GraphDataService` which performs 6 targeted DB queries via `GraphQueryDbContext` (a read-oriented DbContext that applies the full Persistence model via `ApplyAllEntityConfigurations()`). Permission resolution mirrors the Core pattern: reads `X-User-Id` from the trusted gateway-injected header, then queries `user_role_organization` / `user_role_workspace` to build org and per-workspace permission sets before filtering entity and user visibility.

Workspace lifecycle choreography also remains: **`DomainEventConsumerHostedService`** binds `domain.events.graph.workspace.v1`, deduplicates, broadcasts **`domain.workspace.lifecycle.v1`**. **Entity graph create** delegates persistence to Core via Rabbit RPC (Graph must not call Core HTTP).

### Key Files

- `Graph/src/Relativa.Graph/Program.cs` — registers both `GraphDbContext` (idempotency) and `GraphQueryDbContext` (read queries), `IGraphDataService`, all endpoints
- `Graph/src/Relativa.Graph/Data/GraphQueryDbContext.cs` — read DbContext; applies full entity model
- `Graph/src/Relativa.Graph/Graph/GraphDataService.cs` — RBAC-filtered graph assembly (6 queries)
- `Graph/src/Relativa.Graph/Graph/GraphQueryEndpoints.cs` — `GET /api/v1/graph`
- `Graph/src/Relativa.Graph/Graph/GraphDtos.cs` — `GraphNodeDto`, `GraphEdgeDto`, `GraphResponseDto`
- `Graph/src/Relativa.Graph/Messaging/` — choreography consumer + Rabbit options
- `Graph/src/Relativa.Graph/EntityGraphEndpoints.cs` — HTTP surface for graph create
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
| GET | `/audit-log` | JWT + policy `AuditReaders` | Paginated audit rows + `filterContext`; workspace/org RBAC (see [AUDIT-LOG-API.md](AUDIT-LOG-API.md)) |
| GET | `/entities/{entityId}/audit-log` | JWT + policy `AuditReaders` | Same as `entity_type=entity` with fixed `entity_id`; **`workspace_id`** query required |

### Status: Functional

JWT Bearer uses full validation (issuer, audience, signing key, lifetime) aligned with the Gateway. The service also consumes RabbitMQ audit events (`audit.#`) and persists to `entity_audit_log`, `workspace_audit_log`, `organization_audit_log`, and `user_audit_log` with idempotency (`audit_processed_event`). Reads use EF-only queries, FluentValidation, and `GlobalExceptionHandler` (same error shape as Core).

### Key Files

- `Audit/src/Relativa.Audit/Program.cs` -- JWT, DI, exception handler, endpoint mapping
- `Audit/src/Relativa.Audit/Endpoints/AuditEndpoints.cs` -- `/audit-log` + entity-scoped route
- `Audit/src/Relativa.Audit/Services/AuditLogReadService.cs` -- list + RBAC + enriched DTOs
- `Audit/src/Relativa.Audit/Services/AuditEventConsumer.cs` -- RabbitMQ consumer + persistence
- `Audit/src/Relativa.Audit/Data/AuditDbContext.cs` -- shared persistence model

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

**Purpose:** Machine learning service for scoring deals (closure probability, churn risk). Built on Django + DRF with on-demand batch scoring and RabbitMQ-driven freshness updates.

**Stack:** Python 3.12, Django 5.1, Django REST Framework, scikit-learn (`.pkl` models loaded at startup), Celery + Redis (configured but inactive), pika (RabbitMQ choreography consumer)
**Project:** `ML/`
**Port:** 8084

### Endpoints

| Method | Path | Auth | Behavior |
|---|---|---|---|
| POST | `/api/ml/recalculate/` | None | Async enqueue endpoint; accepts explicit `entity_ids` or workspace mode and returns `202` with `job_id` |
| POST | `/api/ml/score/batch` | None | Accepts `{"entity_ids":[int,...]}`; returns `[{"entity_id", "closure_score", "churn_score", "unavailable_reason"}]` with null-safe per-entity scoring, 5 s timeout, and a structured `unavailable_reason` describing exactly which input is missing (no `deal_analysis` row yet, missing `created_at`, unrecognised `status`, no contract + no `deal_value` fallback, contract missing `amount`, etc) |

### Status: Functional scoring + async recalculation workers

Docker runs `manage.py run_domain_consumer` and `manage.py run_recalculate_consumer` concurrently with Django (see `ML/scripts/run_api_and_consumer.sh`). The domain subscriber binds `domain.events.ml.workspace.v1` to `relativa.domain` for both `core.workspace.*` and `core.entity.*`, and uses `rabbitmq_processed_delivery` for idempotency. Recalculation jobs are consumed from `domain.events.ml.recalculate.v1`.

`POST /api/ml/recalculate/` publishes `ml.recalculate.enqueued` domain events with a job payload; `run_recalculate_consumer` performs recomputation and emits progress/completed events. `POST /api/ml/score/batch` focuses on scoring and uses recompute service as stale-data fallback. Celery beat remains commented-out in `settings.py`.

### Key Files

- `ML/ml_api/management/commands/run_domain_consumer.py` — Rabbit consumer (blocking)
- `ML/ml_api/management/commands/run_recalculate_consumer.py` — async recalculation consumer
- `ML/ml_api/recalculate_service.py` — shared enqueue/recompute logic
- `ML/ml_api/views.py` — health + async recalculate + batch scoring handlers
- `ML/ml_api/urls.py` — URL routing
- `ML/scripts/run_api_and_consumer.sh` — runs consumer + Django server in Docker
- `ML/relativa_ml/` -- future ML model package
- `ML/pyproject.toml` -- dependencies

---

## 8. Client (`relativa-client`)

**Purpose:** Vue 3 single-page application. The user-facing frontend that communicates exclusively through the Gateway.

**Stack:** Vue 3, Vite, Node 20, vis-network
**Project:** `Client/`
**Port:** 3000 (configurable via `CLIENT_PORT` in `.env`)

### Endpoints

Not applicable -- this is a client-side SPA served by Vite dev server.

### Status: Functional (core CRM + full graph view)

Vue 3 project with routing, org/workspace management, entity CRUD, audit log, and full graph visualization. `GraphView.vue` fetches from `GET /graph/api/v1/graph` and renders all nodes and edges via vis-network with dynamic per-type color assignment. Graph is org-scoped (route `/graph`), not workspace-scoped. Node click surfaces a detail panel with View / Edit / Delete actions gated by the `permissions` array returned by the graph service. The app reads `VITE_GATEWAY_URL` from environment to know where the Gateway lives.

### Key Files

- `Client/src/` -- Vue source
- `Client/.env.example` -- `VITE_GATEWAY_URL`
- `Client/README.md`
