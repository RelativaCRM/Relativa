-- ============================================================
-- Script: 51_all_entity_types_with_properties.sql
-- Purpose: List all entity types with their properties, data types,
--          required/readonly flags, and whether the type is standalone.
-- Tables:  entity_type, entity_type_property, property
-- Usage:   Run as-is. No parameters required.
-- Notes:   is_standalone=TRUE means the entity can be created
--          independently (client, deal, contact, task).
--          is_standalone=FALSE means it requires a parent relationship
--          (note, contract, deal_analysis).
--          is_readonly=TRUE properties are system-managed and should
--          not be written by application users.
-- ============================================================

SELECT
    et.name                 AS entity_type,
    et.display_name         AS entity_type_display,
    et.is_standalone,
    p.name                  AS property_name,
    p.display_name          AS property_display_name,
    p.data_type,
    etp.is_required,
    p.is_readonly,
    CASE WHEN p.organization_id IS NULL THEN 'global' ELSE 'org-custom' END
                            AS property_scope
FROM entity_type et
LEFT JOIN entity_type_property etp ON etp.entity_type_id = et.id
LEFT JOIN property             p   ON p.id               = etp.property_id
ORDER BY et.name, p.name;
