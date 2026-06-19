# Relativa Database Schema Reference

## Architecture Overview

### EAV (Entity-Attribute-Value) Pattern
The core business data uses an EAV schema to support flexible, runtime-configurable entity shapes. Rather than a fixed table per entity type, all "business entities" (clients, deals, contracts, etc.) live in the `entity` table. Their properties are defined in `property` and `entity_type_property`, and their values live in `entity_property_value` — one row per entity+property pair, with typed columns for each supported data type.

### RBAC (Role-Based Access Control)
Two parallel role hierarchies exist:
- **Organization-level**: `organization_roles` → `organization_role_permissions` → `permissions`
- **Workspace-level**: `workspace_roles` → `workspace_role_permissions` → `permissions`

Roles can be system-wide (no `organization_id` / `workspace_id`) or scoped to a specific org/workspace. A **lower `priority` integer means higher authority** (org_owner = 0).

### Soft-Delete Convention
All user-facing records use an `is_archived` boolean instead of hard deletes. The partial unique index on `users.email` (`WHERE is_archived = FALSE`) is the canonical example. The API treats archived records as deleted.

### Audit / Outbox Pattern
All state-changing API operations publish an event to `audit_outbox` (transactional outbox, same DB transaction). A background publisher forwards events to RabbitMQ. A consumer service writes them to the four domain-specific audit log tables. `audit_processed_event` provides idempotency at the consumer side; `rabbitmq_processed_delivery` provides it at the message-broker side.

---

## Tables

### `users`
Stores all user accounts.

| Column | Type | Nullable | Default | Notes |
|--------|------|:--------:|---------|-------|
| `id` | int (PK) | No | auto | |
| `first_name` | varchar | No | — | Max 100 chars |
| `last_name` | varchar | No | — | Max 100 chars |
| `email` | varchar | No | — | Case-insensitive storage |
| `password` | varchar | No | — | bcrypt hash |
| `created_at` | timestamptz | No | — | Set at registration |
| `is_archived` | bool | No | false | Soft-delete flag |
| `password_reset_token` | varchar | Yes | null | One-time token |
| `password_reset_token_expires_at` | timestamptz | Yes | null | Token TTL |

**Constraints & Indexes:**
- `PK(id)`
- Partial unique index on `(email)` `WHERE is_archived = FALSE` — enforces unique live emails only

**Business Rules:**
- Email validated as a properly-formed address before insertion
- `first_name` and `last_name` must not be blank, max 100 chars each
- Password must be at least 8 characters (hashed before storage)
- Archiving a user cascades to their workspace/org memberships (via service layer, not DB cascade)

---

### `organizations`
Top-level tenant boundary.

| Column | Type | Nullable | Default |
|--------|------|:--------:|---------|
| `id` | int (PK) | No | auto |
| `name` | varchar | No | — |
| `is_archived` | bool | No | false |

**Business Rules:**
- `name` required, max 200 chars, not blank
- Creating an organization automatically creates `organization_settings` and assigns the creator `org_owner`

---

### `organization_settings`
One-to-one extension of `organizations` for configurable settings.

| Column | Type | Nullable | Default |
|--------|------|:--------:|---------|
| `id` | int (PK) | No | auto |
| `organization_id` | int (FK → organizations) | No | — |
| `join_policy` | varchar | No | 'open' |
| `description` | varchar | Yes | null |
| `default_org_role_id` | int (FK → organization_roles) | Yes | null |

**Constraints:**
- `UNIQUE(organization_id)` — one settings row per org
- `CHECK(join_policy IN ('open', 'invite_only'))`

**Business Rules:**
- `join_policy = 'open'` → users may submit join requests
- `join_policy = 'invite_only'` → only explicit invitations work
- `description` max 500 chars
- `default_org_role_id` must belong to the same organization (or be a system role)
- Requires `manage_org_settings` permission to update

---

### `workspaces`
A workspace is a scoped collaboration environment inside an organization.

| Column | Type | Nullable | Default |
|--------|------|:--------:|---------|
| `id` | int (PK) | No | auto |
| `name` | varchar | No | — |
| `organization_id` | int (FK → organizations, CASCADE) | No | — |
| `created_by_user_id` | int (FK → users, RESTRICT) | No | — |
| `is_archived` | bool | No | false |

**Business Rules:**
- `name` required, max 200 chars, not blank
- Creator must be an organization member; they are automatically assigned `ws_admin`
- Requires `create_workspaces` org-level permission
- Deleting the organization cascades to workspaces; deleting the creator is restricted

---

### `workspace_settings`
One-to-one extension of `workspaces` for risk-scoring configuration.

