# Audit Coverage Matrix

> **Last verified:** 2026-05-29 (Added `organization_settings` and `workspace_settings` settings read/update audit events.)

This matrix defines which database tables are currently required to be audited, and whether both prerequisites are present:
- audit table exists
- audit write events exist

| Table | Service owner | Requires audit now | Audit table exists | Audit write events exist | Gap |
|---|---|---:|---:|---:|---:|
| `users` | Authentication | Yes | Yes (`user_audit_log`) | Yes (`user_registered`, `user_provisioned`, `user_profile_updated`, `user_archived`) | No |
| `organizations` | Core | Yes | Yes (`organization_audit_log`) | Yes (`organization_created`, `organization_updated`) | No |
| `workspaces` | Core | Yes | Yes (`workspace_audit_log`) | Yes (`workspace_created`, `workspace_updated`, `workspace_archived`) | No |
| `entity` | Core | Yes | Yes (`entity_audit_log`) | Yes (`entity_created`, `entity_updated`, `entity_archived`) | No |
| `organization_join_requests` | Core | Yes | Yes (`organization_audit_log`) | Yes (`organization_join_request_submitted`, `organization_join_request_reviewed`) | No |
| `organization_invitations` | Core | Yes | Yes (`organization_audit_log`) | Yes (`organization_invitation_created`, `organization_invitation_cancelled`, `organization_invitation_resent`, `organization_invitation_accepted`) | No |
| `workspace_invitations` | — | — | — | **Retired** (table removed; historical `workspace_audit_log` rows may still reference `workspace_invitation_*` / `workspace_member_added_via_invitation`) | — |
| `workspace_join_requests` | — | — | — | **Retired** (table removed; historical rows may reference `workspace_join_request_*` / `workspace_member_added_via_join_request`) | — |
| `user_role_organization` | Core | Yes | Yes (`organization_audit_log`) | Yes (`organization_member_added_via_join_request`, `organization_member_added_via_invitation`, `organization_member_added_via_user_provisioning`, `organization_member_removed`, `organization_member_role_changed`, `organization_member_account_archived`) | No |
| `user_role_workspace` | Core | Yes | Yes (`workspace_audit_log`) | Yes (`workspace_member_added`, `workspace_member_removed`, `workspace_member_role_changed`; legacy only: `workspace_member_added_via_invitation`, `workspace_member_added_via_join_request`) | No |
| `organization_roles` | Core | Yes | Yes (`organization_audit_log`) | Yes (`organization_role_created`, `organization_role_updated`, `organization_role_archived`) | No |
| `workspace_roles` | Core | Yes | Yes (`workspace_audit_log`) | Yes (`workspace_role_created`, `workspace_role_updated`, `workspace_role_archived`) | No |
| `workspace_settings` | Core | Yes | Yes (`workspace_audit_log`) | Yes (`workspace_settings_read`, `workspace_settings_updated`) | No |
| `organization_settings` | Core | Yes | Yes (`organization_audit_log`) | Yes (`organization_settings_read`, `organization_settings_updated`) | No |

## Notes

- Required-now scope includes principal entities, **organization** invitations and join requests, memberships, and role lifecycle flows. Workspace-scoped invitation/join-request **tables are removed**; do not add new code paths that assume those tables exist.
- New entities/tables introduced in future work must be added to this matrix and evaluated before completion.
- `rabbitmq_processed_delivery` (`RabbitMqProcessedDelivery`) is **infrastructure inbox deduplication**, not CRM user-auditable data — it is intentionally absent from this matrix.
- **Entity graph create (Rabbit → Core):** the Core consumer for `EntityGraphCreateRpcV1` calls `EntityService.CreateAsync`, which enqueues the same **`entity_created`** audit path as the HTTP `POST .../entities` handler — no separate matrix row is required.
