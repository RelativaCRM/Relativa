# Architecture -- Patterns, Layers, and Conventions

> **Last verified:** 2026-04-17

> **Maintenance obligation:** If you change architecture patterns, add or modify a layer, alter the persistence model, change validation or auth flows, or introduce new cross-cutting concerns, update this file and its "Last verified" date before finishing your task. See [AI-GUIDES-INDEX.md](../../AI-GUIDES-INDEX.md) for the full update matrix.

---

## High-Level Architecture

```mermaid
flowchart LR
    Browser["Browser / SPA"]
    Gateway["Gateway\n(YARP)"]
    Auth["Authentication"]
    Core["Core"]
    Graph["Graph\n(SignalR)"]
    ML["ML\n(Django)"]
    Audit["Audit"]
    PG["PostgreSQL 16"]

    Browser -->|"HTTP / WS"| Gateway
    Gateway -->|"/auth/*"| Auth
    Gateway -->|"/core/*"| Core
    Gateway -->|"/graph/*"| Graph
    Gateway -->|"/ml/*"| ML
    Gateway -->|"/audit/*"| Audit
    Auth --> PG
    Core --> PG
```

All client traffic flows through the **Gateway**. Backend services do not call each other directly -- there is no message bus, no gRPC, no service-to-service HTTP. The Gateway strips path prefixes and forwards requests to upstream services by internal Docker DNS.

---

## Layered / Clean Architecture

The project uses a **ports-and-adapters (Clean Architecture)** pattern. Each service that follows this pattern is split into four projects:

| Layer | Responsibility | Naming convention |
|---|---|---|
| **Host** | `Program.cs`, endpoint mapping, middleware | `Relativa.<Service>` |
| **Application** | Use cases, DTOs, validators, service interfaces/implementations | `Relativa.<Service>.Application` |
| **Domain** | Repository interfaces, service contracts, domain logic | `Relativa.<Service>.Domain` |
| **Infrastructure** | DbContext, EF repositories, external service implementations (JWT, bcrypt) | `Relativa.<Service>.Infrastructure` |

### Current state per service

| Service | Host | Application | Domain | Infrastructure |
|---|---|---|---|---|
| **Authentication** | Implemented | Implemented (AuthService, DTOs, validators) | Implemented (interfaces only) | Implemented (AuthDbContext, repos, JWT, bcrypt) |
| **Core** | Implemented (org, workspace, member, invitation, role, join-request, permission endpoints) | Implemented (OrganizationService, OrgMemberService, OrgInvitationService, OrgRoleService, JoinRequestService, WorkspaceService, WorkspaceMemberService, InvitationService, RoleService, DTOs, validators) | Implemented (repository interfaces, IWorkspaceContext) | Implemented (RelativaDbContext, repos, WorkspaceContext) |
| **Gateway** | Implemented | N/A (single project) | N/A | N/A |
| **Graph** | Implemented (stub hub) | N/A (single project) | N/A | N/A |
| **Audit** | Implemented (stub) | N/A (single project) | N/A | N/A |

Gateway, Graph, and Audit are single-project services with no layered split. When they grow, they should follow the same four-layer convention as Authentication.

### Dependency direction

```
Host → Application → Domain ← Infrastructure
                                    ↓
                              Persistence (shared)
```

- **Domain** defines interfaces; **Infrastructure** implements them.
- **Application** depends on Domain interfaces, never on Infrastructure directly.
- **Host** wires everything via DI and maps endpoints.
- All layers that need entities reference the shared **Persistence** library.

**Important caveat:** Domain interfaces return `Relativa.Persistence.Entities.*` types directly (e.g. `IUserRepository` returns `User`). This is a pragmatic coupling -- the domain layer is not isolated from the shared entity assembly.

---

## Data Modeling Conventions

### Third Normal Form (3NF)

All tables must satisfy **Third Normal Form**:

- **1NF**: no repeating groups, every column is atomic.
- **2NF**: all non-key attributes fully depend on the entire primary key.
- **3NF**: no transitive dependencies -- every non-key attribute depends on the key, the whole key, and nothing but the key.