| Column | Type | Nullable | Default |
|--------|------|:--------:|---------|
| `id` | int (PK) | No | auto |
| `workspace_id` | int (FK → workspaces) | No | — |
| `description` | varchar | Yes | null |
| `high_risk_threshold` | decimal | No | 0.7 |
| `medium_risk_threshold` | decimal | No | 0.4 |
| `risk_scoring_enabled` | bool | No | true |

**Constraints:**
- `UNIQUE(workspace_id)`
- `CHECK(high_risk_threshold BETWEEN 0 AND 1)`
- `CHECK(medium_risk_threshold BETWEEN 0 AND 1)`
- `CHECK(medium_risk_threshold < high_risk_threshold)`

**Business Rules:**
- Automatically created with defaults when a workspace is created
- Requires `manage_ws_settings` to update

---

### `permissions`
Canonical list of named capabilities. Seeded once; never user-created.

| Column | Type | Nullable | Default |
|--------|------|:--------:|---------|
| `id` | int (PK) | No | auto |
| `name` | varchar | No | — |
| `is_archived` | bool | No | false |

**Constraints:** `UNIQUE(name)`

**Seeded values (16 permissions):**

*Organization-scoped:* `manage_org_settings`, `invite_to_org`, `manage_join_requests`, `remove_org_members`, `assign_org_roles`, `manage_org_roles`, `create_workspaces`, `manage_org_workspace_members`, `create_org_users`, `edit_other_org_users_profile`, `delete_org_users`

*Workspace-scoped:* `manage_ws_settings`, `add_ws_members`, `remove_ws_members`, `assign_ws_roles`, `manage_ws_roles`, `create_entities`, `edit_entities`, `edit_archived_entities`, `delete_entities`, `view_entities`, `view_analytics`, `delete_workspace`

---

### `organization_roles`
Roles that can be assigned to users within an organization.

| Column | Type | Nullable | Default |
|--------|------|:--------:|---------|
| `id` | int (PK) | No | auto |
| `name` | varchar | No | — |
| `organization_id` | int (FK → organizations, CASCADE) | Yes | null |
| `priority` | int | No | — |
| `is_archived` | bool | No | false |

**Constraints:** `UNIQUE(name, organization_id)`

**Business Rules:**
- `organization_id IS NULL` → system role (shared across all orgs)
- `priority = 0` is reserved for `org_owner` (cannot be assigned to custom roles)
- Custom roles must have `priority >= 1`
- System roles cannot be modified or deleted
- Requires `manage_org_roles` permission to create/update/archive

**Seeded system roles:**

| Name | Priority | Permissions |
|------|:---:|---|
| `org_owner` | 0 | All org permissions |
| `org_admin` | 1 | All except `manage_org_roles`, `delete_org_users` |
| `org_member` | 2 | None |

---

### `workspace_roles`
Roles assignable to users within a workspace.

| Column | Type | Nullable | Default |
|--------|------|:--------:|---------|
| `id` | int (PK) | No | auto |
| `name` | varchar | No | — |
| `workspace_id` | int (FK → workspaces, CASCADE) | Yes | null |
| `priority` | int | No | — |
| `is_archived` | bool | No | false |

**Constraints:** `UNIQUE(name, workspace_id)`

**Business Rules:**
- `workspace_id IS NULL` → system role
- System roles cannot be modified or deleted
- Requires `manage_ws_roles` permission to create/update/archive
- Cannot demote the last workspace admin (last-admin guard enforced in service)

**Seeded system roles:**

| Name | Priority | Key Permissions |
|------|:---:|---|
| `ws_admin` | 0 | All workspace permissions |
| `ws_manager` | 1 | add/remove members, entities CRUD, analytics |
| `ws_analyst` | 2 | view entities, view analytics |
| `ws_member` | 3 | view entities |

---

### `organization_role_permissions`
Join table linking organization roles to permissions.

| Column | Type | Nullable |
|--------|------|:--------:|
| `id` | int (PK) | No |
| `org_role_id` | int (FK → organization_roles, CASCADE) | No |
| `permission_id` | int (FK → permissions, CASCADE) | No |

---

### `workspace_role_permissions`
Join table linking workspace roles to permissions.

| Column | Type | Nullable |
|--------|------|:--------:|
| `id` | int (PK) | No |
| `ws_role_id` | int (FK → workspace_roles, CASCADE) | No |
| `permission_id` | int (FK → permissions, CASCADE) | No |

---

### `user_role_organization`
Membership of a user in an organization with an assigned role.

