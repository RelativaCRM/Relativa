-- ============================================================
-- Script: 71_entity_audit_history.sql
-- Purpose: Full chronological audit trail for a single entity —
--          all create, update, archive, and relationship events.
-- Tables:  entity_audit_log, users
-- Usage:   Replace :entity_id with the target entity UUID.
--          In psql: \set entity_id 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
-- Notes:   old_value and new_value are JSONB. Use ->> 'key' to extract
--          specific fields if needed.
--          action values: entity_created, entity_updated,
--          entity_archived, relationship_reassigned.
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
FROM entity_audit_log al
LEFT JOIN users u ON u.id = al.changed_by_id
WHERE al.entity_id = :'entity_id'
ORDER BY al.changed_at DESC;
