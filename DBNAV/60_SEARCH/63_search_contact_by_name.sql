-- ============================================================
-- Script: 63_search_contact_by_name.sql
-- Purpose: Search contact entities by first name or last name using
--          a case-insensitive partial match.
-- Tables:  entity, entity_type, entity_property_value, property,
--          entity_workspace, workspaces
-- Usage:   Replace :search_term with a partial name.
--          In psql: \set search_term 'john'
-- Notes:   Matches against both first_name and last_name properties.
--          Returns both active and archived contacts.
-- ============================================================

SELECT
    e.id AS entity_id,
    MAX(CASE WHEN p.name = 'first_name'  THEN epv.value_string END) AS first_name,
    MAX(CASE WHEN p.name = 'last_name'   THEN epv.value_string END) AS last_name,
    MAX(CASE WHEN p.name = 'email'       THEN epv.value_string END) AS email,
    MAX(CASE WHEN p.name = 'job_title'   THEN epv.value_string END) AS job_title,
    e.is_archived,
    STRING_AGG(DISTINCT w.name, ', ' ORDER BY w.name)               AS workspaces
FROM entity e
JOIN entity_type           et  ON et.id = e.entity_type_id AND et.name = 'contact'
JOIN entity_property_value epv ON epv.entity_id  = e.id
JOIN property              p   ON p.id           = epv.property_id
LEFT JOIN entity_workspace ew  ON ew.entity_id   = e.id
LEFT JOIN workspaces       w   ON w.id           = ew.workspace_id
WHERE e.id IN (
    SELECT epv2.entity_id
    FROM entity_property_value epv2
    JOIN property p2 ON p2.id = epv2.property_id
        AND p2.name IN ('first_name', 'last_name')
    WHERE epv2.value_string ILIKE '%' || :'search_term' || '%'
)
GROUP BY e.id, e.is_archived
ORDER BY last_name, first_name;
