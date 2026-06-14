-- ============================================================
-- Script: 54_entity_by_id_full.sql
-- Purpose: Complete dump of all property values for a single entity,
--          one row per property. Includes the entity's type, creator,
--          workspace membership, and all EAV property values.
-- Tables:  entity, entity_type, entity_property_value, property,
--          entity_workspace, workspaces, users
-- Usage:   Replace :entity_id with the target entity UUID.
--          In psql: \set entity_id 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
-- Notes:   The value column is a COALESCE across all typed value
--          columns, cast to text. Only one column will be non-NULL
--          per row (determined by property.data_type).
-- ============================================================

-- ── Entity metadata ──────────────────────────────────────────
SELECT
    e.id,
    et.name          AS entity_type,
    et.display_name  AS entity_type_display,
    e.is_archived,
    u.email          AS created_by_email,
    STRING_AGG(w.name, ', ' ORDER BY w.name) AS workspaces
FROM entity e
JOIN entity_type et ON et.id = e.entity_type_id
LEFT JOIN users           u  ON u.id           = e.created_by_user_id
LEFT JOIN entity_workspace ew ON ew.entity_id  = e.id
LEFT JOIN workspaces       w  ON w.id          = ew.workspace_id
WHERE e.id = :'entity_id'
GROUP BY e.id, et.name, et.display_name, e.is_archived, u.email;

-- ── Property values ──────────────────────────────────────────
SELECT
    p.name                                AS property_name,
    p.display_name                        AS property_display_name,
    p.data_type,
    p.is_readonly,
    COALESCE(
        epv.value_string,
        epv.value_int::text,
        epv.value_decimal::text,
        epv.value_bool::text,
        epv.value_date::text
    )                                     AS value
FROM entity_property_value epv
JOIN property p ON p.id = epv.property_id
WHERE epv.entity_id = :'entity_id'
ORDER BY p.name;