**No denormalized columns.** Names, labels, and derived data are always resolved via JOINs, never duplicated across tables. This convention applies to all future schema changes.

---

## Shared Persistence Library

**Path:** `Persistence/src/Relativa.Persistence/`

This is a **.NET class library** (no solution, no runnable host) that holds the EF Core entity model shared across services. It is referenced via `ProjectReference` by Core, Authentication, and Migration.

### Contents

| Directory / File | What it contains |
|---|---|
| `Entities/` | 20 entity classes (see list below) |
| `Configurations/` | EF Fluent API `IEntityTypeConfiguration<T>` classes for each entity |
| `ModelBuilderExtensions.cs` | Extension methods: `ApplyAuthEntityConfigurations` (User only) and `ApplyAllEntityConfigurations` (full model) |

### Entity list (20 entities)

| Entity | Table name | Notes |
|---|---|---|
| `User` | `users` | Credentials and profile. No `role_id` column (dropped). |
| `Organization` | `organizations` | Top-level tenant boundary. |
| `Workspace` | `workspaces` | Has `organization_id` FK (direct, no join table). |
| `OrganizationRole` | `organization_roles` | **NEW.** Org-scoped roles (system + custom). |
| `OrganizationRolePermission` | `organization_role_permissions` | **NEW.** Join between org roles and permissions. |
| `UserRoleOrganization` | `user_role_organization` | **NEW.** Org membership: user + org + org role. |
| `OrganizationJoinRequest` | `organization_join_requests` | **NEW.** Pending/approved/rejected requests to join an org. |
| `OrganizationInvitation` | `organization_invitations` | **NEW.** Email-based invitations to join an org. |
| `WorkspaceRole` | `workspace_roles` | **Renamed** from `roles`. Ws-scoped roles (system + custom). |
| `WorkspaceRolePermission` | `workspace_role_permissions` | **Renamed** from `role_permissions`. Join between ws roles and permissions. |
| `UserRoleWorkspace` | `user_role_workspace` | **Renamed** from `workspace_members`. Ws membership: user + workspace + ws role. |
| `Permission` | `permissions` | **Shared** by both org and ws role-permission joins. 16 granular permissions. |
| `WorkspaceInvitation` | `workspace_invitations` | Email-based invitations to join a workspace. |
| `EntityType` | `entity_types` | Discriminator (`client`, `deal`). |
| `Entity` | `entities` | Business record typed by EntityType. |
| `EntityWorkspace` | `entity_workspaces` | Join between Entity and Workspace. |
| `EntityProperty` | `entity_properties` | Polymorphic property row for an Entity. |
| `PersonalDataPropertyValue` | `personal_data_property_values` | Name/contact data for client-type entities. |
| `LocationPropertyValue` | `location_property_values` | Address/geo data for client-type entities. |
| `DealPropertyValue` | `deal_property_values` | Deal value, expected close, closure_score, owner, linked client. |

**Dropped:** `OrganizationWorkspace` (replaced by `workspaces.organization_id` FK). `users.role_id` column removed.

### Multiple DbContexts over one model

Different services compose **different slices** of the entity model:

| DbContext | Location | What it maps | Extension used |
|---|---|---|---|
| `AuthDbContext` | `Authentication/.../Infrastructure/Data/AuthDbContext.cs` | User | `ApplyAuthEntityConfigurations` |
| `RelativaDbContext` | `Core/.../Infrastructure/Data/RelativaDbContext.cs` | All 20 entities | `ApplyAllEntityConfigurations` |
| `MigrationDbContext` | `Migration/.../Data/MigrationDbContext.cs` | All 20 entities | `ApplyAllEntityConfigurations` |

Migrations are owned by the **Migration** service. The migration assembly name is `Relativa.Migration`. Schema changes always go through `Migration/src/Relativa.Migration/Migrations/`.

### DbContext Ownership Matrix

This matrix defines which service is the **authoritative writer** for each table, to support future schema splitting:

