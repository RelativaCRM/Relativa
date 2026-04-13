# Project Overview -- What is Relativa?

> **Last verified:** 2026-04-13

> **Maintenance obligation:** If you change the general purpose, domain model, tech stack, or repo layout, update this file and its "Last verified" date before finishing your task. See [AI-GUIDES-INDEX.md](../../AI-GUIDES-INDEX.md) for the full update matrix.

---

## General Purpose

Relativa is a **multi-tenant CRM / sales-workspace platform**. It lets organizations manage clients, deals, and workspaces with role-based access control, graph visualization of entity relationships, and ML-driven scoring (closure probability, churn risk). The system is designed for sales teams that need structured pipelines with analytics.

---

## Business Domain

| Concept | Description |
|---|---|
| **Organization** | Top-level tenant. Owns one or more workspaces. |
| **Workspace** | Isolated working area within an organization. Entities belong to workspaces via `EntityWorkspace`. |
| **User** | A person with credentials, belonging to one Role. |
| **Role** | Named role (e.g. `admin`, `sales_manager`, `analyst`). Linked to Permissions via `RolePermission`. |
| **Permission** | Granular capability (e.g. `can_edit_deals`, `can_view_analytics`). |
| **EntityType** | Discriminator string (`client`, `deal`). |
| **Entity** | A business record typed by EntityType. Lives in workspaces. |
| **EntityProperty** | A property row for an Entity, pointing to one of the polymorphic value tables below. |
| **PersonalDataPropertyValue** | Name/contact data for client-type entities. |
| **LocationPropertyValue** | Address/geo data for client-type entities. |
| **DealPropertyValue** | Deal value, expected close date, closure_score, owner (User), linked client. |

The domain model lives entirely in the shared Persistence library (`Persistence/src/Relativa.Persistence/Entities/`).

---

## Tech Stack

| Layer | Technology |
|---|---|
| Backend services | ASP.NET Core 10, Minimal APIs |
| API gateway | YARP (Yet Another Reverse Proxy) on ASP.NET Core 10 |
| Real-time | SignalR (Graph service) |
| ML service | Django 5.1, Django REST Framework, scikit-learn, Celery + Redis (planned) |
| Frontend | Vue 3 + Vite, vis-network (graph placeholder) |
| Database | PostgreSQL 16 |
| ORM | Entity Framework Core 10 + Npgsql |
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
├── Core/                       # Business API (.NET 10, clean architecture scaffold)
│   └── src/
│       ├── Relativa.Core/                    # Host (Program.cs, health only)
│       ├── Relativa.Core.Application/        # Empty -- .csproj only, no .cs files
│       ├── Relativa.Core.Domain/             # Empty -- .csproj only, no .cs files
│       └── Relativa.Core.Infrastructure/     # RelativaDbContext
├── Graph/                      # SignalR graph service (.NET 10)
│   └── src/Relativa.Graph/
├── Audit/                      # Audit log API (.NET 10)
│   └── src/Relativa.Audit/
├── Migration/                  # EF Core migration runner (.NET 10 console)
│   └── src/Relativa.Migration/
├── Persistence/                # Shared EF Core entity library (no .sln)
│   └── src/Relativa.Persistence/
│       ├── Entities/           # 14 entity classes
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
| `SCALAR-GUIDE.md` | Scalar API docs walkthrough | Current and accurate. |
| `CONTRIBUTORS.md` | Contributor list | -- |
| `Authentication/README.md` | Auth service | **Outdated** -- claims 501 stubs but login/register are implemented. |
| `Core/README.md` | Core service | Partially outdated -- mentions migrations in Core; they now live in Migration. |
| `Gateway/README.md` | Gateway | Partially outdated -- says JWT validation is a stub; it is now fully configured. |
| `Graph/README.md` | Graph service | Accurate (describes stub state). |
| `Audit/README.md` | Audit service | Accurate (describes stub state). |
| `Migration/README.md` | Migration runner | **Outdated** -- describes entrypoint.sh; actual code uses `MigrateAsync`. |
| `ML/README.md` | ML service | Accurate (describes stub state). |
| `Client/README.md` | Vue client | Accurate (Vite scaffold docs). |

Per-service READMEs are written in Ukrainian.