| Column | Type | Nullable | Default |
|--------|------|:--------:|---------|
| `id` | int (PK) | No | auto |
| `user_id` | int (FK → users, CASCADE) | No | — |
| `organization_id` | int (FK → organizations, CASCADE) | No | — |
| `org_role_id` | int (FK → organization_roles) | No | — |
| `joined_at` | timestamptz | No | — |
| `is_archived` | bool | No | false |

**Constraints:**
- `UNIQUE(user_id, organization_id)` — one membership per user per org
- Index on `(organization_id, is_archived)` for member list queries
- Index on `(user_id)` for "my organizations" queries

**Business Rules:**
- A user can be a member of multiple organizations
- Role must belong to the organization or be a system role
- Assigning non-default roles requires `assign_org_roles` permission
- Role authority is compared by `priority` (lower = higher authority) when removing members
- Cannot remove a member whose role has equal or higher authority than the actor's role

---

### `user_role_workspace`
Membership of a user in a workspace with an assigned role.

| Column | Type | Nullable | Default |
|--------|------|:--------:|---------|
| `id` | int (PK) | No | auto |
| `user_id` | int (FK → users) | No | — |
| `workspace_id` | int (FK → workspaces, CASCADE) | No | — |
| `ws_role_id` | int (FK → workspace_roles) | No | — |
| `joined_at` | timestamptz | No | — |
| `is_archived` | bool | No | false |

**Constraints:**
- `UNIQUE(user_id, workspace_id)`
- Index on `(workspace_id, is_archived)`

**Business Rules:**
- Target user must already be an organization member
- Duplicate memberships rejected
- Requires `add_ws_members` or `manage_org_workspace_members`
- Role reassignment requires `assign_ws_roles`
- **Last-admin guard**: cannot demote the last user with full workspace authority

---

### `organization_invitations`
Pending or resolved invitations sent to external email addresses.

| Column | Type | Nullable | Default |
|--------|------|:--------:|---------|
| `id` | int (PK) | No | auto |
| `organization_id` | int (FK → organizations) | No | — |
| `email` | varchar | No | — |
| `org_role_id` | int (FK → organization_roles) | No | — |
| `invited_by_user_id` | int (FK → users) | No | — |
| `token` | varchar | No | — |
| `status` | varchar | No | — |
| `created_at` | timestamptz | No | — |
| `expires_at` | timestamptz | No | — |

**Constraints:**
- `UNIQUE(token)`
- Index on `(organization_id, status)`
- Index on `(email, status)`
- `CHECK(status IN ('Pending', 'Accepted', 'Cancelled', 'Expired'))`

**Business Rules:**
- `expires_at` = `created_at + 7 days`
- Token is a GUID string, unique globally
- Cannot invite an existing member or email with a Pending invitation already
- Accepting: email must match (case-insensitive), must be Pending and not expired
- Requires `invite_to_org` permission to create/cancel/resend
- Non-default role assignment requires `assign_org_roles`

---

### `organization_join_requests`
Requests submitted by users to join an organization that has `join_policy = 'open'`.

| Column | Type | Nullable | Default |
|--------|------|:--------:|---------|
| `id` | int (PK) | No | auto |
| `user_id` | int (FK → users) | No | — |
| `organization_id` | int (FK → organizations) | No | — |
| `message` | text | Yes | null |
| `status` | varchar | No | 'Pending' |
| `created_at` | timestamptz | No | — |
| `reviewed_by_user_id` | int (FK → users) | Yes | null |
| `reviewed_at` | timestamptz | Yes | null |

**Constraints:**
- Index on `(organization_id, status)`
- Index on `(user_id, status)`
- `CHECK(status IN ('Pending', 'Approved', 'Rejected'))`

**Business Rules:**
- Cannot submit if `join_policy = 'invite_only'`
- Cannot submit if user is already a member or has a Pending request for the same org
- Review requires `manage_join_requests` permission
- On approval: user is assigned `default_org_role_id` from org settings (or system `org_member`)
- `reviewed_by_user_id` and `reviewed_at` set on review

---

### `entity_type`
Defines the types of business entities in the system. System-managed.

| Column | Type | Nullable | Default |
|--------|------|:--------:|---------|
| `id` | int (PK) | No | auto |
| `name` | varchar | No | — |
| `is_standalone` | bool | No | true |

**Constraints:** `UNIQUE(name)`

**Business Rules:**
- `is_standalone = FALSE` means entities of this type require a parent relationship to exist
- Non-standalone types: `deal_analysis`, `contract`, `note`
- Not user-creatable; only modified by migrations

---