| Table | Auth (read/write) | Core (read/write) |
|---|---|---|
| `users` | **Read/Write** (credentials) | Read-only (identity resolution) |
| `organizations` | -- | **Read/Write** |
| `organization_roles` | -- | **Read/Write** |
| `organization_role_permissions` | -- | **Read/Write** |
| `user_role_organization` | -- | **Read/Write** |
| `organization_join_requests` | -- | **Read/Write** |
| `organization_invitations` | -- | **Read/Write** |
| `workspaces` | -- | **Read/Write** |
| `workspace_roles` | -- | **Read/Write** |
| `workspace_role_permissions` | -- | **Read/Write** |
| `user_role_workspace` | -- | **Read/Write** |
| `workspace_invitations` | -- | **Read/Write** |
| `permissions` | -- | **Read/Write** |
| All entity/property tables | -- | **Read/Write** |

**Rules:**
1. Auth service must **never write** to any table other than `users`.
2. Core service must **never write** to password hashes or JWT-related User fields.
3. New workspace/org-related configurations go into `ApplyAllEntityConfigurations()` only -- they are **not** added to `ApplyAuthEntityConfigurations()`.

---

## Domain Model

```mermaid
erDiagram
    Organization ||--o{ Workspace : "has workspaces"
    Organization ||--o{ UserRoleOrganization : "has members"
    Organization ||--o{ OrganizationRole : "has roles"
    Organization ||--o{ OrganizationJoinRequest : "join requests"
    Organization ||--o{ OrganizationInvitation : "invitations"
    OrganizationRole ||--o{ OrganizationRolePermission : grants
    OrganizationRole ||--o{ UserRoleOrganization : "assigned via"
    Workspace ||--o{ EntityWorkspace : contains
    Workspace ||--o{ UserRoleWorkspace : "has members"
    Workspace ||--o{ WorkspaceInvitation : "has invites"
    Workspace ||--o{ WorkspaceRole : "scoped roles"
    WorkspaceRole ||--o{ WorkspaceRolePermission : grants
    WorkspaceRole ||--o{ UserRoleWorkspace : "assigned via"
    Permission ||--o{ OrganizationRolePermission : "org granted via"
    Permission ||--o{ WorkspaceRolePermission : "ws granted via"
    Entity ||--o{ EntityWorkspace : "belongs to"
    EntityType ||--o{ Entity : types
    Entity ||--o{ EntityProperty : has
    EntityProperty ||--o| PersonalDataPropertyValue : "value (client)"
    EntityProperty ||--o| LocationPropertyValue : "value (client)"
    EntityProperty ||--o| DealPropertyValue : "value (deal)"
    DealPropertyValue }o--|| User : "owned by"
    User ||--o{ UserRoleOrganization : "member of orgs"
    User ||--o{ UserRoleWorkspace : "member of workspaces"
```

**Key relationships:**

- **Organization is the primary multi-tenant boundary.** Users must join an organization before they can access workspaces within it.
- `Organization` → `Workspace` is a direct FK (`workspaces.organization_id`), not a join table.
- **Split RBAC schema:** Organization roles and workspace roles are separate table hierarchies that share a common `permissions` table.
  - **Org path:** `User` → `UserRoleOrganization` → `OrganizationRole` → `OrganizationRolePermission` → `Permission`
  - **Ws path:** `User` → `UserRoleWorkspace` → `WorkspaceRole` → `WorkspaceRolePermission` → `Permission`
- **16 granular permissions** in the shared `permissions` table:
  - **7 org-scoped:** `manage_org_settings`, `invite_to_org`, `manage_join_requests`, `remove_org_members`, `assign_org_roles`, `manage_org_roles`, `create_workspaces`
  - **9 ws-scoped:** `manage_ws_settings`, `invite_to_workspace`, `add_ws_members`, `remove_ws_members`, `assign_ws_roles`, `manage_ws_roles`, `edit_deals`, `view_deals`, `view_analytics`
- **7 default system roles:**
  - **3 org roles:** `org_owner` (all 7 org perms), `org_admin` (subset), `org_member` (minimal)
  - **4 ws roles:** `ws_admin` (all 9 ws perms), `ws_manager` (subset), `ws_analyst` (view-only), `ws_member` (minimal)
