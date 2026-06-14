-- ============================================================
-- Script: 73_org_audit_log.sql
-- Purpose: Audit events for a specific organization — settings
--          changes, member add/remove, role changes, invitations,
--          and join requests.
-- Tables:  organization_audit_log, users
-- Usage:   Replace :org_id with the target organization UUID.
--          Optionally set :since_date to limit to recent events.
--          In psql:
--            \set org_id     'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
--            \set since_date '2025-01-01'
-- Notes:   Remove the since_date filter if you want all history.
--          Default LIMIT is 100 — raise it for full exports.
--          action values include: organization_created/updated,
--          settings_updated, member_added/removed/role_changed,
--          role_created/updated/archived, invitation_sent/accepted/
--          cancelled/expired, join_request_approved/rejected.
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
FROM organization_audit_log al
LEFT JOIN users u ON u.id = al.changed_by_id
WHERE al.organization_id = :'org_id'
  -- AND al.changed_at >= :'since_date'::date   -- uncomment to restrict date range
ORDER BY al.changed_at DESC
LIMIT 100;
