# Project Status -- What is Done and What is Not

> **Last verified:** 2026-04-13

> **Maintenance obligation:** If you implement a feature that was listed as stub or TODO, move it to the "Implemented" section. If you introduce a new known issue or break something, add it to "Known Issues." Always update the "Last verified" date. See [AI-GUIDES-INDEX.md](../../AI-GUIDES-INDEX.md) for the full update matrix.

---

## Status Summary

| Service | Status | One-line summary |
|---|---|---|
| Gateway | **Functional** | YARP routing, JWT validation, health, Scalar -- all working |
| Authentication | **Functional** | Login, register, JWT with role/permission claims, FluentValidation -- all working |
| Core | **Stub** | Infrastructure only; Domain and Application layers are empty |
| Graph | **Stub** | SignalR hub exists but has no logic |
| Audit | **Stub** | Returns empty array; JWT validation disabled |
| Migration | **Functional** | Applies EF migrations on startup; schema + seed data work |
| ML | **Stub** | Single endpoint returns hardcoded stub |
| Client | **Scaffold** | Vue 3 project with routing and graph placeholder |
| Persistence | **Functional** | Full entity model (14 entities), fluent configs, ModelBuilderExtensions |

---

## Implemented (working)

### Authentication service

- `POST /api/v1/auth/register` -- creates user, hashes password with bcrypt, assigns role (default from config if none specified), returns user DTO with 201 + Location header.
- `POST /api/v1/auth/login` -- validates credentials, issues JWT with claims: `sub`, `email`, `role`, `permissions`.
- Full clean-architecture layers: Domain interfaces, Application service + DTOs + FluentValidation validators, Infrastructure (AuthDbContext, UserRepository, RoleRepository, JwtTokenService, BcryptPasswordHasher).
- GlobalExceptionHandler maps ValidationException -> 400, UnauthorizedAccessException -> 401, duplicate email -> 409.
- EF Core health check at `/health`.
- OpenAPI + Scalar docs.

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
- `20260412140114_InitSeedData.cs` -- seeds roles (`admin`, `sales_manager`, `analyst`), permissions (`can_edit_deals`, `can_view_analytics`, etc.), organizations, workspaces, demo entity types (`client`, `deal`), sample entities with property values.
- Docker Compose runs this before auth and core start.

### Persistence library

- 14 entity classes with EF Fluent API configurations.
- `ModelBuilderExtensions.ApplyAuthEntityConfigurations()` for auth-only subset.
- `ModelBuilderExtensions.ApplyAllEntityConfigurations()` for full model.
- Referenced by Core, Authentication, and Migration via ProjectReference.

### Docker Compose

- Full 10-service stack with dependency ordering.
- Single bridge network `relativa_net`.
- Named volume `postgres_data` for persistent DB.
- Environment variable injection for DB, JWT, and YARP config.
- Migration runs to completion before dependent services start.

### Client

- Vue 3 + Vite scaffold.
- Routing configured.
- `GraphView.vue` with vis-network placeholder.
- Reads `VITE_GATEWAY_URL` from environment.

---

## Stubs / Partially Implemented

### Core service (THE MAIN GAP)

**What exists:**
- `RelativaDbContext` with full entity model registered.
- Serilog, GlobalExceptionHandler, CORS, OpenAPI + Scalar, health check.
- Clean-architecture project structure: `Relativa.Core.Domain` and `Relativa.Core.Application` .csproj files exist.

**What is missing:**
- `Relativa.Core.Domain/` has **zero .cs files** -- no interfaces, no domain logic.
- `Relativa.Core.Application/` has **zero .cs files** -- no use cases, no DTOs, no validators, no services.
- **No business endpoints** -- only `/health` exists.
- No CRUD for entities, deals, users, or workspaces.
- No workspace isolation logic.
- No business rules (BP-01 through BP-06).
- No domain events.

### Graph service

**What exists:** SignalR hub mapped at `/hubs/graph`, service identity endpoint.
**What is missing:** `OnConnectedAsync` only calls `base`. No graph data queries, no recursive CTE queries, no RBAC filtering, no live update logic, no ML score integration.

### Audit service

**What exists:** `/audit-log` endpoint with `AuditReaders` authorization policy (requires Admin or Analyst role).
**What is missing:** Endpoint returns empty array `[]`. JWT signature validation is deliberately disabled (all checks set to `false`). No actual audit event storage or ingestion. No domain event consumer.

### ML service

**What exists:** `POST /api/ml/recalculate/` endpoint.
**What is missing:** Returns `{"status":"accepted","detail":"stub"}`. No ML models (closure_score, churn_score). No Celery tasks defined. No Redis broker in Docker Compose. Beat schedule is commented out in `settings.py`.

### Client

**What exists:** Vue 3 scaffold with routing, graph view placeholder.
**What is missing:** No actual pages beyond placeholders. No authentication flow (login/register forms). No deal/client management UI. No dashboard. D3 integration noted as "for later."

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

---

## Roadmap (from READMEs and code comments)

### Core service -- business API

- CRUD endpoints for entities, users, deals, workspaces.
- Workspace isolation (multi-tenant data scoping).
- Business rules BP-01 through BP-06 (referenced in `Core/README.md`, not defined in code).
- Domain events published to Audit after entity mutations.

### Authentication

- Token refresh flow (`POST /refresh`).
- Token blacklisting.
- `Jwt:RefreshTokenDays` is configured (7 days) but not used.

### Gateway

- Full RBAC middleware (beyond basic JWT validation).
- Per-route permission checks based on JWT `permissions` claim.

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

- Login and register forms wired to Gateway auth endpoints.
- Deal/client management pages.
- Dashboard with analytics.
- D3-based graph visualization (replacing vis-network placeholder).

### Infrastructure

- CI/CD pipeline (GitHub Actions or similar).
- Test projects (at minimum: unit tests for Application services, integration tests for API endpoints).
- Production-ready CORS configuration.
- TLS/HTTPS for production deployment.
