-- ============================================================
-- Script: 57_property_allowed_values.sql
-- Purpose: List all properties that have constrained allowed values
--          (enum-style String properties) with those values and
--          which entity types use them.
-- Tables:  property, property_allowed_value, entity_type_property,
--          entity_type
-- Usage:   Run as-is to see all enum properties and values.
--          To filter to a specific property add:
--            AND p.name = 'client_status'
-- Notes:   Allowed value matching in the application is
--          case-insensitive. The value column is the stored raw value;
--          display_name is the human-readable label shown in the UI.
-- ============================================================

SELECT
    p.name                          AS property_name,
    p.display_name                  AS property_display_name,
    STRING_AGG(DISTINCT et.name, ', ' ORDER BY et.name)
                                    AS used_by_entity_types,
    pav.value,
    pav.display_name                AS value_display_name
FROM property p
JOIN property_allowed_value pav ON pav.property_id  = p.id
LEFT JOIN entity_type_property etp ON etp.property_id = p.id
LEFT JOIN entity_type          et  ON et.id           = etp.entity_type_id
GROUP BY p.name, p.display_name, pav.value, pav.display_name
ORDER BY p.name, pav.value;