### `entity`
Every business record (client, deal, contract, contact, task, note, deal_analysis) is a row here.

| Column | Type | Nullable | Default |
|--------|------|:--------:|---------|
| `id` | int (PK) | No | auto |
| `entity_type_id` | int (FK → entity_type, RESTRICT) | No | — |
| `created_by_user_id` | int (FK → users, RESTRICT) | No | — |
| `is_archived` | bool | No | false |

**Indexes:**
- `ix_entity_created_by_user` on `(created_by_user_id)`

**Business Rules:**
- Every entity must belong to ≥ 1 workspace (via `entity_workspace`)
- `created_by_user_id` is set to the requesting user on creation; used for role-priority access checks
- Visibility: a user can see entities they created OR entities created by users with strictly lower authority (higher priority number) in the same workspace. Org owners bypass this filter.
- Archived entities can only be edited with the `edit_archived_entities` permission
- Cannot delete entities of a type where all properties are `is_readonly = TRUE`

---

### `entity_workspace`
Scopes an entity to a workspace. An entity may appear in multiple workspaces.

| Column | Type | Nullable |
|--------|------|:--------:|
| `id` | int (PK) | No |
| `entity_id` | int (FK → entity, CASCADE) | No |
| `workspace_id` | int (FK → workspaces, CASCADE) | No |

**Constraints:** `UNIQUE(entity_id, workspace_id)`

**Business Rules:**
- Every entity must have at least one workspace link; entities with zero workspace links are inaccessible
- Cascade on both sides ensures cleanup on entity/workspace deletion

---

### `property`
Defines a named attribute that can be assigned to one or more entity types.

| Column | Type | Nullable | Default |
|--------|------|:--------:|---------|
| `id` | int (PK) | No | auto |
| `name` | varchar | No | — |
| `data_type` | varchar | No | — |
| `is_readonly` | bool | No | false |
| `organization_id` | int (FK → organizations, CASCADE) | Yes | null |

**Constraints:**
- `CHECK(data_type IN ('String', 'Int', 'Decimal', 'Bool', 'Date'))`

**Business Rules:**
- `organization_id IS NULL` → global system property
- `organization_id IS NOT NULL` → org-scoped custom property
- `is_readonly = TRUE` → value can only be written by the system (ML pipeline); rejected in Create/Update API calls. Filters/sorts on readonly properties are silently dropped for users without `view_analytics`.
- Properties with `AllowedValues` enforce an enum constraint on `value_string`

---

### `entity_type_property`
Join table binding properties to entity types with optional required flag.

| Column | Type | Nullable | Default |
|--------|------|:--------:|---------|
| `entity_type_id` | int (PK, FK → entity_type, CASCADE) | No | — |
| `property_id` | int (PK, FK → property, CASCADE) | No | — |
| `is_required` | bool | No | false |

**Business Rules:**
- `is_required = TRUE` → entity creation must supply a non-null, non-blank value for this property
- A property must belong to an entity type's definition to be submitted in Create/Update requests

---

### `entity_property_value`
Stores the actual value of one property for one entity. One row per (entity, property) pair.

| Column | Type | Nullable |
|--------|------|:--------:|
| `entity_id` | int (PK, FK → entity, CASCADE) | No |
| `property_id` | int (PK, FK → property, CASCADE) | No |
| `value_string` | varchar | Yes |
| `value_int` | int | Yes |
| `value_decimal` | decimal | Yes |
| `value_bool` | bool | Yes |
| `value_date` | date | Yes |

**Business Rules — Typed column rule:** Only the column matching the property's `data_type` may be non-NULL:
- `String` → `value_string`; all others must be NULL
- `Int` → `value_int`; all others must be NULL
- `Decimal` → `value_decimal`; all others must be NULL
- `Bool` → `value_bool`; all others must be NULL
- `Date` → `value_date`; all others must be NULL

**Allowed-value validation:** For String properties with rows in `property_allowed_value`, the `value_string` must match one of the allowed values (case-insensitive).

---

### `property_allowed_value`
Enum constraints for String properties.

| Column | Type | Nullable |
|--------|------|:--------:|
| `property_id` | int (PK, FK → property, CASCADE) | No |
| `value` | varchar (PK) | No |

**Business Rules:**
- Presence of any row for a `property_id` activates allowed-value validation on `entity_property_value`
- Matching is case-insensitive (OrdinalIgnoreCase in application code)

---

### `entity_relationship_type`
Defines a named relationship between two entity types with cardinality and required rules.

