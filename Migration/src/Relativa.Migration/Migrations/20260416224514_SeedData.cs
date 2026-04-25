using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Relativa.Migration.Migrations
{
    /// <inheritdoc />
    public partial class SeedData : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- ============================================================
-- 1. Permissions (16 granular)
-- ============================================================
INSERT INTO permissions (id, name, is_archived) VALUES
-- Organization-scoped (ids 1-7)
(1,  'manage_org_settings',  FALSE),
(2,  'invite_to_org',        FALSE),
(3,  'manage_join_requests', FALSE),
(4,  'remove_org_members',   FALSE),
(5,  'assign_org_roles',     FALSE),
(6,  'manage_org_roles',     FALSE),
(7,  'create_workspaces',    FALSE),
-- Workspace-scoped (ids 8-16)
(8,  'manage_ws_settings',   FALSE),
(9,  'invite_to_workspace',  FALSE),
(10, 'add_ws_members',       FALSE),
(11, 'remove_ws_members',    FALSE),
(12, 'assign_ws_roles',      FALSE),
(13, 'manage_ws_roles',      FALSE),
(14, 'manage_entities',      FALSE),
(15, 'view_entities',        FALSE),
(16, 'view_analytics',       FALSE);

-- ============================================================
-- 2. Organization Roles (system, organization_id = NULL)
-- ============================================================
INSERT INTO organization_roles (id, name, organization_id, is_archived) VALUES
(1, 'org_owner',  NULL, FALSE),
(2, 'org_admin',  NULL, FALSE),
(3, 'org_member', NULL, FALSE);

-- org_owner: all 7 org permissions
INSERT INTO organization_role_permissions (id, org_role_id, permission_id) VALUES
(1, 1, 1), (2, 1, 2), (3, 1, 3), (4, 1, 4), (5, 1, 5), (6, 1, 6), (7, 1, 7);

-- org_admin: all org permissions except manage_org_roles (6)
INSERT INTO organization_role_permissions (id, org_role_id, permission_id) VALUES
(8, 2, 1), (9, 2, 2), (10, 2, 3), (11, 2, 4), (12, 2, 5), (13, 2, 7);

-- ============================================================
-- 3. Workspace Roles (system, workspace_id = NULL)
-- ============================================================
INSERT INTO workspace_roles (id, name, workspace_id, is_archived) VALUES
(1, 'ws_admin',   NULL, FALSE),
(2, 'ws_manager', NULL, FALSE),
(3, 'ws_analyst', NULL, FALSE),
(4, 'ws_member',  NULL, FALSE);

-- ws_admin: all 9 ws permissions (8-16)
INSERT INTO workspace_role_permissions (id, ws_role_id, permission_id) VALUES
(1, 1, 8), (2, 1, 9), (3, 1, 10), (4, 1, 11), (5, 1, 12), (6, 1, 13), (7, 1, 14), (8, 1, 15), (9, 1, 16);

-- ws_manager: invite_to_workspace, add_ws_members, manage_entities, view_entities, view_analytics
INSERT INTO workspace_role_permissions (id, ws_role_id, permission_id) VALUES
(10, 2, 9), (11, 2, 10), (12, 2, 14), (13, 2, 15), (14, 2, 16);

-- ws_analyst: view_analytics, view_entities
INSERT INTO workspace_role_permissions (id, ws_role_id, permission_id) VALUES
(15, 3, 15), (16, 3, 16);

-- ws_member: view_entities
INSERT INTO workspace_role_permissions (id, ws_role_id, permission_id) VALUES
(17, 4, 15);

-- ============================================================
-- 4. Organizations
-- ============================================================
INSERT INTO organizations (id, name, is_archived) VALUES
(1, 'Relativa Global', FALSE),
(2, 'Tech Innovators', FALSE);

-- ============================================================
-- 5. Users (placeholder bcrypt hashes)
-- ============================================================
INSERT INTO users (id, first_name, last_name, email, password, created_at, is_archived) VALUES
(1, 'Dorian',  'Gray',     'admin@relativa.com',    '$2a$11$U0L3412xLEeQjOfrj5VGb.kPt.RAHBaV/lSNIbHesBuQc90DmFHfC', CURRENT_TIMESTAMP, FALSE),
(2, 'Ivan',    'Franko',  'ivan.f@relativa.com',   '$2a$11$4J7luzuGBbWMQhuGnebPnu34QyUe867wkeBqahTtrVfjV0YMHNhqu',  CURRENT_TIMESTAMP, FALSE),
(3, 'Lesya',   'Ukrainka', 'lesya.u@relativa.com', '$2a$11$whaqAlWKw6kwO5K4hh2c5.DsjWOsSxIIP5QOLQK0/yZFWFZVDQMW2', CURRENT_TIMESTAMP, FALSE);

-- ============================================================
-- 6. Organization Memberships
-- ============================================================
INSERT INTO user_role_organization (id, user_id, organization_id, org_role_id, joined_at, is_archived) VALUES
(1, 1, 1, 1, CURRENT_TIMESTAMP, FALSE),  -- Dorian = org_owner @ Relativa Global
(2, 2, 1, 3, CURRENT_TIMESTAMP, FALSE),  -- Ivan   = org_member @ Relativa Global
(3, 3, 1, 3, CURRENT_TIMESTAMP, FALSE);  -- Lesya  = org_member @ Relativa Global

-- ============================================================
-- 7. Workspaces
-- ============================================================
INSERT INTO workspaces (id, name, organization_id, created_by_user_id, is_archived) VALUES
(1, 'EU Sales Workspace', 1, 1, FALSE),
(2, 'US Tech Workspace',  2, 1, FALSE);

-- ============================================================
-- 8. Workspace Memberships
-- ============================================================
INSERT INTO user_role_workspace (id, user_id, workspace_id, ws_role_id, joined_at, is_archived) VALUES
(1, 1, 1, 1, CURRENT_TIMESTAMP, FALSE),  -- Dorian = ws_admin   @ EU Sales
(2, 2, 1, 2, CURRENT_TIMESTAMP, FALSE),  -- Ivan   = ws_manager @ EU Sales
(3, 3, 1, 3, CURRENT_TIMESTAMP, FALSE);  -- Lesya  = ws_analyst @ EU Sales

-- ============================================================
-- 9. Entity Types (InitialCreate schema — EAV applied in later migration)
-- ============================================================
INSERT INTO entity_types (id, type_id, is_archived) VALUES
(1, 'client', FALSE),
(2, 'deal',   FALSE);

-- ============================================================
-- 10. Entities
-- ============================================================
INSERT INTO entities (id, type, is_archived) VALUES
(1, 1, FALSE),  -- Client 1
(2, 1, FALSE),  -- Client 2
(3, 2, FALSE),  -- Deal 1
(4, 2, FALSE),  -- Deal 2
(5, 2, FALSE);  -- Deal 3

INSERT INTO entity_workspaces (id, entity_id, workspace_id) VALUES
(1, 1, 1), (2, 2, 1), (3, 3, 1), (4, 4, 1), (5, 5, 1);

INSERT INTO personal_data_property_values (id, first_name, last_name, phone_number, email, passport_number, birth_date) VALUES
(1, 'Oleksiy', 'Ivanenko',    '+380671234567', 'o.ivanenko@tech.ua', NULL, '1985-05-20'),
(2, 'Maria',   'Zankovetska', '+380501234567', 'm.zan@corp.ua',      NULL, '1990-11-15');

INSERT INTO location_property_values (id, country, region, state, city, address, postal_code, locale, timezone) VALUES
(1, 'Ukraine', 'Kyiv Oblast', NULL, 'Kyiv', 'Khreshchatyk St, 1',  '01001', 'uk-UA', 'Europe/Kyiv'),
(2, 'Ukraine', 'Lviv Oblast', NULL, 'Lviv', 'Rynok Square, 10',    '79000', 'uk-UA', 'Europe/Kyiv');

INSERT INTO deal_property_values (id, value, owner_id, client_id, expected_close, closure_score, created_at) VALUES
(1, 25000.00, 2, 1, '2026-06-30 00:00:00+00', 0.74, CURRENT_TIMESTAMP),
(2, 15000.00, 3, 2, '2026-05-15 00:00:00+00', 0.85, CURRENT_TIMESTAMP),
(3, 50000.00, 2, 1, '2026-08-01 00:00:00+00', 0.45, CURRENT_TIMESTAMP);

INSERT INTO entity_properties (id, entity_id, personal_data_property_id, location_property_id, deal_property_id) VALUES
(1, 1, 1, 1, NULL),
(2, 2, 2, 2, NULL),
(3, 3, NULL, NULL, 1),
(4, 4, NULL, NULL, 2),
(5, 5, NULL, NULL, 3);

-- ============================================================
-- 11. Reset sequences (InitialCreate tables only)
-- ============================================================
SELECT setval(pg_get_serial_sequence('permissions', 'id'), (SELECT COALESCE(MAX(id),1) FROM permissions));
SELECT setval(pg_get_serial_sequence('organization_roles', 'id'), (SELECT COALESCE(MAX(id),1) FROM organization_roles));
SELECT setval(pg_get_serial_sequence('organization_role_permissions', 'id'), (SELECT COALESCE(MAX(id),1) FROM organization_role_permissions));
SELECT setval(pg_get_serial_sequence('workspace_roles', 'id'), (SELECT COALESCE(MAX(id),1) FROM workspace_roles));
SELECT setval(pg_get_serial_sequence('workspace_role_permissions', 'id'), (SELECT COALESCE(MAX(id),1) FROM workspace_role_permissions));
SELECT setval(pg_get_serial_sequence('organizations', 'id'), (SELECT COALESCE(MAX(id),1) FROM organizations));
SELECT setval(pg_get_serial_sequence('users', 'id'), (SELECT COALESCE(MAX(id),1) FROM users));
SELECT setval(pg_get_serial_sequence('user_role_organization', 'id'), (SELECT COALESCE(MAX(id),1) FROM user_role_organization));
SELECT setval(pg_get_serial_sequence('workspaces', 'id'), (SELECT COALESCE(MAX(id),1) FROM workspaces));
SELECT setval(pg_get_serial_sequence('user_role_workspace', 'id'), (SELECT COALESCE(MAX(id),1) FROM user_role_workspace));
SELECT setval(pg_get_serial_sequence('entity_types', 'id'), (SELECT COALESCE(MAX(id),1) FROM entity_types));
SELECT setval(pg_get_serial_sequence('entities', 'id'), (SELECT COALESCE(MAX(id),1) FROM entities));
SELECT setval(pg_get_serial_sequence('entity_workspaces', 'id'), (SELECT COALESCE(MAX(id),1) FROM entity_workspaces));
SELECT setval(pg_get_serial_sequence('personal_data_property_values', 'id'), (SELECT COALESCE(MAX(id),1) FROM personal_data_property_values));
SELECT setval(pg_get_serial_sequence('location_property_values', 'id'), (SELECT COALESCE(MAX(id),1) FROM location_property_values));
SELECT setval(pg_get_serial_sequence('deal_property_values', 'id'), (SELECT COALESCE(MAX(id),1) FROM deal_property_values));
SELECT setval(pg_get_serial_sequence('entity_properties', 'id'), (SELECT COALESCE(MAX(id),1) FROM entity_properties));
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM entity_properties;
DELETE FROM deal_property_values;
DELETE FROM location_property_values;
DELETE FROM personal_data_property_values;
DELETE FROM entity_workspaces;
DELETE FROM entities;
DELETE FROM entity_types;
DELETE FROM user_role_workspace;
DELETE FROM workspaces;
DELETE FROM user_role_organization;
DELETE FROM users;
DELETE FROM organizations;
DELETE FROM workspace_role_permissions;
DELETE FROM workspace_roles;
DELETE FROM organization_role_permissions;
DELETE FROM organization_roles;
DELETE FROM permissions;
            ");
        }
    }
}
