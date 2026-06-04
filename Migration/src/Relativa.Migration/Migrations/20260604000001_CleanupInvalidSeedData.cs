using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EfMigration = Microsoft.EntityFrameworkCore.Migrations.Migration;
using Relativa.Migration.Data;

#nullable disable

namespace Relativa.Migration.Migrations;

/// <summary>
/// Hard-deletes every row that violates current CRUD business rules.
/// All cleanup is fully generic — no hard-coded IDs or entity type names.
/// Down() is a deliberate no-op: deleted data cannot be recovered.
/// </summary>
[DbContext(typeof(MigrationDbContext))]
[Migration("20260604000001_CleanupInvalidSeedData")]
public partial class CleanupInvalidSeedData : EfMigration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── Step 1: Wrong typed-column entity_property_value rows ────────────
        // Each property has a single canonical value column determined by its data_type.
        // Any row that has a non-NULL value in a column that doesn't match the type is corrupt.
        migrationBuilder.Sql(
            """
            DELETE FROM entity_property_value epv
            USING property p
            WHERE epv.property_id = p.id
              AND (
                  (p.data_type = 'String'  AND (epv.value_int  IS NOT NULL OR epv.value_decimal IS NOT NULL OR epv.value_bool IS NOT NULL OR epv.value_date IS NOT NULL))
               OR (p.data_type = 'Int'     AND (epv.value_string IS NOT NULL OR epv.value_decimal IS NOT NULL OR epv.value_bool IS NOT NULL OR epv.value_date IS NOT NULL))
               OR (p.data_type = 'Decimal' AND (epv.value_string IS NOT NULL OR epv.value_int    IS NOT NULL OR epv.value_bool IS NOT NULL OR epv.value_date IS NOT NULL))
               OR (p.data_type = 'Bool'    AND (epv.value_string IS NOT NULL OR epv.value_int    IS NOT NULL OR epv.value_decimal IS NOT NULL OR epv.value_date IS NOT NULL))
               OR (p.data_type = 'Date'    AND (epv.value_string IS NOT NULL OR epv.value_int    IS NOT NULL OR epv.value_decimal IS NOT NULL OR epv.value_bool IS NOT NULL))
              );
            """
        );

        // ── Step 2: Allowed-value violations ────────────────────────────────
        // For String properties that have rows in property_allowed_value,
        // delete any entity_property_value whose value_string is not in the allowed set.
        // Matching is case-insensitive (consistent with the application's OrdinalIgnoreCase check).
        migrationBuilder.Sql(
            """
            DELETE FROM entity_property_value epv
            USING property p
            WHERE epv.property_id = p.id
              AND p.data_type = 'String'
              AND EXISTS (
                  SELECT 1 FROM property_allowed_value WHERE property_id = p.id
              )
              AND NOT EXISTS (
                  SELECT 1 FROM property_allowed_value av
                  WHERE av.property_id = p.id
                    AND av.value ILIKE epv.value_string
              );
            """
        );

        // ── Step 3: Cardinality violations (many_to_one / one_to_one) ───────
        // For each relationship type constrained to at most one outgoing link per source,
        // keep only the row with the lowest id and delete the rest.
        migrationBuilder.Sql(
            """
            DELETE FROM entity_relationship er
            WHERE er.relationship_type_id IN (
                SELECT id FROM entity_relationship_type
                WHERE relationship_cardinality IN ('many_to_one', 'one_to_one')
            )
            AND er.id NOT IN (
                SELECT MIN(er2.id)
                FROM entity_relationship er2
                JOIN entity_relationship_type ert ON er2.relationship_type_id = ert.id
                WHERE ert.relationship_cardinality IN ('many_to_one', 'one_to_one')
                GROUP BY er2.source_entity_id, er2.relationship_type_id
            );
            """
        );

        // ── Step 4: Entities with no entity_workspace row ───────────────────
        // Such entities are completely inaccessible via the API.
        // Delete all their relationships first (both directions) to satisfy FK constraints,
        // then delete property values, workspace links (none exist), and the entity itself.
        migrationBuilder.Sql(
            """
            DO $$
            DECLARE
                v_orphan_ids int[];
            BEGIN
                SELECT ARRAY_AGG(e.id) INTO v_orphan_ids
                FROM entity e
                WHERE NOT EXISTS (
                    SELECT 1 FROM entity_workspace ew WHERE ew.entity_id = e.id
                );

                IF v_orphan_ids IS NULL OR array_length(v_orphan_ids, 1) = 0 THEN
                    RETURN;
                END IF;

                -- Remove outgoing relationships (source_entity_id CASCADE, but we need it gone first anyway)
                DELETE FROM entity_relationship WHERE source_entity_id = ANY(v_orphan_ids);
                -- Remove incoming relationships (target_entity_id RESTRICT — must delete before entity)
                DELETE FROM entity_relationship WHERE target_entity_id = ANY(v_orphan_ids);
                DELETE FROM entity_property_value WHERE entity_id = ANY(v_orphan_ids);
                DELETE FROM entity WHERE id = ANY(v_orphan_ids);
            END $$;
            """
        );

        // ── Step 5: Non-standalone entities with no incoming parent relationship ─
        // Iterate over every entity type marked is_standalone = FALSE that appears as a TARGET
        // in at least one relationship type. Entities of those types with zero incoming relationships
        // are orphans and must be hard-deleted.
        migrationBuilder.Sql(
            """
            DO $$
            DECLARE
                v_type_rec   RECORD;
                v_orphan_ids int[];
            BEGIN
                FOR v_type_rec IN
                    SELECT DISTINCT et.id AS type_id
                    FROM entity_type et
                    WHERE et.is_standalone = FALSE
                      AND EXISTS (
                          SELECT 1 FROM entity_relationship_type ert
                          WHERE ert.target_entity_type_id = et.id
                      )
                LOOP
                    SELECT ARRAY_AGG(e.id) INTO v_orphan_ids
                    FROM entity e
                    WHERE e.entity_type_id = v_type_rec.type_id
                      AND NOT EXISTS (
                          SELECT 1
                          FROM entity_relationship er
                          JOIN entity_relationship_type ert ON er.relationship_type_id = ert.id
                          WHERE er.target_entity_id = e.id
                            AND ert.target_entity_type_id = v_type_rec.type_id
                      );

                    IF v_orphan_ids IS NOT NULL AND array_length(v_orphan_ids, 1) > 0 THEN
                        DELETE FROM entity_relationship WHERE source_entity_id = ANY(v_orphan_ids);
                        DELETE FROM entity_relationship WHERE target_entity_id = ANY(v_orphan_ids);
                        DELETE FROM entity_property_value WHERE entity_id = ANY(v_orphan_ids);
                        DELETE FROM entity_workspace WHERE entity_id = ANY(v_orphan_ids);
                        DELETE FROM entity WHERE id = ANY(v_orphan_ids);
                    END IF;
                END LOOP;
            END $$;
            """
        );

        // ── Step 6: Entities missing a required outgoing relationship ────────
        // Covers entity types that are the SOURCE of an is_required=TRUE relationship type
        // but have zero rows for that relationship type (e.g., contract with no contract_deal link).
        migrationBuilder.Sql(
            """
            DO $$
            DECLARE
                v_rel_rec    RECORD;
                v_orphan_ids int[];
            BEGIN
                FOR v_rel_rec IN
                    SELECT DISTINCT ert.source_entity_type_id AS type_id, ert.id AS rel_type_id
                    FROM entity_relationship_type ert
                    WHERE ert.is_required = TRUE
                LOOP
                    SELECT ARRAY_AGG(e.id) INTO v_orphan_ids
                    FROM entity e
                    WHERE e.entity_type_id = v_rel_rec.type_id
                      AND NOT EXISTS (
                          SELECT 1
                          FROM entity_relationship er
                          WHERE er.source_entity_id = e.id
                            AND er.relationship_type_id = v_rel_rec.rel_type_id
                      );

                    IF v_orphan_ids IS NOT NULL AND array_length(v_orphan_ids, 1) > 0 THEN
                        DELETE FROM entity_relationship WHERE source_entity_id = ANY(v_orphan_ids);
                        DELETE FROM entity_relationship WHERE target_entity_id = ANY(v_orphan_ids);
                        DELETE FROM entity_property_value WHERE entity_id = ANY(v_orphan_ids);
                        DELETE FROM entity_workspace WHERE entity_id = ANY(v_orphan_ids);
                        DELETE FROM entity WHERE id = ANY(v_orphan_ids);
                    END IF;
                END LOOP;
            END $$;
            """
        );

        // ── Step 7: Backfill NULL created_by_user_id ────────────────────────
        // Early seed migrations inserted entities without created_by_user_id (column was added later).
        // The application's access checks require a non-NULL creator; backfill using the workspace
        // creator of the entity's first associated workspace.
        migrationBuilder.Sql(
            """
            UPDATE entity e
            SET created_by_user_id = (
                SELECT w.created_by_user_id
                FROM entity_workspace ew
                JOIN workspaces w ON w.id = ew.workspace_id
                WHERE ew.entity_id = e.id
                ORDER BY ew.workspace_id
                LIMIT 1
            )
            WHERE e.created_by_user_id IS NULL;
            """
        );

        // ── Step 8: Entities missing required properties ─────────────────────
        // For every entity type that has is_required=TRUE properties, find entities that are
        // missing at least one required property value. These violate the creation contract
        // and can never be served correctly by the API.
        migrationBuilder.Sql(
            """
            DO $$
            DECLARE
                v_type_rec   RECORD;
                v_prop_rec   RECORD;
                v_orphan_ids int[];
                v_missing    int[];
            BEGIN
                FOR v_type_rec IN
                    SELECT DISTINCT entity_type_id AS type_id
                    FROM entity_type_property
                    WHERE is_required = TRUE
                LOOP
                    v_orphan_ids := ARRAY[]::int[];

                    FOR v_prop_rec IN
                        SELECT property_id
                        FROM entity_type_property
                        WHERE entity_type_id = v_type_rec.type_id
                          AND is_required = TRUE
                    LOOP
                        -- Collect entities of this type that have no value row for this required property
                        SELECT ARRAY_AGG(e.id) INTO v_missing
                        FROM entity e
                        WHERE e.entity_type_id = v_type_rec.type_id
                          AND NOT EXISTS (
                              SELECT 1
                              FROM entity_property_value epv
                              WHERE epv.entity_id = e.id
                                AND epv.property_id = v_prop_rec.property_id
                                -- The value column for the correct type must be non-NULL
                                AND (
                                    epv.value_string  IS NOT NULL
                                 OR epv.value_int     IS NOT NULL
                                 OR epv.value_decimal IS NOT NULL
                                 OR epv.value_bool    IS NOT NULL
                                 OR epv.value_date    IS NOT NULL
                                )
                          );

                        IF v_missing IS NOT NULL THEN
                            v_orphan_ids := v_orphan_ids || v_missing;
                        END IF;
                    END LOOP;

                    -- Deduplicate
                    SELECT ARRAY_AGG(DISTINCT id) INTO v_orphan_ids
                    FROM UNNEST(v_orphan_ids) AS id;

                    IF v_orphan_ids IS NOT NULL AND array_length(v_orphan_ids, 1) > 0 THEN
                        DELETE FROM entity_relationship WHERE source_entity_id = ANY(v_orphan_ids);
                        DELETE FROM entity_relationship WHERE target_entity_id = ANY(v_orphan_ids);
                        DELETE FROM entity_property_value WHERE entity_id = ANY(v_orphan_ids);
                        DELETE FROM entity_workspace WHERE entity_id = ANY(v_orphan_ids);
                        DELETE FROM entity WHERE id = ANY(v_orphan_ids);
                    END IF;
                END LOOP;
            END $$;
            """
        );

        // ── Step 9: Stale permanently-failed audit_outbox messages ──────────
        migrationBuilder.Sql(
            """
            DELETE FROM audit_outbox
            WHERE publish_attempts > 10
              AND published_at_utc IS NULL;
            """
        );

        // ── Step 10: Long-expired pending invitations ────────────────────────
        // Pending invitations more than 30 days past their expiry are stale noise.
        migrationBuilder.Sql(
            """
            DELETE FROM organization_invitations
            WHERE status = 'Pending'
              AND expires_at < NOW() - INTERVAL '30 days';
            """
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Deliberate no-op: hard-deleted rows cannot be recovered.
        // Run the seed migration again if test data is needed.
    }
}