| Column | Type | Nullable | Default |
|--------|------|:--------:|---------|
| `id` | int (PK) | No | auto |
| `name` | varchar | No | — |
| `source_entity_type_id` | int (FK → entity_type, RESTRICT) | No | — |
| `target_entity_type_id` | int (FK → entity_type, RESTRICT) | No | — |
| `is_required` | bool | No | false |
| `relationship_cardinality` | varchar(32) | No | 'many_to_one' |

**Constraints:**
- `UNIQUE(name)`
- `CHECK(relationship_cardinality IN ('many_to_one', 'one_to_many', 'one_to_one', 'many_to_many'))`

**Seeded relationship types:**

| Name | Source → Target | Cardinality | is_required |
|------|---|---|:---:|
| `deal_client` | deal → client | many_to_one | No |
| `deal_analysis` | deal → deal_analysis | many_to_one | No |
| `contract_deal` | contract → deal | many_to_one | **Yes** |
| `client_contact` | client → contact | one_to_many | No |
| `deal_contact` | deal → contact | many_to_many | No |
| `deal_task` | deal → task | one_to_many | No |
| `client_task` | client → task | one_to_many | No |
| `deal_note` | deal → note | one_to_many | No |
| `client_note` | client → note | one_to_many | No |

---

### `entity_relationship`
An instance of a relationship between two entity records.

| Column | Type | Nullable |
|--------|------|:--------:|
| `id` | int (PK) | No |
| `source_entity_id` | int (FK → entity, CASCADE) | No |
| `target_entity_id` | int (FK → entity, RESTRICT) | No |
| `relationship_type_id` | int (FK → entity_relationship_type, RESTRICT) | No |

**Indexes:**
- `ix_entity_relationship_source` on `(source_entity_id)`
- `ix_entity_relationship_type` on `(relationship_type_id)`

**Business Rules:**
- `many_to_one` / `one_to_one`: source may have at most 1 outgoing link of this type (enforced in service)
- `one_to_one`: additionally, target may have at most 1 incoming link of this type
- `is_required = TRUE` on the type: source entity must always have ≥ 1 link of this type; the last link cannot be deleted
- Cannot create a relationship if source or target is archived
- Cannot create a relationship to/from an entity whose all properties are readonly (deal_analysis guard)
- Source entity is deleted → its outgoing relationships CASCADE delete
- Target entity deletion is RESTRICTED while it has incoming relationships

---

### `entity_audit_log`
Append-only record of all entity CRUD events.

| Column | Type | Nullable |
|--------|------|:--------:|
| `id` | uuid (PK) | No |
| `action` | varchar(50) | No |
| `changed_by_id` | int (FK → users) | Yes |
| `entity_id` | int (FK → entity) | Yes |
| `entity_type` | varchar(50) | No |
| `field_name` | varchar | Yes |
| `old_value` | jsonb | Yes |
| `new_value` | jsonb | Yes |
| `changed_at` | timestamptz | No |

**Indexes:**
- `ix_entity_audit_log_changed_at` DESC
- `ix_eal_entity_changed_at` on `(entity_id, changed_at DESC)`
- `ix_eal_changed_by_changed_at` on `(changed_by_id, changed_at DESC)`

**Populated for actions:** `entity_created`, `entity_updated`, `entity_archived`, `relationship_reassigned`

---

### `organization_audit_log`
Append-only record of all organization-scoped events.

| Column | Type | Nullable |
|--------|------|:--------:|
| `id` | uuid (PK) | No |
| `action` | varchar(50) | No |
| `changed_by_id` | int (FK → users) | Yes |
| `organization_id` | int (FK → organizations) | Yes |
| `field_name` | varchar | Yes |
| `old_value` | jsonb | Yes |
| `new_value` | jsonb | Yes |
| `changed_at` | timestamptz | No |

**Populated for actions:** `organization_created`, `organization_updated`, `organization_settings_updated`, `organization_member_added`, `organization_member_removed`, `organization_member_role_changed`, `organization_role_created`, `organization_role_updated`, `organization_role_archived`, `organization_invitation_created`, `organization_invitation_cancelled`, `organization_invitation_resent`, `organization_invitation_accepted`, `organization_join_request_submitted`, `organization_join_request_reviewed`

---

### `workspace_audit_log`
Append-only record of all workspace-scoped events.

| Column | Type | Nullable |
|--------|------|:--------:|
| `id` | uuid (PK) | No |
| `action` | varchar(50) | No |
| `changed_by_id` | int (FK → users) | Yes |
| `workspace_id` | int (FK → workspaces) | Yes |
| `field_name` | varchar | Yes |
| `old_value` | jsonb | Yes |
| `new_value` | jsonb | Yes |
| `changed_at` | timestamptz | No |

