# Audit Coverage Matrix

> **Last verified:** 2026-05-01

This matrix defines which database tables are currently required to be audited, and whether both prerequisites are present:
- audit table exists
- audit write events exist

| Table | Service owner | Requires audit now | Audit table exists | Audit write events exist | Gap |
|---|---|---:|---:|---:|---:|
| `users` | Authentication | Yes | Yes (`user_audit_log`) | Yes (`user_registered`) | No |
| `organizations` | Core | Yes | Yes (`organization_audit_log`) | Yes (`organization_created`, `organization_updated`) | No |
| `workspaces` | Core | Yes | Yes (`workspace_audit_log`) | Yes (`workspace_created`, `workspace_updated`, `workspace_archived`) | No |
| `entity` | Core | Yes | Yes (`entity_audit_log`) | Yes (`entity_created`, `entity_updated`, `entity_archived`) | No |
| `organization_join_requests` | Core | No (phase 2) | N/A | N/A | No |
| `organization_invitations` | Core | No (phase 2) | N/A | N/A | No |
| `workspace_invitations` | Core | No (phase 2) | N/A | N/A | No |
| `user_role_organization` | Core | No (phase 2) | N/A | N/A | No |
| `user_role_workspace` | Core | No (phase 2) | N/A | N/A | No |
| `organization_roles` | Core | No (phase 2) | N/A | N/A | No |
| `workspace_roles` | Core | No (phase 2) | N/A | N/A | No |

## Notes

- Required-now scope is intentionally strict and limited to the four principal business tables currently enforced by application flows.
- New entities/tables introduced in future work must be added to this matrix and evaluated before completion.