- `OrganizationJoinRequest` tracks pending/approved/rejected requests from users wanting to join an org.
- `OrganizationInvitation` tracks email-based invitations to join an org (parallel to `WorkspaceInvitation` for workspaces).
- `Entity` belongs to workspaces via `EntityWorkspace` and is typed by `EntityType` (`client` or `deal`).
- `EntityProperty` is a polymorphic property bag: each row points to at most one of `PersonalDataPropertyValue`, `LocationPropertyValue`, or `DealPropertyValue`.
- `DealPropertyValue` has an `owner` (User) and a linked `client` (Entity), plus `value`, `expected_close`, `closure_score`, and timestamps.
- `OrganizationRole.OrganizationId` is **nullable** -- `null` for system roles, set for custom org-specific roles.
- `WorkspaceRole.WorkspaceId` is **nullable** -- `null` for system roles, set for custom workspace-specific roles.

---

## Validation Approach

Validation uses **FluentValidation** in the Application layer with explicit invocation in service methods.

### Flow

1. **Validator discovery:** `AddValidatorsFromAssemblyContaining<>()` in `Program.cs` registers all validators from the Application assembly via DI.
2. **Explicit validation:** Service methods call `validator.ValidateAndThrowAsync(request)` before any business logic.
3. **Exception mapping:** `GlobalExceptionHandler` middleware catches `ValidationException` and returns HTTP 400 with structured error details.

There is **no** global automatic validation filter or minimal-API endpoint filter. Validation is always explicitly called inside the application service.

### Validators implemented

**Authentication:**
- `LoginRequestValidator` -- in `Authentication/src/Relativa.Authentication.Application/Validators/`
- `RegisterRequestValidator` -- in `Authentication/src/Relativa.Authentication.Application/Validators/`

**Core:**
- `CreateWorkspaceRequestValidator`, `UpdateWorkspaceRequestValidator` -- workspace operations
- `InviteMemberRequestValidator`, `AcceptInvitationRequestValidator` -- workspace invitations
- `CreateRoleRequestValidator` -- role management
- `UpdateMemberRoleRequestValidator` -- member management
- Organization-related validators for org CRUD, join requests, org invitations, org roles

### Convention for new services

When adding validation to a new service, follow the same pattern:
1. Create `*Validator` classes in the Application project using FluentValidation.
2. Register via `AddValidatorsFromAssemblyContaining<>()` in `Program.cs`.
3. Call `ValidateAndThrowAsync` in the service method.
4. Ensure `GlobalExceptionHandler` maps `ValidationException` to 400.

---

## Authentication and Authorization Flow

```mermaid
sequenceDiagram
    participant Client as Browser / SPA
    participant GW as Gateway
    participant Auth as Authentication
    participant Core as Core
    participant DB as PostgreSQL

    Client->>GW: POST /auth/api/v1/auth/login
    GW->>Auth: POST /api/v1/auth/login (anonymous route)
    Auth->>DB: Lookup user by email
    DB-->>Auth: User
    Auth->>Auth: Verify bcrypt password
    Auth->>Auth: Generate JWT (sub, email, jti)
    Auth-->>GW: 200 { accessToken, expiresAt }
    GW-->>Client: 200 { accessToken, expiresAt }

    Client->>GW: GET /auth/api/v1/auth/me (Bearer JWT)
    GW->>GW: Validate JWT
    GW->>Auth: Forward request
    Auth->>DB: Lookup user by sub claim
    Auth-->>GW: 200 { id, email, firstName, lastName }
    GW-->>Client: 200 { id, email, firstName, lastName }

    Client->>GW: POST /core/api/v1/organizations (Bearer JWT)
    GW->>GW: Validate JWT
    GW->>Core: Forward request
    Core->>DB: Create org, assign creator as org_owner
    Core-->>GW: 201 organization
    GW-->>Client: 201 organization

    Client->>GW: GET /core/api/v1/workspaces/{id}/members (Bearer JWT)
    GW->>GW: Validate JWT
    GW->>Core: Forward request
    Core->>DB: Lookup UserRoleWorkspace by userId + workspaceId
    DB-->>Core: Membership + Role + Permissions
    Core->>Core: Authorize based on workspace role
    Core-->>GW: Response
    GW-->>Client: Response
```

