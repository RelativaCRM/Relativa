-- ============================================================
-- Script: 83_entities_without_property_values.sql
-- Purpose: Find entities that have no property values at all —
--          likely incomplete or broken records created without
--          the normal EAV value insertion.
-- Tables:  entity, entity_property_value, entity_type
-- Usage:   Run as-is. No parameters required.
-- Notes:   Under normal operation newly created entities immediately
--          receive property values. Rows returned here suggest a
--          failed write somewhere in the creation flow.
--          Unlike orphaned entities, these are workspace-linked but
--          have empty data.
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
    SELECT 1 FROM entity_property_value epv WHERE epv.entity_id = e.id
)
ORDER BY et.name, e.id;
