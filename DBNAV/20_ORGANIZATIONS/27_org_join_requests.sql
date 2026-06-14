-- ============================================================
-- Script: 27_org_join_requests.sql
-- Purpose: List all join requests for an organization, showing
--          requester info, optional message, review status, and
--          who reviewed them.
-- Tables:  organization_join_requests, users (requester + reviewer),
--          organizations
-- Usage:   Replace :org_id with the target organization UUID.
--          In psql: \set org_id 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
-- Notes:   Join requests only appear when the organization's join_policy
--          is 'open'. Filter by status to narrow down:
--          Pending, Approved, Rejected.
-- ============================================================

SELECT
    jr.id,
    req.id                                      AS requester_user_id,
    req.first_name || ' ' || req.last_name      AS requester_name,
    req.email                                   AS requester_email,
    jr.message,
    jr.status,
    jr.created_at,
    rev.first_name || ' ' || rev.last_name      AS reviewed_by,
    jr.reviewed_at
FROM organization_join_requests jr
JOIN users req ON req.id = jr.user_id
LEFT JOIN users rev ON rev.id = jr.reviewed_by_user_id
WHERE jr.organization_id = :'org_id'
-- AND jr.status = 'Pending'   -- uncomment to filter by status
ORDER BY jr.created_at DESC;
