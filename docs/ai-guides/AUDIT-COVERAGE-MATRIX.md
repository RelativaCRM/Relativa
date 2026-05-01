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
| `organization_join_requests` | Core | Yes | Yes (`organization_audit_log`) | Yes (`organization_join_request_submitted`, `organization_join_request_reviewed`) | No |
| `organization_invitations` | Core | Yes | Yes (`organization_audit_log`) | Yes (`organization_invitation_created`, `organization_invitation_cancelled`, `organization_invitation_accepted`) | No |
| `workspace_invitations` | Core | Yes | Yes (`workspace_audit_log`) | Yes (`workspace_invitation_created`, `workspace_invitation_cancelled`, `workspace_invitation_accepted`) | No |
| `user_role_organization` | Core | Yes | Yes (`organization_audit_log`) | Yes (`organization_member_added_via_join_request`, `organization_member_added_via_invitation`) | No |
| `user_role_workspace` | Core | Yes | Yes (`workspace_audit_log`) | Yes (`workspace_member_added`, `workspace_member_added_via_invitation`, `workspace_member_removed`, `workspace_member_role_changed`) | No |
| `organization_roles` | Core | Yes | Yes (`organization_audit_log`) | Yes (`organization_role_created`, `organization_role_updated`, `organization_role_archived`) | No |
| `workspace_roles` | Core | Yes | Yes (`workspace_audit_log`) | Yes (`workspace_role_created`, `workspace_role_updated`, `workspace_role_archived`) | No |

## Notes

- Required-now scope now includes principal entities plus invitations, memberships, join requests, and role lifecycle flows.
- New entities/tables introduced in future work must be added to this matrix and evaluated before completion.
