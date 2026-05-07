# Audit log HTTP API

> **Last verified:** 2026-05-08 (`organization_member_account_archived` org-scope action for org admin user archive.)

The Audit service (`relativa-audit`, port **8086**) exposes read APIs backed by PostgreSQL. Clients should call through the **Gateway** at `{origin}/audit/...` (YARP strips the `/audit` prefix upstream).

---

## Authentication

- Send **`Authorization: Bearer <JWT>`** — same token as Auth (`Jwt:Issuer`, `Jwt:Audience`, `Jwt:SecretKey` must match the Audit service configuration).
- The Gateway forwards **`X-User-Id`** and **`X-User-Email`** from validated claims; Audit uses JWT **`sub`** when authenticated, or **`X-User-Id`** as a fallback.

---

## RBAC (who may call)

| Query param `entity_type` (audit category) | Required context | Allowed roles |
|---|---|---|
| `entity` | `workspace_id` | `ws_admin`, `ws_analyst` in that workspace |
| `workspace` | `workspace_id` | `ws_admin`, `ws_analyst` |
| `organization` | `organization_id` | `org_owner`, `org_admin` |
| `user` | *(none)* | Caller may see rows where the **target user** is themselves, **or** shares an organization with caller as org owner/admin, **or** shares a workspace with caller where caller is `ws_admin` / `ws_analyst`. Optional `target_user_id` filters to one user (must still be in the visible set). |

Failure → **403** `{ "status":403,"title":"Forbidden","detail":"..." }`.

---

## Endpoints

### `GET /audit-log`

**Query parameters**

| Name | Required | Default | Description |
|---|---|---|---|
| `entity_type` | Yes | — | Audit category: `entity`, `workspace`, `organization`, `user`. Alias: `scope`. |
| `workspace_id` | For `entity` / `workspace` | — | Workspace context for RBAC and filtering. |
| `organization_id` | For `organization` | — | Organization context. |
| `date_from` | No | 30 days ago | Alias: `from`. |
| `date_to` | No | now | Alias: `to`. |
| `action` | No | — | Exact match on `action` column. |
| `index` | No | `1` | 1-based page index. |
| `page_size` | No | `20` | Max `100`. |
| `entity_id` | No | — | Filter entity audit rows. Alias: `targetId` when `entity_type=entity`. |
| `domain_entity_type` | No | — | Filters `entity_audit_log.entity_type` (stores **entity type id** as string from Core). |
| `actor_user_id` | No | — | Filter by actor (`changed_by`). Alias: `actorUserId`. |
| `target_user_id` | No | — | Filter user-scope audit by subject user. |

### `GET /entities/{entityId}/audit-log`

Same query params as above except **`entity_type` is implied** (`entity`) and **`entity_id`** is fixed from the route. **`workspace_id`** is still **required**.

---

## Success response

```json
{
  "data": [ /* AuditLogEntryDto */ ],
  "total": 0,
  "page": 1,
  "perPage": 20,
  "filterContext": {
    "workspace": { "id": 1, "name": "...", "organizationId": 1, "organizationName": "..." }
  }
}
```

`filterContext` is present when workspace or organization context applies.

### `data[]` row (`AuditLogEntryDto`)

- `id` (UUID), `entity_type` (category; JSON name — **not** the CRM type column), `action`, `fieldName`, `changedAt`
- `actor`: `{ userId, email, firstName, lastName }` or null
- `oldValue`, `newValue`: JSON values from jsonb columns
- **One** of the contextual objects is set: `entity`, `workspace`, `organization`, `targetUser` (others null)
- `entityDeleted`: when an entity row was removed but audit remains (FK set null)
- `entityTypeIdFromEvent`: denormalized string from `entity_audit_log.entity_type`
- `propertyDefinitionsForEntityType`: metadata for the entity type (`propertyId`, `name`, `dataType`)
- `propertyChanges`: for property-bag payloads, expanded rows with typed metadata

---

## Errors

Same envelope as Core: `{ "status", "title", "detail" }`.

| Status | When |
|---:|---|
| 400 | FluentValidation / bad arguments |
| 401 | Missing or invalid JWT / identity |
| 403 | RBAC denied |
| 404 | Unknown `workspace_id`, `organization_id`, `entity_id`, or entity not in workspace |
| 500 | Unhandled exception |

---

## Examples (via Gateway)

```http
GET /audit/audit-log?entity_type=entity&workspace_id=1&index=1&page_size=20
Authorization: Bearer <token>
```

```http
GET /audit/entities/42/audit-log?workspace_id=1
Authorization: Bearer <token>
```