**Populated for actions:** `workspace_created`, `workspace_updated`, `workspace_archived`, `workspace_settings_updated`, `workspace_member_added`, `workspace_member_removed`, `workspace_member_role_changed`, `workspace_role_created`, `workspace_role_updated`, `workspace_role_archived`

---

### `user_audit_log`
Append-only record of all user-scoped events.

| Column | Type | Nullable |
|--------|------|:--------:|
| `id` | uuid (PK) | No |
| `action` | varchar(50) | No |
| `changed_by_id` | int (FK → users) | Yes |
| `target_user_id` | int (FK → users) | Yes |
| `field_name` | varchar | Yes |
| `old_value` | jsonb | Yes |
| `new_value` | jsonb | Yes |
| `changed_at` | timestamptz | No |

**Populated for actions:** `user_registered`, `user_profile_updated`, `user_password_changed`, `user_password_reset_requested`, `user_password_reset_completed`

---

### `audit_outbox`
Transactional outbox for audit events awaiting publication to RabbitMQ.

| Column | Type | Nullable | Default |
|--------|------|:--------:|---------|
| `id` | bigint (PK) | No | auto-increment |
| `event_id` | uuid | No | — |
| `payload_json` | jsonb | No | — |
| `routing_key` | varchar(512) | No | — |
| `occurred_at_utc` | timestamptz | No | — |
| `created_at_utc` | timestamptz | No | — |
| `published_at_utc` | timestamptz | Yes | null |
| `publish_attempts` | int | No | 0 |
| `last_error` | varchar(2000) | Yes | null |

**Constraints:**
- `UNIQUE(event_id)`
- Index `ix_audit_outbox_pending` on `(published_at_utc, id)` — used by polling publisher
- Index `ix_audit_outbox_created_at_utc`

**Business Rules:**
- Written in the same DB transaction as the business operation
- `published_at_utc IS NULL` → awaiting publication
- `publish_attempts > 10` with `published_at_utc IS NULL` → permanently failed; cleaned up by maintenance migration

---

### `audit_processed_event`
Idempotency tracker for the audit consumer. Prevents duplicate writes to audit log tables.

| Column | Type | Nullable |
|--------|------|:--------:|
| `event_id` | uuid (PK) | No |
| `processed_at_utc` | timestamptz | No |

---

### `rabbitmq_processed_delivery`
Idempotency tracker at the RabbitMQ consumer level. Prevents re-processing if a message is redelivered.

| Column | Type | Nullable |
|--------|------|:--------:|
| `message_id` | varchar (PK part) | No |
| `consumer_group` | varchar (PK part) | No |
| `processed_at_utc` | timestamptz | No |

**Constraints:** Composite PK on `(message_id, consumer_group)`

---

## Entity Type Reference

### `client`
Represents a B2B company or account that can be the target of sales deals.

**`is_standalone = TRUE`** — can exist independently.

| Property | Data Type | Required | Readonly | Allowed Values |
|----------|-----------|:--------:|:--------:|----------------|
| `company_name` | String | No | No | — |
| `industry` | String | No | No | technology, finance, healthcare, retail, manufacturing, energy, education, other |
| `website` | String | No | No | — |
| `annual_revenue` | Decimal | No | No | — |
| `employee_count` | String | No | No | 1-10, 11-50, 51-200, 201-1000, 1000+ |
| `client_status` | String | No | No | lead, prospect, active, at_risk, churned |
| `email` | String | No | No | — |
| `country` | String | No | No | — |
| `first_name` | String | No | No | — |
| `last_name` | String | No | No | — |
| `phone_number` | String | No | No | — |
| `city` | String | No | No | — |
| `client_lifetime_value` | Decimal | No | **Yes** | — (ML-computed) |
| `client_tenure_days` | Int | No | **Yes** | — (ML-computed) |

**Outgoing relationships:** `deal_client` (as target), `client_contact`, `client_task`, `client_note`
**Incoming relationships:** `deal_client` (deals point to this client)

---

### `deal`
Represents a sales opportunity with lifecycle tracking.

**`is_standalone = TRUE`** — can exist independently.

| Property | Data Type | Required | Readonly | Allowed Values |
|----------|-----------|:--------:|:--------:|----------------|
| `created_at` | Date | **Yes** | No | — |
| `status` | String | **Yes** | No | opened, pending, closed, revoked |
| `deal_value` | Decimal | No | No | — |
| `title` | String | No | No | — |
| `deal_stage` | String | No | No | Prospecting, Qualification, Proposal, Negotiation |
| `deal_source` | String | No | No | cold_outreach, referral, website, event, partner |
| `priority` | String | No | No | high, medium, low |
| `expected_close` | Date | No | No | — |

