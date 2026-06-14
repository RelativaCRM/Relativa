-- ============================================================
-- Script: 32_workspace_by_id.sql
-- Purpose: Full detail for a single workspace including settings,
--          parent organization, creator, and member count.
-- Tables:  workspaces, workspace_settings, organizations, users
-- Usage:   Replace :workspace_id with the target workspace UUID.
--          In psql: \set workspace_id 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
-- Notes:   risk_scoring_enabled controls the ML risk model.
--          high_risk_threshold and medium_risk_threshold are decimal
--          values between 0 and 1 (e.g. 0.7 and 0.4).
-- ============================================================

SELECT
    w.id,
    w.name,
    w.is_archived,
    o.id             AS org_id,
    o.name           AS org_name,
    u.first_name || ' ' || u.last_name AS created_by,
    ws.description,
    ws.risk_scoring_enabled,
    ws.high_risk_threshold,
    ws.medium_risk_threshold
FROM workspaces w
JOIN organizations   o  ON o.id = w.organization_id
JOIN users           u  ON u.id = w.created_by_user_id
LEFT JOIN workspace_settings ws ON ws.workspace_id = w.id
WHERE w.id = :'workspace_id';
