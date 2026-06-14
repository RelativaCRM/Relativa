-- ============================================================
-- Script: 26_org_pending_invitations.sql
-- Purpose: List invitations for an organization with their current
--          status, expiry date, invited role, and who sent them.
-- Tables:  organization_invitations, users, organization_roles,
--          organizations
-- Usage:   Replace :org_id with the target organization UUID.
--          In psql: \set org_id 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
-- Notes:   Default filter shows only 'Pending' invitations. Uncomment
--          the alternative WHERE clause to see all statuses.
--          An invitation whose expires_at < NOW() but status is still
--          'Pending' has not been cleaned up — see script
--          86_pending_invitations_expired.sql for a health check.
-- ============================================================

SELECT
    i.id,
    i.email           AS invited_email,
    r.name            AS invited_role,
    u.first_name || ' ' || u.last_name AS invited_by,
    u.email           AS inviter_email,
    i.status,
    i.created_at,
    i.expires_at,
    CASE WHEN i.expires_at < NOW() AND i.status = 'Pending'
         THEN TRUE ELSE FALSE END AS is_overdue
FROM organization_invitations i
JOIN organization_roles r ON r.id = i.org_role_id
JOIN users              u ON u.id = i.invited_by_user_id
WHERE i.organization_id = :'org_id'
  AND i.status = 'Pending'   -- change or remove to see other statuses
-- AND i.status IN ('Pending', 'Accepted', 'Cancelled', 'Expired')
ORDER BY i.created_at DESC;