**Outgoing relationships:** `deal_client` (many_to_one), `deal_analysis` (many_to_one), `deal_contact` (many_to_many), `deal_task` (one_to_many), `deal_note` (one_to_many)
**Incoming relationships:** `contract_deal` (contracts point to this deal)

---

### `deal_analysis`
ML feature vector computed for a deal. System-managed, not user-creatable via API.

**`is_standalone = FALSE`** — must be the target of a `deal_analysis` relationship from a deal.

| Property | Data Type | Required | Readonly | Notes |
|----------|-----------|:--------:|:--------:|-------|
| `days_since_created` | Int | **Yes** | No | Age of the deal in days |
| `stage_encoded` | Int | **Yes** | No | Numeric encoding of deal stage (0=none, 1=Prospecting, 2=Qualification, 3=Proposal, 4=Negotiation) |
| `num_interactions` | Int | **Yes** | No | Count of tracked interactions |
| `days_since_last_contact` | Int | **Yes** | No | Recency signal |
| `num_open_deals` | Int | **Yes** | No | Open deal count for the client |
| `avg_deal_value` | Decimal | **Yes** | No | Average deal value for the client |
| `source_updated_at` | Date | **Yes** | No | When source data was last refreshed |
| `calculated_at` | Date | **Yes** | No | When ML scores were computed |
| `days_until_expected_close` | Int | No | **Yes** | Computed from expected_close |
| `historical_close_rate` | Decimal | No | **Yes** | Historical win rate for this client |

**Incoming relationships:** `deal_analysis` (from a deal — required)

---

### `contract`
Formal agreement attached to a deal.

**`is_standalone = FALSE`** — must have exactly 1 outgoing `contract_deal` link (many_to_one, `is_required = TRUE`).

| Property | Data Type | Required | Readonly | Allowed Values |
|----------|-----------|:--------:|:--------:|----------------|
| `contract_number` | String | **Yes** | No | — |
| `start_date` | Date | **Yes** | No | — |
| `end_date` | Date | **Yes** | No | — |
| `amount` | Decimal | **Yes** | No | — |
| `currency` | String | **Yes** | No | — |
| `signed_at` | Date | **Yes** | No | — |
| `contract_status` | String | **Yes** | No | active, revoked |
| `contract_type` | String | No | No | subscription, one_time, retainer |

**Outgoing relationships:** `contract_deal` (many_to_one, is_required=TRUE → must always have a linked deal)

---

### `contact`
An individual person associated with a client company.

**`is_standalone = TRUE`**

| Property | Data Type | Required | Readonly |
|----------|-----------|:--------:|:--------:|
| `first_name` | String | **Yes** | No |
| `last_name` | String | **Yes** | No |
| `email` | String | No | No |
| `phone_number` | String | No | No |
| `city` | String | No | No |
| `country` | String | No | No |
| `job_title` | String | No | No |
| `department` | String | No | No |

**Incoming relationships:** `client_contact` (from client), `deal_contact` (from deal)

---

### `task`
An actionable item linked to a deal or client.

**`is_standalone = TRUE`**

| Property | Data Type | Required | Readonly | Allowed Values |
|----------|-----------|:--------:|:--------:|----------------|
| `task_title` | String | **Yes** | No | — |
| `task_status` | String | **Yes** | No | todo, in_progress, done, cancelled |
| `task_priority` | String | No | No | high, medium, low |
| `task_type` | String | No | No | call, meeting, email, follow_up, demo |
| `due_date` | Date | No | No | — |

**Incoming relationships:** `deal_task` (from deal), `client_task` (from client)

---

### `note`
A free-text observation or meeting summary attached to a deal or client.

**`is_standalone = FALSE`** — must be the target of ≥ 1 `deal_note` or `client_note` relationship.

| Property | Data Type | Required | Readonly |
|----------|-----------|:--------:|:--------:|
| `note_content` | String | **Yes** | No |
| `note_date` | Date | **Yes** | No |

**Incoming relationships:** `deal_note` (from deal), `client_note` (from client)

---

## CRUD Rules Summary

### User
| Operation | Rules |
|-----------|-------|
| **Create** | Email required + valid format; first/last name required (max 100); password min 8 chars; email unique among non-archived users |
| **Read** | Users can read their own profile; `edit_other_org_users_profile` for others |
| **Update** | First/last name updatable; email requires re-validation; `edit_other_org_users_profile` to edit others |
| **Delete** | Soft delete (`is_archived = TRUE`); organization memberships cascaded via service |

