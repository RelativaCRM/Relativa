# Relativa CRM — Функції та переваги

![Version](https://img.shields.io/badge/Version-2.0.0-0078D4) ![Status](https://img.shields.io/badge/Status-Stable-22C55E) ![License](https://img.shields.io/badge/License-MIT-F59E0B) ![Tests](https://img.shields.io/badge/Tests-Passing-22C55E?logo=githubactions&logoColor=white) ![Coverage](https://img.shields.io/badge/Coverage-85%25-EAB308)

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white) ![Vue.js](https://img.shields.io/badge/Vue.js-3.5-4FC08D?logo=vuedotjs&logoColor=white) ![TypeScript](https://img.shields.io/badge/TypeScript-5-3178C6?logo=typescript&logoColor=white) ![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?logo=postgresql&logoColor=white) ![SignalR](https://img.shields.io/badge/SignalR-10-0078D4?logo=microsoft&logoColor=white) ![Vite](https://img.shields.io/badge/Vite-5-646CFF?logo=vite&logoColor=white) ![Chart.js](https://img.shields.io/badge/Chart.js-4-FF6384?logo=chartdotjs&logoColor=white)

---

> **CRM, який каже команді що робити далі — а не лише що вже сталося.**

---

## 🤖 Аналітика продажів на основі AI

*Технологія: `POST /api/ml/score/batch` — окремий ML-мікросервіс, що повертає ймовірність закриття угоди та ризик відтоку клієнта для кожного запису*

- 🎯 **Фокус на тому, що закривається** — Relativa автоматично ранжує кожну угоду за ймовірністю закриття, тож команда припиняє здогадуватись і діє з потрібними записами сьогодні
- 📉 **Зупиніть відтік до того, як він стався** — вбудований скорінг ризику клієнта виявляє акаунти під загрозою до того, як вони замовкнуть, а не після того, як підуть
- ⚡ **Нуль часу на аналіз** — ML працює у фоні; продавці бачать пріоритизований список, а не сиру базу даних, яку треба інтерпретувати самотужки

---

## 🌐 Мережевий граф у реальному часі

*Технологія: SignalR WebSocket-хаби (`/graph/hubs/graph`, `/core/hubs/core`) + атомарне створення складених сутностей (`POST /entity-graph/create`) + запит до графу з фільтром ризику (`GET /graph/`)*

- 👁️ **Бачте всю мережу клієнтів наживо** — кожний зв'язок і кожне оновлення миттєво відображаються для всієї команди без жодного оновлення сторінки
- ⚠️ **Помічайте ризик у момент появи** — відфільтруйте граф за рівнем ризику (high / medium / low), і проблемні ділянки підсвітяться на екрані в реальному часі
- 🤝 **Справжня командна співпраця** — коли колега додає угоду або пов'язує контакт, екран кожного члена команди відображає це за мілісекунди

---

## 🔐 Гранульований контроль доступу + повний журнал аудиту

*Технологія: 8 ендпоінтів управління власними ролями на рівні організації та робочого простору + Audit Log з 13 параметрами фільтрації (ініціатор, ціль, діапазон дат, робочий простір, тип дії тощо)*

- 🛡️ **Точні дозволи, жодного зайвого доступу** — визначте, що саме кожна роль може бачити та робити; призначайте на рівні організації або для окремого робочого простору
- 📋 **Кожна дія — назавжди зафіксована** — хто, що змінив, коли і з якого робочого простору — повний пошук за два кліки
- ✅ **Готовність до compliance із коробки** — проходьте перевірки безпеки без паніки; фільтруйте журнал аудиту за датою, користувачем або сутністю за лічені секунди

---

## Технічна довідка

*Для наступного етапу документації.*

| Функція | API-ендпоінт | Сервіс |
|---------|-------------|--------|
| ML-скорінг угод + відтоку | `POST /api/ml/score/batch` | ML Service |
| Візуалізація графу | `GET /api/v1/graph/` | Graph Service |
| Комбіноване створення в графі | `POST /api/v1/workspaces/{id}/entity-graph/create` | Graph Service |
| Оновлення графу в реальному часі | `/graph/hubs/graph` (SignalR) | Graph Service |
| Оновлення сутностей у реальному часі | `/core/hubs/core` (SignalR) | Core Service |
| Глобальний журнал аудиту | `GET /audit-log` | Audit Service |
| Журнал аудиту сутності | `GET /entities/{id}/audit-log` | Audit Service |
| Власні ролі організації | `POST /api/v1/organizations/{id}/roles` | Core Service |
| Власні ролі робочого простору | `POST /api/v1/workspaces/{id}/roles` | Core Service |
| Дашборд організації | `GET /api/v1/dashboard/summary` (+ 5 ендпоінтів) | Graph Service |
| Дашборд робочого простору | `GET /api/v1/dashboard/workspace/{id}/summary` (+ 5 ендпоінтів) | Graph Service |
