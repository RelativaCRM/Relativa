-- ============================================================
-- Script: 86_pending_invitations_expired.sql
-- Purpose: Find organization invitations whose expiry date has
--          passed but whose status is still 'Pending'. These
--          should have been transitioned to 'Expired' automatically
--          by the application.
-- Tables:  organization_invitations, organizations
-- Usage:   Run as-is. No parameters required.
-- Notes:   Invitations expire 7 days after creation by default.
--          If this query returns rows it indicates the expiry
--          cleanup job is not running or has fallen behind.
--          These invitations are effectively dead — the token will
--          be rejected at acceptance time — but they clutter the
--          pending invitation list.
-- ============================================================

SELECT
    i.id,
    o.name            AS org_name,
    i.email           AS invited_email,
    i.created_at,
    i.expires_at,
    NOW() - i.expires_at AS overdue_by,
    i.status
FROM organization_invitations i
JOIN organizations o ON o.id = i.organization_id
WHERE i.status     = 'Pending'
  AND i.expires_at < NOW()
ORDER BY i.expires_at ASC;