### JWT details

- **Issuing service:** Authentication (`JwtTokenService`)
- **Algorithm:** HMAC-SHA256 (symmetric key from `JWT_SECRET`)
- **Claims:** `sub` (user ID), `email`, `jti` (token ID). Role and permissions are **not** embedded in the JWT -- they are resolved per-request by Core using organization/workspace membership DB lookups.
- **Validation point:** Gateway validates issuer, audience, signing key, and lifetime
- **Audit exception:** Audit service has JWT registered but all validation checks disabled (stub)

### Organization-scoped authorization

Authorization for organization endpoints:
1. Core extracts the user ID from the JWT `sub` claim.
2. Core looks up the `UserRoleOrganization` record for the user + organization pair, which includes the org role and its permissions.
3. Each org endpoint handler checks the required org permission before executing business logic.
4. Some endpoints only require org membership (e.g. listing members), while others require specific permissions (e.g. `manage_org_settings` to update org details).

### Workspace-scoped authorization

Authorization for workspace endpoints:
1. Core extracts the user ID from the JWT `sub` claim.
2. Core looks up the `UserRoleWorkspace` record for the user + workspace pair, which includes the ws role and its permissions.
3. Each workspace endpoint handler checks the required ws permission before executing business logic.
4. Workspace creation requires the `create_workspaces` **org-scoped** permission in the target organization.

### Authorization policies

- **Gateway:** `MapReverseProxy().RequireAuthorization()` -- all proxied routes require a valid JWT unless explicitly marked anonymous in YARP route config. Auth routes are split: `/login` and `/register` are anonymous, `/me` requires JWT.
- **Audit:** `AuditReaders` policy requires any authenticated user (stub -- will be refined when Audit is implemented).
- **Core:** Per-endpoint authorization via `UserRoleOrganization` or `UserRoleWorkspace` DB lookups (no ASP.NET authorization policies; checked in application service layer).

---

## Inter-Service Communication

- **HTTP via Gateway only.** No service-to-service calls exist.
- **No message bus.** No RabbitMQ, Kafka, MassTransit, or gRPC.
- **SignalR:** Graph service exposes a WebSocket hub at `/hubs/graph` for real-time client updates (not inter-service messaging).
- **Planned:** Domain events from Core to Audit (mechanism not yet decided -- could be direct HTTP, a message bus, or outbox pattern).

---

## Cross-Cutting Concerns

| Concern | Implementation | Where |
|---|---|---|
| **Logging** | Serilog (console + rolling file) | Core, Authentication, Gateway |
| **Exception handling** | `IExceptionHandler` + `GlobalExceptionHandler` + `AddProblemDetails()` | Core, Authentication, Gateway (distinct implementations per host) |
| **Health checks** | `/health` endpoint, EF Core DB checks on Auth and Core | All .NET services |
| **API docs** | OpenAPI + Scalar (`/scalar/v1`, `/openapi/v1.json`) | Auth, Core, Gateway, Graph (dev) |
| **CORS** | `AllowAnyOrigin/Header/Method` | Core only (tighten for production) |
| **Forwarded headers** | `X-Forwarded-For`, `X-Forwarded-Proto` | Gateway |

---

## Coding Conventions

| Convention | Details |
|---|---|
| **API style** | Minimal APIs exclusively -- no MVC `[ApiController]` classes anywhere |
| **Endpoint organization** | Static extension methods (e.g. `AuthEndpoints.MapAuthEndpoints()`, `OrganizationEndpoints.MapOrganizationEndpoints()`, `WorkspaceEndpoints.MapWorkspaceEndpoints()`) |
| **DI lifetimes** | Scoped for repositories and application services; singleton for `IPasswordHasher` |
| **Configuration** | Options pattern (`Configure<JwtOptions>`) |
| **No `Startup.cs`** | All configuration in `Program.cs` (minimal hosting model) |
| **Target framework** | `net10.0` across all .NET projects |
| **Package versioning** | Referenced in `Asp.Versioning.Http` (Authentication) but **not yet used** in code |