### Organization
| Operation | Rules |
|-----------|-------|
| **Create** | Name required (max 200, non-blank); creator auto-assigned `org_owner`; `organization_settings` created with defaults |
| **Read** | User must be an org member |
| **Update** | Name required (max 200); requires `manage_org_settings` |
| **Delete** | Soft delete; cascades to workspaces |

### Workspace
| Operation | Rules |
|-----------|-------|
| **Create** | Name required (max 200); org must exist; creator must be org member; requires `create_workspaces`; creator auto-assigned `ws_admin`; `workspace_settings` created with defaults |
| **Read** | User must be workspace member |
| **Update** | Name required (max 200); requires `manage_ws_settings` |
| **Delete** | Soft delete; requires `delete_workspace` OR `ws_admin` fallback OR org owner |

### Entity
| Operation | Rules |
|-----------|-------|
| **Create** | `entity_type_id` must exist; requires `create_entities`; all required properties must be provided; readonly properties rejected; allowed values enforced; required relationships must be supplied; cardinality checked; non-all-readonly type required |
| **Read** | Requires `view_entities`; visibility filtered by role priority (creator or higher-authority users only; org owners bypass); readonly filter/sort dropped without `view_analytics` |
| **Update** | Requires `edit_entities`; archived entities additionally require `edit_archived_entities`; unknown/duplicate property IDs rejected; readonly values must be preserved |
| **Delete** | Soft delete (archive); requires `delete_entities`; not allowed if all properties are readonly |

### Organization Role
| Operation | Rules |
|-----------|-------|
| **Create** | Name max 100; ≥ 1 permission required; priority ≥ 1; requires `manage_org_roles` |
| **Read** | Org members can list roles |
| **Update** | System roles immutable; requires `manage_org_roles` |
| **Delete** | Only custom roles; soft delete; requires `manage_org_roles` |

### Workspace Role
| Operation | Rules |
|-----------|-------|
| **Create** | Name max 100; ≥ 1 permission required; requires `manage_ws_roles` |
| **Update** | System roles immutable; requires `manage_ws_roles` |
| **Delete** | Only custom roles; last-admin guard applies; requires `manage_ws_roles` |

### Organization Invitation
| Operation | Rules |
|-----------|-------|
| **Create** | Email valid; no existing member/pending invite; non-default role requires `assign_org_roles`; requires `invite_to_org`; expiry = NOW + 7 days |
| **Cancel** | Pending only; requires `invite_to_org` |
| **Resend** | Pending only; new token + new expiry; requires `invite_to_org` |
| **Accept** | Token valid; Pending and not expired; email matches; user not already member |

### Organization Join Request
| Operation | Rules |
|-----------|-------|
| **Create** | Org must be `join_policy = 'open'`; no existing membership or Pending request |
| **Read** | User can see own requests; `manage_join_requests` to see org's pending list |
| **Review** | Requires `manage_join_requests`; Approved → membership created; decision immutable |

---

## Permission Matrix

### Organization-Level Roles

| Permission | org_owner | org_admin | org_member |
|------------|:---------:|:---------:|:----------:|
| manage_org_settings | ✓ | ✓ | — |
| invite_to_org | ✓ | ✓ | — |
| manage_join_requests | ✓ | ✓ | — |
| remove_org_members | ✓ | ✓ | — |
| assign_org_roles | ✓ | ✓ | — |
| manage_org_roles | ✓ | — | — |
| create_workspaces | ✓ | ✓ | — |
| manage_org_workspace_members | ✓ | ✓ | — |
| create_org_users | ✓ | ✓ | — |
| edit_other_org_users_profile | ✓ | ✓ | — |
| delete_org_users | ✓ | — | — |

### Workspace-Level Roles

| Permission | ws_admin | ws_manager | ws_analyst | ws_member |
|------------|:--------:|:----------:|:----------:|:---------:|
| manage_ws_settings | ✓ | — | — | — |
| add_ws_members | ✓ | ✓ | — | — |
| remove_ws_members | ✓ | ✓ | — | — |
| assign_ws_roles | ✓ | — | — | — |
| manage_ws_roles | ✓ | — | — | — |
| create_entities | ✓ | ✓ | — | — |
| edit_entities | ✓ | ✓ | — | — |
| edit_archived_entities | ✓ | — | — | — |
| delete_entities | ✓ | ✓ | — | — |
| view_entities | ✓ | ✓ | ✓ | ✓ |
| view_analytics | ✓ | ✓ | ✓ | — |
| delete_workspace | ✓ | — | — | — |
