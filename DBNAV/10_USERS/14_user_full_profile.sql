-- ============================================================
-- Script: 14_user_full_profile.sql
-- Purpose: Comprehensive dump for a single user — core identity,
--          additional emails, external login providers, 2FA backup
--          code count, organization memberships, and workspace
--          memberships with roles.
-- Tables:  users, user_settings, user_email, user_external_login,
--          user_backup_code, user_role_organization, organizations,
--          organization_roles, user_role_workspace, workspaces,
--          workspace_roles
-- Usage:   Replace :user_id with the target UUID.
--          Results are returned as several separate result sets
--          (one per section). Run in psql or a client that supports
--          multiple result sets (e.g. DBeaver, DataGrip).
-- Notes:   Each section is labeled with a comment header.
-- ============================================================

-- ── 1. Core identity ─────────────────────────────────────────
SELECT
    u.id,
    u.first_name,
    u.last_name,
    u.email,
    u.phone,
    u.date_of_birth,
    u.created_at,
    u.email_verified,
    u.two_factor_enabled,
    u.is_archived,
    us.locale
FROM users u
LEFT JOIN user_settings us ON us.user_id = u.id
WHERE u.id = :'user_id';

-- ── 2. Additional email addresses ────────────────────────────
SELECT
    address,
    is_verified,
    source,
    created_at
FROM user_email
WHERE user_id = :'user_id'
ORDER BY created_at;

-- ── 3. External login providers ──────────────────────────────
SELECT
    provider,
    subject,
    created_at
FROM user_external_login
WHERE user_id = :'user_id'
ORDER BY provider;

-- ── 4. 2FA backup codes (counts only — never show hashes) ────
SELECT
    COUNT(*) FILTER (WHERE used_at IS NULL)  AS unused_backup_codes,
    COUNT(*) FILTER (WHERE used_at IS NOT NULL) AS used_backup_codes,
    COUNT(*)                                 AS total_backup_codes
FROM user_backup_code
WHERE user_id = :'user_id';

-- ── 5. Organization memberships ──────────────────────────────
SELECT
    o.id          AS org_id,
    o.name        AS org_name,
    r.name        AS role_name,
    r.display_name AS role_display_name,
    uro.joined_at,
    uro.is_archived AS membership_archived
FROM user_role_organization uro
JOIN organizations   o ON o.id = uro.organization_id
JOIN organization_roles r ON r.id = uro.org_role_id
WHERE uro.user_id = :'user_id'
ORDER BY uro.is_archived ASC, o.name;

-- ── 6. Workspace memberships ─────────────────────────────────
SELECT
    w.id          AS workspace_id,
    w.name        AS workspace_name,
    o.name        AS org_name,
    r.name        AS role_name,
    r.display_name AS role_display_name,
    urw.joined_at,
    urw.is_archived AS membership_archived
FROM user_role_workspace urw
JOIN workspaces      w ON w.id = urw.workspace_id
JOIN organizations   o ON o.id = w.organization_id
JOIN workspace_roles r ON r.id = urw.ws_role_id
WHERE urw.user_id = :'user_id'
ORDER BY urw.is_archived ASC, o.name, w.name;
