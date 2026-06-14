# Relativa CRM — Довідник API ендпоінтів

> **Архітектура:** Мікросервіси (.NET Minimal API) за API-шлюзом
> **Базовий префікс:** Усі зовнішні маршрути проксіюються через шлюз (Gateway)
> **Автентифікація:** JWT Bearer — шлюз перевіряє токени та додає заголовки `X-User-Id` / `X-User-Email` перед пересиланням

---

## Зміст

1. [Автентифікація](#1-автентифікація)
2. [Підтримка](#2-підтримка)
3. [Організації](#3-організації)
   - [Учасники](#31-учасники-організації)
   - [Ролі](#32-ролі-організації)
   - [Запрошення](#33-запрошення-до-організації)
   - [Запити на вступ (рівень організації)](#34-запити-на-вступ-рівень-організації)
4. [Робочі простори](#4-робочі-простори)
   - [Учасники](#41-учасники-робочого-простору)
   - [Ролі](#42-ролі-робочого-простору)
5. [Дозволи](#5-дозволи)
6. [Запрошення (рівень користувача)](#6-запрошення-рівень-користувача)
7. [Запити на вступ (рівень користувача)](#7-запити-на-вступ-рівень-користувача)
8. [Сутності](#8-сутності)
9. [Типи сутностей](#9-типи-сутностей)
10. [Зв'язки між сутностями](#10-звязки-між-сутностями)
11. [Граф сутностей (RPC)](#11-граф-сутностей-rpc)
12. [Запит до графу](#12-запит-до-графу)
13. [Дашборд — Організація](#13-дашборд--організація)
14. [Дашборд — Робочий простір](#14-дашборд--робочий-простір)
15. [Журнал аудиту](#15-журнал-аудиту)
16. [ML-скорінг](#16-ml-скорінг)
17. [Хаби реального часу (SignalR)](#17-хаби-реального-часу-signalr)
18. [Маршрутизація шлюзу](#18-маршрутизація-шлюзу)

---

## Позначення

| Символ | Значення |
|--------|---------|
| 🔓 | Автентифікація не потрібна |
| 🔒 | Потрібен JWT Bearer токен |
| `{id}` | Цілочисельний параметр шляху |
| `?param` | Необов'язковий параметр запиту |

---

## 1. Автентифікація

**Файл сервісу:** [Authentication/src/Relativa.Authentication/Endpoints/AuthEndpoints.cs](../Authentication/src/Relativa.Authentication/Endpoints/AuthEndpoints.cs)
**Клієнтський файл:** [Client/src/api/auth.ts](../Client/src/api/auth.ts)
**Базовий шлях:** `/api/v1/auth`

### Ідентифікація та сесія

| Метод | Шлях | Авт. | Опис |
|-------|------|------|------|
| `POST` | `/login` | 🔓 | Автентифікація за обліковими даними |
| `POST` | `/oauth/{provider}` | 🔓 | Вхід через зовнішнього OAuth-провайдера |
| `GET` | `/exists` | 🔓 | Перевірка існування облікового запису за email |
| `POST` | `/register` | 🔓 | Реєстрація нового облікового запису |
| `POST` | `/verify-email` | 🔓 | Підтвердження email-адреси кодом |
| `POST` | `/resend-verification` | 🔓 | Повторне надсилання коду підтвердження |
| `GET` | `/verification-channels` | 🔓 | Список доступних методів підтвердження |

### Відновлення паролю

| Метод | Шлях | Авт. | Опис |
|-------|------|------|------|
| `POST` | `/forgot-password` | 🔓 | Запит на надсилання листа для скидання паролю |
| `GET` | `/reset-password/validate` | 🔓 | Валідація токену скидання паролю |
| `POST` | `/reset-password` | 🔓 | Скидання паролю за допомогою дійсного токену |

### Поточний користувач — Профіль

| Метод | Шлях | Авт. | Опис |
|-------|------|------|------|
| `GET` | `/me` | 🔒 | Отримати профіль поточного користувача |
| `PATCH` | `/me` | 🔒 | Оновити профіль поточного користувача |
| `DELETE` | `/me` | 🔒 | Архівувати (м'яке видалення) поточний обліковий запис |
| `GET` | `/me/settings` | 🔒 | Отримати налаштування користувача |
| `PATCH` | `/me/settings` | 🔒 | Оновити налаштування користувача |

### Поточний користувач — Двофакторна автентифікація

| Метод | Шлях | Авт. | Опис |
|-------|------|------|------|
| `GET` | `/me/2fa` | 🔒 | Отримати статус 2FA |
| `POST` | `/me/2fa/setup` | 🔒 | Розпочати налаштування 2FA (повертає TOTP-секрет/QR) |
| `POST` | `/me/2fa/enable` | 🔒 | Увімкнути 2FA після підтвердження TOTP-коду |
| `POST` | `/me/2fa/disable` | 🔒 | Вимкнути 2FA |
| `POST` | `/me/2fa/backup-codes` | 🔒 | Перегенерувати резервні коди |
| `POST` | `/me/2fa/master-code` | 🔒 | Перегенерувати головний код відновлення |

### Поточний користувач — Керування email-адресами

| Метод | Шлях | Авт. | Опис |
|-------|------|------|------|
| `GET` | `/me/emails` | 🔒 | Список усіх email-адрес облікового запису |
| `POST` | `/me/emails` | 🔒 | Додати нову email-адресу |
| `POST` | `/me/emails/verify` | 🔒 | Підтвердити нещодавно додану email-адресу |
| `POST` | `/me/emails/resend` | 🔒 | Повторно надіслати код підтвердження email |
| `POST` | `/me/emails/primary` | 🔒 | Встановити підтверджену адресу як основну |
| `POST` | `/me/emails/remove` | 🔒 | Видалити неосновну email-адресу |

### Поточний користувач — OAuth-підключення

| Метод | Шлях | Авт. | Опис |
|-------|------|------|------|
| `POST` | `/me/connections/{provider}` | 🔒 | Прив'язати OAuth-провайдера до облікового запису |

---

## 2. Підтримка

**Файл сервісу:** [Authentication/src/Relativa.Authentication/Endpoints/SupportEndpoints.cs](../Authentication/src/Relativa.Authentication/Endpoints/SupportEndpoints.cs)
**Клієнтський файл:** [Client/src/api/support.ts](../Client/src/api/support.ts)
**Базовий шлях:** `/api/v1/support`

| Метод | Шлях | Авт. | Опис |
|-------|------|------|------|
| `POST` | `/contact` | 🔓 | Надіслати повідомлення до служби підтримки |

---

## 3. Організації

**Файли сервісу:**
- [Core/src/Relativa.Core/Endpoints/OrganizationEndpoints.cs](../Core/src/Relativa.Core/Endpoints/OrganizationEndpoints.cs)
- [Core/src/Relativa.Core/Endpoints/OrganizationUserEndpoints.cs](../Core/src/Relativa.Core/Endpoints/OrganizationUserEndpoints.cs)

**Клієнтський файл:** [Client/src/api/organizations.ts](../Client/src/api/organizations.ts)
**Базовий шлях:** `/api/v1/organizations`

| Метод | Шлях | Авт. | Опис |
|-------|------|------|------|
| `POST` | `/` | 🔒 | Створити нову організацію |
| `GET` | `/` | 🔒 | Список організацій поточного користувача |
| `GET` | `/search` | 🔒 | Пошук організацій за назвою |
| `GET` | `/{id}` | 🔒 | Отримати деталі організації |
| `PUT` | `/{id}` | 🔒 | Оновити організацію |
| `GET` | `/{id}/settings` | 🔒 | Отримати налаштування організації |
| `PUT` | `/{id}/settings` | 🔒 | Оновити налаштування організації |
| `POST` | `/{organizationId}/users` | 🔒 | Створити користувача безпосередньо в організації |
| `PATCH` | `/{organizationId}/users/{userId}` | 🔒 | Оновити профіль користувача організації |
| `DELETE` | `/{organizationId}/users/{userId}` | 🔒 | Видалити (жорстке видалення) користувача організації |

### 3.1 Учасники організації

**Файл сервісу:** [Core/src/Relativa.Core/Endpoints/OrgMemberEndpoints.cs](../Core/src/Relativa.Core/Endpoints/OrgMemberEndpoints.cs)

| Метод | Шлях | Авт. | Опис |
|-------|------|------|------|
| `GET` | `/{organizationId}/members` | 🔒 | Список усіх учасників організації |
| `DELETE` | `/{organizationId}/members/{userId}` | 🔒 | Видалити учасника з організації |
| `PUT` | `/{organizationId}/members/{userId}/role` | 🔒 | Змінити роль учасника в організації |

### 3.2 Ролі організації

**Файл сервісу:** [Core/src/Relativa.Core/Endpoints/OrgRoleEndpoints.cs](../Core/src/Relativa.Core/Endpoints/OrgRoleEndpoints.cs)

| Метод | Шлях | Авт. | Опис |
|-------|------|------|------|
| `GET` | `/{organizationId}/roles` | 🔒 | Список ролей у цій організації |
| `POST` | `/{organizationId}/roles` | 🔒 | Створити власну роль організації |
| `PUT` | `/{organizationId}/roles/{roleId}` | 🔒 | Оновити роль організації |
| `DELETE` | `/{organizationId}/roles/{roleId}` | 🔒 | Архівувати роль організації |

### 3.3 Запрошення до організації

**Файл сервісу:** [Core/src/Relativa.Core/Endpoints/OrgInvitationEndpoints.cs](../Core/src/Relativa.Core/Endpoints/OrgInvitationEndpoints.cs)

| Метод | Шлях | Авт. | Опис |
|-------|------|------|------|
| `POST` | `/{organizationId}/invitations` | 🔒 | Запросити користувача до організації |
| `GET` | `/{organizationId}/invitations` | 🔒 | Список активних запрошень |
| `DELETE` | `/{organizationId}/invitations/{invitationId}` | 🔒 | Скасувати запрошення |
| `POST` | `/{organizationId}/invitations/{invitationId}/resend` | 🔒 | Повторно надіслати лист із запрошенням |

### 3.4 Запити на вступ (рівень організації)

**Файл сервісу:** [Core/src/Relativa.Core/Endpoints/JoinRequestEndpoints.cs](../Core/src/Relativa.Core/Endpoints/JoinRequestEndpoints.cs)

| Метод | Шлях | Авт. | Опис |
|-------|------|------|------|
| `POST` | `/{organizationId}/join-requests` | 🔒 | Подати запит на вступ до організації |
| `GET` | `/{organizationId}/join-requests` | 🔒 | Список запитів на вступ (вигляд адміністратора) |
| `PUT` | `/{organizationId}/join-requests/{requestId}` | 🔒 | Схвалити або відхилити запит на вступ |

---

## 4. Робочі простори

**Файл сервісу:** [Core/src/Relativa.Core/Endpoints/WorkspaceEndpoints.cs](../Core/src/Relativa.Core/Endpoints/WorkspaceEndpoints.cs)
**Клієнтський файл:** [Client/src/api/workspaces.ts](../Client/src/api/workspaces.ts)
**Базовий шлях:** `/api/v1/workspaces`

| Метод | Шлях | Авт. | Опис |
|-------|------|------|------|
| `POST` | `/` | 🔒 | Створити новий робочий простір |
| `GET` | `/` | 🔒 | Список робочих просторів (фільтр `?organizationId`) |
| `GET` | `/{id}` | 🔒 | Отримати деталі робочого простору |
| `PUT` | `/{id}` | 🔒 | Оновити робочий простір |
| `DELETE` | `/{id}` | 🔒 | Архівувати робочий простір |
| `GET` | `/{id}/settings` | 🔒 | Отримати налаштування робочого простору |
| `PUT` | `/{id}/settings` | 🔒 | Оновити налаштування робочого простору |

### 4.1 Учасники робочого простору

**Файл сервісу:** [Core/src/Relativa.Core/Endpoints/MemberEndpoints.cs](../Core/src/Relativa.Core/Endpoints/MemberEndpoints.cs)

| Метод | Шлях | Авт. | Опис |
|-------|------|------|------|
| `GET` | `/{workspaceId}/members` | 🔒 | Список учасників робочого простору |
| `POST` | `/{workspaceId}/members` | 🔒 | Додати учасника до робочого простору |
| `PUT` | `/{workspaceId}/members/{userId}/role` | 🔒 | Оновити роль учасника в робочому просторі |
| `DELETE` | `/{workspaceId}/members/{userId}` | 🔒 | Видалити учасника з робочого простору |

### 4.2 Ролі робочого простору

**Файл сервісу:** [Core/src/Relativa.Core/Endpoints/RoleEndpoints.cs](../Core/src/Relativa.Core/Endpoints/RoleEndpoints.cs)

| Метод | Шлях | Авт. | Опис |
|-------|------|------|------|
| `GET` | `/{workspaceId}/roles` | 🔒 | Список ролей у цьому робочому просторі |
| `POST` | `/{workspaceId}/roles` | 🔒 | Створити власну роль робочого простору |
| `PUT` | `/{workspaceId}/roles/{roleId}` | 🔒 | Оновити роль робочого простору |
| `DELETE` | `/{workspaceId}/roles/{roleId}` | 🔒 | Архівувати роль робочого простору |

---

## 5. Дозволи

**Файл сервісу:** [Core/src/Relativa.Core/Endpoints/RoleEndpoints.cs](../Core/src/Relativa.Core/Endpoints/RoleEndpoints.cs)
**Базовий шлях:** `/api/v1/permissions`

| Метод | Шлях | Авт. | Опис |
|-------|------|------|------|
| `GET` | `/` | 🔒 | Список усіх доступних визначень дозволів |

---

## 6. Запрошення (рівень користувача)

**Файл сервісу:** [Core/src/Relativa.Core/Endpoints/InvitationEndpoints.cs](../Core/src/Relativa.Core/Endpoints/InvitationEndpoints.cs)
**Базовий шлях:** `/api/v1/invitations`

| Метод | Шлях | Авт. | Опис |
|-------|------|------|------|
| `POST` | `/accept-org` | 🔒 | Прийняти запрошення до організації |
| `POST` | `/decline-org` | 🔒 | Відхилити запрошення до організації |
| `GET` | `/mine` | 🔒 | Усі активні запрошення поточного користувача |
| `GET` | `/mine/organization` | 🔒 | Активні запрошення до організацій поточного користувача |

---

## 7. Запити на вступ (рівень користувача)

**Файл сервісу:** [Core/src/Relativa.Core/Endpoints/JoinRequestEndpoints.cs](../Core/src/Relativa.Core/Endpoints/JoinRequestEndpoints.cs)
**Базовий шлях:** `/api/v1/join-requests`

| Метод | Шлях | Авт. | Опис |
|-------|------|------|------|
| `GET` | `/mine` | 🔒 | Запити на вступ, подані поточним користувачем |
| `DELETE` | `/mine/{requestId}` | 🔒 | Скасувати запит на вступ |

---

## 8. Сутності

**Файл сервісу:** [Core/src/Relativa.Core/Endpoints/EntityEndpoints.cs](../Core/src/Relativa.Core/Endpoints/EntityEndpoints.cs)
**Клієнтський файл:** [Client/src/api/entities.ts](../Client/src/api/entities.ts)
**Базовий шлях:** `/api/v1/workspaces/{workspaceId}/entities`

| Метод | Шлях | Авт. | Опис |
|-------|------|------|------|
| `GET` | `/` | 🔒 | Список сутностей — підтримує фільтрацію, сортування та пагінацію |
| `GET` | `/{entityId}` | 🔒 | Отримати повні деталі сутності |
| `POST` | `/` | 🔒 | Створити нову сутність |
| `PATCH` | `/{entityId}` | 🔒 | Оновити властивості сутності (часткове оновлення) |
| `DELETE` | `/{entityId}` | 🔒 | Архівувати сутність |

**Оператори фільтрів, що підтримуються в `GET /`:** `eq`, `neq`, `gt`, `lt`, `gte`, `lte`, `contains`, `startsWith`
**Пагінація:** skip / take із загальною кількістю у відповіді

---

## 9. Типи сутностей

**Файл сервісу:** [Core/src/Relativa.Core/Endpoints/EntityTypeEndpoints.cs](../Core/src/Relativa.Core/Endpoints/EntityTypeEndpoints.cs)
**Базовий шлях:** `/api/v1/entity-types`

| Метод | Шлях | Авт. | Опис |
|-------|------|------|------|
| `GET` | `/` | 🔒 | Список усіх типів сутностей із визначеннями властивостей |

---

## 10. Зв'язки між сутностями

**Файл сервісу:** [Core/src/Relativa.Core/Endpoints/EntityRelationshipEndpoints.cs](../Core/src/Relativa.Core/Endpoints/EntityRelationshipEndpoints.cs)
**Базовий шлях:** `/api/v1/workspaces/{workspaceId}/entity-relationships`

| Метод | Шлях | Авт. | Опис |
|-------|------|------|------|
| `POST` | `/` | 🔒 | Створити зв'язок між двома сутностями |
| `PUT` | `/{relationshipId}` | 🔒 | Перепризначити джерело або ціль зв'язку |
| `DELETE` | `/{relationshipId}` | 🔒 | Видалити зв'язок |

---

## 11. Граф сутностей (RPC)

**Файл сервісу:** [Graph/src/Relativa.Graph/EntityGraphEndpoints.cs](../Graph/src/Relativa.Graph/EntityGraphEndpoints.cs)
**Клієнтський файл:** [Client/src/api/entityGraph.ts](../Client/src/api/entityGraph.ts)
**Базовий шлях:** `/api/v1/workspaces/{workspaceId}/entity-graph`

| Метод | Шлях | Авт. | Опис |
|-------|------|------|------|
| `POST` | `/create` | 🔒 | Створити сутність через Graph RPC (комбіноване створення) |

---

## 12. Запит до графу

**Файл сервісу:** [Graph/src/Relativa.Graph/Graph/GraphQueryEndpoints.cs](../Graph/src/Relativa.Graph/Graph/GraphQueryEndpoints.cs)
**Клієнтський файл:** [Client/src/api/graph.ts](../Client/src/api/graph.ts)
**Базовий шлях:** `/api/v1/graph`

| Метод | Шлях | Авт. | Параметри запиту | Опис |
|-------|------|------|-----------------|------|
| `GET` | `/` | 🔒 | `organizationId` *(обов'язково)*, `?riskLevel` (high \| medium \| low) | Отримати вузли та ребра графу |

---

## 13. Дашборд — Організація

**Файл сервісу:** [Graph/src/Relativa.Graph/Dashboard/DashboardEndpoints.cs](../Graph/src/Relativa.Graph/Dashboard/DashboardEndpoints.cs)
**Клієнтський файл:** [Client/src/api/dashboard.ts](../Client/src/api/dashboard.ts)
**Базовий шлях:** `/api/v1/dashboard`

> Усі ендпоінти потребують параметр запиту `?organizationId`.

| Метод | Шлях | Авт. | Опис |
|-------|------|------|------|
| `GET` | `/summary` | 🔒 | Зведені KPI на рівні організації |
| `GET` | `/pipeline` | 🔒 | Воронка угод |
| `GET` | `/risk-distribution` | 🔒 | Розподіл ризиків сутностей |
| `GET` | `/trends` | 🔒 | Тренди за 6 місяців |
| `GET` | `/top-entities` | 🔒 | Топ угод і клієнтів |
| `GET` | `/workspaces-comparison` | 🔒 | Порівняння KPI між робочими просторами |

---

## 14. Дашборд — Робочий простір

**Файл сервісу:** [Graph/src/Relativa.Graph/Dashboard/WorkspaceDashboardEndpoints.cs](../Graph/src/Relativa.Graph/Dashboard/WorkspaceDashboardEndpoints.cs)
**Клієнтський файл:** [Client/src/api/workspaceDashboard.ts](../Client/src/api/workspaceDashboard.ts)
**Базовий шлях:** `/api/v1/dashboard/workspace/{workspaceId}`

| Метод | Шлях | Авт. | Опис |
|-------|------|------|------|
| `GET` | `/summary` | 🔒 | Зведені KPI робочого простору |
| `GET` | `/pipeline` | 🔒 | Воронка угод робочого простору |
| `GET` | `/risk-distribution` | 🔒 | Розподіл ризиків у робочому просторі |
| `GET` | `/trends` | 🔒 | Тренди робочого простору за 6 місяців |
| `GET` | `/top-entities` | 🔒 | Топ угод і клієнтів робочого простору |
| `GET` | `/member-activity` | 🔒 | Статистика активності учасників |

---

## 15. Журнал аудиту

**Файл сервісу:** [Audit/src/Relativa.Audit/Endpoints/AuditEndpoints.cs](../Audit/src/Relativa.Audit/Endpoints/AuditEndpoints.cs)
**Клієнтський файл:** [Client/src/api/audit.ts](../Client/src/api/audit.ts)

| Метод | Шлях | Авт. | Опис |
|-------|------|------|------|
| `GET` | `/audit-log` | 🔒 | Пагінований глобальний журнал аудиту |
| `GET` | `/entities/{entityId}/audit-log` | 🔒 | Журнал аудиту в межах конкретної сутності |

**Підтримувані параметри запиту:**

| Параметр | Опис |
|----------|------|
| `entity_type` | Фільтр за типом сутності |
| `scope` | Фільтр масштабу журналу |
| `date_from` / `from` | Початок діапазону дат |
| `date_to` / `to` | Кінець діапазону дат |
| `action` | Фільтр за типом дії |
| `index` | Номер сторінки |
| `page_size` | Розмір сторінки |
| `entity_id` | Фільтр за сутністю |
| `targetId` | Фільтр за ціллю |
| `domain_entity_type` | Тип сутності на рівні домену |
| `workspace_id` | Фільтр за робочим простором |
| `organization_id` | Фільтр за організацією |
| `actor_user_id` | Фільтр за користувачем-ініціатором |
| `target_user_id` | Фільтр за цільовим користувачем |

---

## 16. ML-скорінг

**Клієнтський файл:** [Client/src/api/ml.ts](../Client/src/api/ml.ts)
**Базовий шлях:** `/api/ml`

| Метод | Шлях | Авт. | Опис |
|-------|------|------|------|
| `POST` | `/score/batch` | 🔒 | Пакетний скорінг сутностей: ймовірність закриття угоди / відтоку клієнта |

---

## 17. Хаби реального часу (SignalR)

**Клієнтські файли:**
- [Client/src/api/graphHub.ts](../Client/src/api/graphHub.ts)
- [Client/src/api/coreHub.ts](../Client/src/api/coreHub.ts)

| URL хабу | Клієнтський файл | Призначення |
|----------|-----------------|-------------|
| `/graph/hubs/graph` | `graphHub.ts` | Оновлення вузлів та ребер графу в реальному часі |
| `/core/hubs/core` | `coreHub.ts` | Оновлення сутностей та основних даних у реальному часі |

> З'єднання SignalR потребує дійсного JWT-токену, переданого під час рукостискання (handshake).

---

## 18. Маршрутизація шлюзу

**Файл:** [Gateway/src/Relativa.Gateway/OpenApi/AggregatedOpenApiEndpoint.cs](../Gateway/src/Relativa.Gateway/OpenApi/AggregatedOpenApiEndpoint.cs)

API-шлюз є єдиною точкою входу для всього зовнішнього трафіку. Він:

1. **Перевіряє** JWT Bearer токени для кожного захищеного маршруту
2. **Додає** заголовки `X-User-Id` та `X-User-Email` перед пересиланням
3. **Маршрутизує** запити до відповідного мікросервісу:

| Префікс шляху | Мікросервіс |
|---------------|------------|
| `/auth/*` | Сервіс автентифікації |
| `/core/*` | Основний сервіс |
| `/graph/*` | Сервіс графу |
| `/audit/*` | Сервіс аудиту |
| `/ml/*` | Сервіс ML-скорінгу |

---

## Підсумок

| Домен | Ендпоінти | Без авт. |
|-------|-----------|---------|
| Автентифікація | 28 | 10 |
| Підтримка | 1 | 1 |
| Організації | 10 | 0 |
| Учасники організації | 3 | 0 |
| Ролі організації | 4 | 0 |
| Запрошення до організації | 4 | 0 |
| Запити на вступ (орг.) | 3 | 0 |
| Робочі простори | 7 | 0 |
| Учасники робочого простору | 4 | 0 |
| Ролі робочого простору | 4 | 0 |
| Дозволи | 1 | 0 |
| Запрошення (користувач) | 4 | 0 |
| Запити на вступ (користувач) | 2 | 0 |
| Сутності | 5 | 0 |
| Типи сутностей | 1 | 0 |
| Зв'язки між сутностями | 3 | 0 |
| Граф сутностей RPC | 1 | 0 |
| Запит до графу | 1 | 0 |
| Дашборд (організація) | 6 | 0 |
| Дашборд (робочий простір) | 6 | 0 |
| Журнал аудиту | 2 | 0 |
| ML-скорінг | 1 | 0 |
| **Разом** | **101** | **11** |
