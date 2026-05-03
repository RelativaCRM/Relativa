# Project Overview -- What is Relativa?

> **Last verified:** 2026-05-02

> **Maintenance obligation:** If you change the general purpose, domain model, tech stack, or repo layout, update this file and its "Last verified" date before finishing your task. See [AI-GUIDES-INDEX.md](../../AI-GUIDES-INDEX.md) for the full update matrix.

---

## General Purpose

Relativa is a **multi-tenant CRM / sales-workspace platform**. It lets organizations manage clients, deals, and workspaces with role-based access control, graph visualization of entity relationships, and ML-driven scoring (closure probability, churn risk). The system is designed for sales teams that need structured pipelines with analytics.

---

## Business Domain

| Concept | Description |
|---|---|
| **Organization** | **Primary multi-tenant boundary.** Users must join an organization before accessing workspaces. Owns workspaces directly (`workspaces.organization_id` FK). Has its own roles, members, join requests, and invitations. |
| **Workspace** | Isolated working area within an organization. Has a creator (User) and members. Entities belong to workspaces via `EntityWorkspace`. |
| **UserRoleOrganization** | Join between User, Organization, and OrganizationRole. A user can be in multiple organizations with different roles. |
| **UserRoleWorkspace** | Join between User, Workspace, and WorkspaceRole. A user can be in multiple workspaces with different roles. Must be an org member first. |
| **OrganizationJoinRequest** | Tracks pending/approved/rejected requests from users wanting to join an organization. |
| **OrganizationInvitation** | Email-based invitation to join an organization. Carries an email, a target org role, and an expiry date. |
| **WorkspaceInvitation** | Email-based invitation to join a workspace. Carries an email, a target ws role, and an expiry date. |
| **User** | A person with credentials. Has no global role -- roles are scoped to each organization and workspace membership. |
| **OrganizationRole** | Named role scoped to an organization (e.g. `org_owner`, `org_admin`, `org_member`). Linked to permissions via `OrganizationRolePermission`. `OrganizationId` is nullable: `null` for system roles, set for custom org-specific roles. |
| **WorkspaceRole** | Named role scoped to a workspace (e.g. `ws_admin`, `ws_manager`, `ws_analyst`, `ws_member`). Linked to permissions via `WorkspaceRolePermission`. `WorkspaceId` is nullable: `null` for system roles, set for custom workspace-specific roles. |
| **Permission** | Granular capability shared by both org and ws role hierarchies. 16 total: 7 org-scoped (e.g. `manage_org_settings`, `create_workspaces`) and 9 ws-scoped (e.g. `manage_ws_settings`, `edit_deals`, `view_analytics`). |
| **EntityType** | Named type discriminator (`client`, `deal`). Extensible — new types can be added by inserting a row. |
| **Entity** | A business record typed by `EntityType`. Lives in workspaces via `EntityWorkspace`. All entity types share the same EAV storage — no per-type tables. |
| **Property** | A named attribute definition with a data type (`String`, `Int`, `Decimal`, `Bool`, `Date`). Global (`organization_id = NULL`) or org-specific (`organization_id` set). |
| **EntityTypeProperty** | Schema-layer mapping: which `Property` definitions belong to which `EntityType`, with an `is_required` flag. Composite PK on `(entity_type_id, property_id)`. |
| **EntityPropertyValue** | Data-layer storage: one row per entity+property pair holding the actual typed value. Only one of `value_string / value_int / value_decimal / value_bool / value_date` is populated per row. |
| **EntityRelationshipType** | Schema-layer definition of a valid directed link between two entity types (e.g. `deal_client`: deal → client). |
| **EntityRelationship** | Data-layer instance of a directed link between two entity records, typed by `EntityRelationshipType`. Replaces the old hard-coded `deal_property_values.client_id` FK. |

The domain model lives entirely in the shared Persistence library (`Persistence/src/Relativa.Persistence/Entities/`).

### User flow

1. **Register** → user has no memberships.
2. **Create or join an organization** → user becomes an org member with an org role (creator gets `org_owner`).
3. **Create or join a workspace** within the organization → user becomes a ws member with a ws role (creator gets `ws_admin`). Workspace creation requires the `create_workspaces` org permission.
4. **Work within the workspace** → RBAC governs what the user can do (manage settings, invite members, edit deals, view analytics, etc.).

---

## Tech Stack

