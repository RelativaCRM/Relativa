-- ============================================================
-- Script: 74_workspace_audit_log.sql
-- Purpose: Audit events for a specific workspace — creation,
--          settings changes, member add/remove/role changes,
--          role management, and archiving.
-- Tables:  workspace_audit_log, users
-- Usage:   Replace :workspace_id with the target workspace UUID.
--          In psql:
--            \set workspace_id 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
--            \set since_date   '2025-01-01'
-- Notes:   Remove the since_date filter to see all history.
--          action values: workspace_created/updated/archived,
--          settings_updated, member_added/removed/role_changed,
--          role_created/updated/archived.
-- ============================================================

SELECT
    al.id,
    al.changed_at,
    al.action,
    u.first_name || ' ' || u.last_name AS changed_by,
    u.email                            AS changed_by_email,
    al.field_name,
    al.old_value,
    al.new_value
FROM workspace_audit_log al
LEFT JOIN users u ON u.id = al.changed_by_id
WHERE al.workspace_id = :'workspace_id'
  -- AND al.changed_at >= :'since_date'::date   -- uncomment to restrict date range
ORDER BY al.changed_at DESC
LIMIT 100;
