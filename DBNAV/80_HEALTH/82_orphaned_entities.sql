-- ============================================================
-- Script: 82_orphaned_entities.sql
-- Purpose: Find entities that exist in the entity table but are
--          not scoped to any workspace (no row in entity_workspace).
--          These entities are invisible to users but consume storage.
-- Tables:  entity, entity_workspace, entity_type
-- Usage:   Run as-is. No parameters required.
-- Notes:   Orphaned entities likely indicate a bug — they were
--          created but the entity_workspace link was never written.
--          Under normal operation this query should return 0 rows.
--          Do NOT delete these without investigation; the entity
--          might still be referenced by entity_relationship rows.
-- ============================================================

SELECT
    e.id           AS entity_id,
    et.name        AS entity_type,
    et.display_name,
    e.is_archived,
    u.email        AS created_by_email
FROM entity e
JOIN entity_type et ON et.id = e.entity_type_id
LEFT JOIN users  u  ON u.id  = e.created_by_user_id
WHERE NOT EXISTS (
    SELECT 1 FROM entity_workspace ew WHERE ew.entity_id = e.id
)
ORDER BY et.name, e.id;
