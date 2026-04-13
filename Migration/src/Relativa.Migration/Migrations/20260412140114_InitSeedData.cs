using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Relativa.Migration.Migrations
{
    /// <inheritdoc />
    public partial class InitSeedData : Microsoft.EntityFrameworkCore.Migrations.Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- 1. Довідники: Ролі та Права доступу
INSERT INTO roles (id, name, is_archived) VALUES
(1, 'admin', FALSE),
(2, 'sales_manager', FALSE),
(3, 'analyst', FALSE);

INSERT INTO permissions (id, name, is_archived) VALUES
(1, 'can_manage_settings', FALSE),
(2, 'can_assign_roles', FALSE),
(3, 'can_edit_deals', FALSE),
(4, 'can_view_analytics', FALSE);

INSERT INTO role_permissions (id, role_id, permission_id) VALUES
(1, 1, 1), (2, 1, 2), (3, 1, 3), (4, 1, 4), -- Admin має всі права
(5, 2, 3),                                  -- Sales Manager може редагувати угоди
(6, 3, 4);                                  -- Analyst може бачити аналітику

-- 2. Мультитенантність: Організації та Робочі простори
INSERT INTO organizations (id, name, is_archived) VALUES
(1, 'Relativa Global', FALSE),
(2, 'Tech Innovators', FALSE);

INSERT INTO workspaces (id, name, is_archived) VALUES
(1, 'EU Sales Workspace', FALSE),
(2, 'US Tech Workspace', FALSE);

INSERT INTO organization_workspaces (id, org_id, workspace_id) VALUES
(1, 1, 1),
(2, 2, 2);

-- 3. Користувачі системи (Співробітники)
INSERT INTO users (id, first_name, last_name, email, password, role_id, created_at, is_archived) VALUES
(1, 'Dorian', 'Gray', 'admin@relativa.com', '$2y$10$hashed_pwd_placeholder', 1, CURRENT_TIMESTAMP, FALSE),
(2, 'Іван', 'Франко', 'ivan.f@relativa.com', '$2y$10$hashed_pwd_placeholder', 2, CURRENT_TIMESTAMP, FALSE),
(3, 'Леся', 'Українка', 'lesya.u@relativa.com', '$2y$10$hashed_pwd_placeholder', 2, CURRENT_TIMESTAMP, FALSE);

-- 4. Довідник типів сутностей
INSERT INTO entity_types (id, type_id, is_archived) VALUES
(1, 'client', FALSE),
(2, 'deal', FALSE);

-- 5. Базові сутності (Entities)
INSERT INTO entities (id, type, is_archived) VALUES
(1, 1, FALSE), -- Клієнт 1
(2, 1, FALSE), -- Клієнт 2
(3, 2, FALSE), -- Угода 1
(4, 2, FALSE), -- Угода 2
(5, 2, FALSE); -- Угода 3

INSERT INTO entity_workspaces (id, entity_id, workspace_id) VALUES
(1, 1, 1), (2, 2, 1), (3, 3, 1), (4, 4, 1), (5, 5, 1);

-- 6. Фізичні дані сутностей (Сховища властивостей)
INSERT INTO personal_data_property_values (id, first_name, last_name, phone_number, email, passport_number, birth_date) VALUES
(1, 'Олексій', 'Іваненко', '+380671234567', 'o.ivanenko@tech.ua', NULL, '1985-05-20'),
(2, 'Марія', 'Заньковецька', '+380501234567', 'm.zan@corp.ua', NULL, '1990-11-15');

INSERT INTO location_property_values (id, country, region, state, city, address, postal_code, locale, timezone) VALUES
(1, 'Ukraine', 'Kyiv Oblast', NULL, 'Kyiv', 'Вул. Хрещатик, 1', '01001', 'uk-UA', 'Europe/Kyiv'),
(2, 'Ukraine', 'Lviv Oblast', NULL, 'Lviv', 'Площа Ринок, 10', '79000', 'uk-UA', 'Europe/Kyiv');

INSERT INTO deal_property_values (id, value, owner_id, client_id, expected_close, closure_score, created_at) VALUES
(1, 25000.00, 2, 1, '2026-06-30 00:00:00', 0.74, CURRENT_TIMESTAMP), -- Угода Івана з Олексієм
(2, 15000.00, 3, 2, '2026-05-15 00:00:00', 0.85, CURRENT_TIMESTAMP), -- Угода Лесі з Марією
(3, 50000.00, 2, 1, '2026-08-01 00:00:00', 0.45, CURRENT_TIMESTAMP); -- Ще одна угода Івана з Олексієм

-- 7. Поліморфна маршрутизація (Хаб Властивостей)
INSERT INTO entity_properties (id, entity_id, personal_data_property_id, location_property_id, deal_property_id) VALUES
(1, 1, 1, 1, NULL), 
(2, 2, 2, 2, NULL),
(3, 3, NULL, NULL, 1),
(4, 4, NULL, NULL, 2),
(5, 5, NULL, NULL, 3);

-- 8. Синхронізація лічильників ідентифікаторів (Sequences) PostgreSQL
SELECT setval(pg_get_serial_sequence('roles', 'id'), coalesce(max(id), 1), max(id) IS NOT null) FROM roles;
SELECT setval(pg_get_serial_sequence('permissions', 'id'), coalesce(max(id), 1), max(id) IS NOT null) FROM permissions;
SELECT setval(pg_get_serial_sequence('role_permissions', 'id'), coalesce(max(id), 1), max(id) IS NOT null) FROM role_permissions;
SELECT setval(pg_get_serial_sequence('organizations', 'id'), coalesce(max(id), 1), max(id) IS NOT null) FROM organizations;
SELECT setval(pg_get_serial_sequence('workspaces', 'id'), coalesce(max(id), 1), max(id) IS NOT null) FROM workspaces;
SELECT setval(pg_get_serial_sequence('organization_workspaces', 'id'), coalesce(max(id), 1), max(id) IS NOT null) FROM organization_workspaces;
SELECT setval(pg_get_serial_sequence('users', 'id'), coalesce(max(id), 1), max(id) IS NOT null) FROM users;
SELECT setval(pg_get_serial_sequence('entity_types', 'id'), coalesce(max(id), 1), max(id) IS NOT null) FROM entity_types;
SELECT setval(pg_get_serial_sequence('entities', 'id'), coalesce(max(id), 1), max(id) IS NOT null) FROM entities;
SELECT setval(pg_get_serial_sequence('entity_workspaces', 'id'), coalesce(max(id), 1), max(id) IS NOT null) FROM entity_workspaces;
SELECT setval(pg_get_serial_sequence('personal_data_property_values', 'id'), coalesce(max(id), 1), max(id) IS NOT null) FROM personal_data_property_values;
SELECT setval(pg_get_serial_sequence('location_property_values', 'id'), coalesce(max(id), 1), max(id) IS NOT null) FROM location_property_values;
SELECT setval(pg_get_serial_sequence('deal_property_values', 'id'), coalesce(max(id), 1), max(id) IS NOT null) FROM deal_property_values;
SELECT setval(pg_get_serial_sequence('entity_properties', 'id'), coalesce(max(id), 1), max(id) IS NOT null) FROM entity_properties;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM entity_properties WHERE id IN (1, 2, 3, 4, 5);
DELETE FROM deal_property_values WHERE id IN (1, 2, 3);
DELETE FROM location_property_values WHERE id IN (1, 2);
DELETE FROM personal_data_property_values WHERE id IN (1, 2);
DELETE FROM entity_workspaces WHERE id IN (1, 2, 3, 4, 5);
DELETE FROM entities WHERE id IN (1, 2, 3, 4, 5);
DELETE FROM entity_types WHERE id IN (1, 2);
DELETE FROM users WHERE id IN (1, 2, 3);
DELETE FROM organization_workspaces WHERE id IN (1, 2);
DELETE FROM workspaces WHERE id IN (1, 2);
DELETE FROM organizations WHERE id IN (1, 2);
DELETE FROM role_permissions WHERE id IN (1, 2, 3, 4, 5, 6);
DELETE FROM permissions WHERE id IN (1, 2, 3, 4);
DELETE FROM roles WHERE id IN (1, 2, 3);
            ");
        }
    }
}
