-- ============================================================
-- Script: 52_entity_type_property_schema.sql
-- Purpose: Detailed property schema for one entity type — name,
--          display name, data type, required/readonly flags, and
--          the list of allowed values for enum-style properties.
-- Tables:  entity_type, entity_type_property, property,
--          property_allowed_value
-- Usage:   Replace :entity_type_name with the type to inspect.
--          In psql: \set entity_type_name 'client'
--          Valid values: client, deal, contact, task, note,
--                        contract, deal_analysis
-- Notes:   allowed_values is NULL for non-enum properties.
--          Allowed values are case-insensitive in the application.
-- ============================================================

SELECT
    p.name              AS property_name,
    p.display_name      AS property_display_name,
    p.data_type,
    etp.is_required,
    p.is_readonly,
    CASE WHEN p.organization_id IS NULL THEN 'global' ELSE 'org-custom' END
                        AS property_scope,
    STRING_AGG(pav.value || ' (' || pav.display_name || ')', ' | '
               ORDER BY pav.value)  AS allowed_values
FROM entity_type et
JOIN entity_type_property    etp ON etp.entity_type_id = et.id
JOIN property                p   ON p.id               = etp.property_id
LEFT JOIN property_allowed_value pav ON pav.property_id = p.id
WHERE et.name = :'entity_type_name'
GROUP BY p.name, p.display_name, p.data_type, etp.is_required,
         p.is_readonly, p.organization_id
ORDER BY etp.is_required DESC, p.name;
