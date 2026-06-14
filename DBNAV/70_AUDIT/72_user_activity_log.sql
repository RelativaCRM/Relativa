-- ============================================================
-- Script: 72_user_activity_log.sql
-- Purpose: All recorded actions performed by a specific user across
--          all four audit log tables, unified into a single timeline.
-- Tables:  entity_audit_log, organization_audit_log,
--          workspace_audit_log, user_audit_log
-- Usage:   Replace :user_id with the target user UUID.
--          In psql: \set user_id 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
-- Notes:   The subject_id column holds the ID of the record that was
--          acted upon (entity_id, org_id, workspace_id, or user_id).
--          Adjust the LIMIT below to fetch more history.
-- ============================================================

SELECT * FROM (
    SELECT
        'entity'        AS log_source,
        id,
        changed_at,
        action,
        entity_id       AS subject_id,
        entity_type     AS subject_context,
        field_name,
        old_value,
        new_value
    FROM entity_audit_log
    WHERE changed_by_id = :'user_id'

    UNION ALL

    SELECT
        'organization'  AS log_source,
        id,
        changed_at,
        action,
        organization_id AS subject_id,
        NULL            AS subject_context,
        field_name,
        old_value,
        new_value
    FROM organization_audit_log
    WHERE changed_by_id = :'user_id'

    UNION ALL

    SELECT
        'workspace'     AS log_source,
        id,
        changed_at,
        action,
        workspace_id    AS subject_id,
        NULL            AS subject_context,
        field_name,
        old_value,
        new_value
    FROM workspace_audit_log
    WHERE changed_by_id = :'user_id'

    UNION ALL

    SELECT
        'user'          AS log_source,
        id,
        changed_at,
        action,
        target_user_id  AS subject_id,
        NULL            AS subject_context,
        field_name,
        old_value,
        new_value
    FROM user_audit_log
    WHERE changed_by_id = :'user_id'
) all_events
ORDER BY changed_at DESC
LIMIT 200;
