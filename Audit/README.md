# relativa-audit

**ASP.NET Core 10** service: consumes RabbitMQ audit events and exposes **read APIs** for audit history. The write path is the single persistence target for `*_audit_log` tables in PostgreSQL. **Solution:** `Relativa.Audit.sln` — **Web project:** `src/Relativa.Audit/`.

## Port

- **8086** (e.g. `http://localhost:8086`). Through the **Gateway** use `/audit/...` (prefix stripped upstream).

## Configuration

- `ConnectionStrings:Default` — same PostgreSQL database as Core (shared schema).
- `Jwt:SecretKey`, `Jwt:Issuer`, `Jwt:Audience` — must match Authentication / Gateway.
- `RabbitMqAudit:*` — exchange, queue, routing for `audit.#`.
- `AuditLogRead:DefaultDateRangeDays` — default lookback when `date_from` / `date_to` omitted (default `30`).

## API (summary)

- `GET /health` — liveness.
- `GET /audit-log` — paginated audit with filters, RBAC, enriched DTOs. Requires **Bearer JWT** (policy `AuditReaders`).
- `GET /entities/{entityId}/audit-log` — same as entity-scoped listing; **`workspace_id`** query is required.

Full contract: [docs/ai-guides/AUDIT-LOG-API.md](../docs/ai-guides/AUDIT-LOG-API.md).

## Build & run

```bash
cd Audit
dotnet restore Relativa.Audit.sln
dotnet build Relativa.Audit.sln
dotnet run --project src/Relativa.Audit
```

## Architecture

Other services publish domain audit payloads via RabbitMQ; this host persists them with idempotency (`audit_processed_event`). Clients use only the **gateway** for HTTP.