| Layer | Technology |
|---|---|
| Backend services | ASP.NET Core 10, Minimal APIs |
| API gateway | YARP (Yet Another Reverse Proxy) on ASP.NET Core 10 |
| Real-time | SignalR (Graph service) |
| ML service | Django 5.1, Django REST Framework, scikit-learn, Celery + Redis (planned) |
| Frontend | Vue 3 + Vite + TypeScript, Pinia, Vue Router, PrimeVue 4 (Aura), Tailwind CSS 3, vis-network (graph placeholder) |
| Database | PostgreSQL 16 |
| ORM | Entity Framework Core 10 + Npgsql |
| Messaging | RabbitMQ (transactional outbox → `audit.events` + choreography `relativa.domain`; shared helpers in `Messaging/`) |
| Auth | JWT (symmetric key), BCrypt password hashing, FluentValidation |
| Logging | Serilog (console + rolling file) |
| API docs | OpenAPI + Scalar |
| Containerization | Docker Compose (single bridge network) |

---

## Repository Layout

Relativa is a **monorepo**. Each service has its own .NET solution (or package manager for non-.NET). There is no single root `.sln`.

```
Relativa/
├── AI-GUIDES-INDEX.md          # Entry point for AI agents
├── DOCKER-BUILD.md             # Operational Docker guide (how to run)
├── SCALAR-GUIDE.md             # Scalar API docs walkthrough
├── CONTRIBUTORS.md
├── .env.example                # Env template for Docker Compose
├── docker-compose.yaml         # Full stack definition
│
├── Gateway/                    # YARP reverse proxy (.NET 10)
│   └── src/Relativa.Gateway/
├── Authentication/             # Auth service (.NET 10, clean architecture)
│   └── src/
│       ├── Relativa.Authentication/          # Host (Program.cs, Endpoints)
│       ├── Relativa.Authentication.Application/  # DTOs, validators, AuthService
│       ├── Relativa.Authentication.Domain/       # Interfaces
│       └── Relativa.Authentication.Infrastructure/  # DbContext, repos, JWT, bcrypt
├── Core/                       # Business API (.NET 10, clean architecture)
│   └── src/
│       ├── Relativa.Core/                    # Host (Program.cs, Endpoints)
│       ├── Relativa.Core.Application/        # Services, DTOs, validators
│       ├── Relativa.Core.Domain/             # Repository interfaces
│       └── Relativa.Core.Infrastructure/     # RelativaDbContext
├── Messaging/                  # RabbitMQ helpers for outbox publishers (.NET class library)
│   └── src/Relativa.Messaging/
├── Graph/                      # SignalR graph service (.NET 10 + choreography consumer)
│   └── src/Relativa.Graph/
├── Audit/                      # Audit log API (.NET 10)
│   └── src/Relativa.Audit/
├── Migration/                  # EF Core migration runner (.NET 10 console)
│   └── src/Relativa.Migration/
├── Persistence/                # Shared EF Core entity library (no .sln)
│   └── src/Relativa.Persistence/
│       ├── Contracts/          # Audit + choreography envelopes (shared with consumers)
│       ├── Entities/           # Domain + audit/outbox/support entities (see ARCHITECTURE list)
│       ├── Configurations/     # Fluent API configs
│       └── ModelBuilderExtensions.cs
├── Client/                     # Vue 3 + Vite SPA
├── ML/                         # Django ML service
│   ├── ml_api/                 # Django app
│   └── relativa_ml/            # ML package (future models)
└── .github/
    └── pull_request_template.md
```

---

## Existing Documentation

| File | Scope | Notes |
|---|---|---|
| `DOCKER-BUILD.md` | Operational Docker guide | Current and accurate. |
| `docs/runbooks/RABBITMQ-CHOREOGRAPHY.md` | Rabbit choreography + DLQ operations | Describes exchanges, purge commands, idempotency table |
| `SCALAR-GUIDE.md` | Scalar API docs walkthrough | Current and accurate. |
| `CONTRIBUTORS.md` | Contributor list | -- |
| `Authentication/README.md` | Auth service | **Outdated** -- claims 501 stubs but login/register/me are implemented. |
| `Core/README.md` | Core service | Partially outdated -- mentions migrations in Core; they now live in Migration. |
| `Gateway/README.md` | Gateway | Partially outdated -- says JWT validation is a stub; it is now fully configured. |
| `Graph/README.md` | Graph service | Accurate (describes stub state). |
| `Audit/README.md` | Audit service | Outdated -- Audit is now functional with RabbitMQ consumer + persistence. |
| `Migration/README.md` | Migration runner | **Outdated** -- describes entrypoint.sh; actual code uses `MigrateAsync`. |
| `ML/README.md` | ML service | Accurate (describes stub state). |
| `Client/README.md` | Vue client | Accurate (Vite scaffold docs). |

Per-service READMEs are written in Ukrainian.
