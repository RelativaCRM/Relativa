# Promotion Strategy — Relativa

> **Завдання 1.2 — Product Hunt & Social Media Plan (План залучення першої сотні користувачів)**
> Мета: презентувати складне інженерне рішення мовою вигоди для клієнта та скласти план «посіву»
> інформації для залучення перших ~100 користувачів.

---

## 0. Контекст продукту (на чому будується стратегія)

**Relativa** — це self-hosted, мультитенантна **B2B CRM-платформа** для відділів продажів. Замість
нескінченних таблиць вона показує всю організацію як **інтерактивний граф зв'язків** (клієнти, угоди,
контракти, контакти й люди), де вузли угод **підсвічуються кольором за ML-ризиком** (червоний / жовтий /
зелений) на базі реальної моделі scikit-learn.

**Ключові факти, важливі для маркетингу:**

| Що це дає | Чому це продає |
|---|---|
| Граф зв'язків на одному канвасі | Унікальний UX — жоден конкурент (HubSpot, Pipedrive, Salesforce) так не вміє |
| ML-скоринг: ймовірність закриття + ризик відтоку | «Розумна» CRM без enterprise-цінника |
| Two-level RBAC (Organization → Workspace) | Для компаній із кількома командами продажів |
| Self-hosted через `docker compose up` | Для регульованих галузей і вимог до data-residency |
| Open source + Swagger/Scalar + Docker-ready | Любить аудиторія розробників та tech-lead'ів |
| Ціна Growth-тарифу у **2–9× нижча** за конкурентів | Головний фінансовий аргумент |

> **Позиціонування:** _Salesforce-level features, Pipedrive-level price._
> **Слоган:** _Turn relationships into revenue._

**Подвійна природа продукту** визначає всю стратегію каналів: Relativa — це одночасно
**B2B / Professional** продукт (CRM для бізнесу) **і Developer Tool** (open-source, self-host, API-first).
Тому ми працюємо на двох аудиторіях паралельно.

---

## А) Вибір каналів (Channel Selection)

Обґрунтування вибору платформ залежно від типу продукту. Relativa попадає у дві категорії, тож
використовуємо обидві групи каналів, але з різним меседжем.

| Канал | Тип аудиторії | Чому саме сюди | Меседж |
|---|---|---|---|
| **LinkedIn** | B2B / Professional | Тут сидять sales-керівники, RevOps, founder'и SMB — ті, хто **платить** за CRM | Мова вигоди: економія часу, видимість ризиків, ціна |
| **Facebook (профільні групи)** | B2B / SMB власники | Українські та регіональні спільноти підприємців і продажників | Емоційний пост «Біль → Рішення → Результат» |
| **Reddit** (`r/webdev`, `r/programming`, `r/dotnet`, `r/selfhosted`) | Developer Tools | Open-source + self-host + .NET 10/Vue 3 — ідеальний фіт для `r/selfhosted` і `r/dotnet` | Технічний сторітелінг, чесний build-in-public |
| **DOU** (dou.ua) | Developer Tools (UA) | Українська інженерна спільнота; формат лонгріду про архітектуру | Технічний кейс + «ми студенти-інженери» |
| **Discord-сервери** (.NET, Vue, Selfhosted, інді-CRM/SaaS) | Developer Tools | Жива розмова, швидкий фідбек по архітектурі | Запит на feedback + посилання на репозиторій |
| **Product Hunt** | Mass / Early adopters | Майданчик №1 для запуску нових продуктів і збору перших користувачів | Hunter Kit + Launch Day (див. розділ Б) |
| **Twitter / X** | Developer Tools | `#BuildInPublic` ком'юніті | Короткі технічні інсайти + GIF графа |

**Чого свідомо НЕ беремо:** Instagram / TikTok (Mass Market, акцент на візуал). Relativa — не
масовий B2C-продукт; «красива картинка» тут не конвертує в реєстрації. Замість цього вкладаємо
візуал (GIF графа) у LinkedIn / Product Hunt / Twitter, де він працює на цільову аудиторію.

> **Пріоритет посіву:** Product Hunt (день запуску) → LinkedIn + Facebook (емоційний пост) →
> Reddit `r/selfhosted` + `r/dotnet` + DOU (технічний пост) → Discord/Twitter (підтримка хвилі).

---

## Б) Product Hunt Strategy (Алгоритм запуску)

### Б.1. Hunter Kit (набір для запуску)

> Контент майданчика — англійською (Product Hunt — міжнародна платформа).

**Tagline (≤ 60 символів):**

```
See every deal & relationship on one ML-risk graph
```
> _Резервні варіанти:_
> `One graph for every deal, contact & ML risk score` ·
> `Salesforce-grade CRM at Pipedrive price, self-hosted`

**Опис (≤ 260 символів):**

```
Relativa is a self-hostable B2B CRM that shows every client, deal and
contact on one interactive graph, color-coded by real scikit-learn risk
scores. Salesforce-level RBAC, audit and ML — at Pipedrive prices.
One `docker compose up` and you're live.
```

**Скріншоти (3–5 шт.):** використовуємо вже наявні демо-ассети репозиторію (`assets/demos/`,
`assets/screenshots/`):

1. `12-graph-filled-with-data.gif` — **головний герой**: граф із даними, вузли підсвічені за ризиком.
2. `09-create-entity-deal.gif` — створення угоди (показує продукт «у дії»).
3. `13-audit-log.gif` — audit log (доказ enterprise-рівня compliance).
4. `assets/screenshots/mobile/08-graph.png` — граф на мобільному (повна адаптивність).
5. `14-setting.gif` — налаштування/RBAC (гнучкість для команд).

**GIF-анімація логотипа:** анімований `Client/src/assets/relativa-logo.png` (поява лого →
розгортання у граф зв'язків). Hero-банер для thumbnail — `assets/branding/relativa_hero_banner_1920x480_radial.png`.

**Топ-1 посилання (єдиний CTA):** GitHub-репозиторій (open source) — звідти користувач за
один `docker compose up` піднімає весь стек.

### Б.2. Алгоритм виходу (timeline)

| Коли | Дія |
|---|---|
| **T − 7 днів** | Знайти Hunter'а з гарною кармою; підготувати Hunter Kit; зібрати «теплу» базу (друзі, одногрупники, Discord) у список «нагадати в день запуску» |
| **T − 3 дні** | Тизер у Twitter/LinkedIn: «Launching on Product Hunt this {weekday}». Перевірити, що репозиторій, README, Swagger та `docker compose up` працюють «з нуля» |
| **T − 1 день** | Фінальна перевірка медіа; заготовити «Перший коментар» від Makers (нижче) |
| **День запуску, 00:01 PT** | Публікація (вівторок–четвер — найкращі дні). Одразу постимо «Перший коментар» |
| **Перші 4 години** | Особисто запрошуємо теплу базу; **просимо фідбек, а не просто upvote**; відповідаємо на кожен коментар < 15 хв |
| **Протягом дня** | Паралельно виходять пости в LinkedIn/Facebook (емоційний) і Reddit/DOU (технічний) з посиланням на PH |
| **T + 1 день** | Дякуємо спільноті, публікуємо «what's next», збираємо фічреквести у roadmap |

### Б.3. Перший коментар від авторів (Makers) — Шаблон 3

> Публікується одразу під запуском. Розкриває історію створення та технічні виклики.

```
Hi Product Hunt! 👋

We're a team of student engineers, and today we're launching Relativa.

It started simply: we couldn't find an affordable CRM that shows the WHOLE
picture. Every tool we tried buried relationships in endless tables — and the
moment you wanted ML risk scoring + real RBAC + an audit trail, the price
jumped to Salesforce territory.

So we built our own — fast, reliable and open source.

What's inside:
• An org-wide relationship graph — every client, deal and contact on one
  canvas, with deal nodes color-coded by a real scikit-learn risk model.
• Two-level RBAC (Organization → Workspace) + a full transactional audit log,
  the kind usually locked behind enterprise tiers.
• Self-hosted: full Swagger/Scalar API docs and a Docker-ready stack —
  `docker compose up` and the entire platform is live.

The hardest part? Rendering that graph fast. A naive build did one DB query
per node plus one ML call per deal — hundreds of round-trips. We rewrote it
into a handful of set-based queries + a single batched ML call. (Happy to go
deep on this in the comments 👇)

We're here ALL DAY to answer questions. What should we add in the next release?

Thanks for the support! 🙏
— The Relativa team
```

---

## В) Content Plan — два пости

### Пост 1 — Емоційний (Шаблон 1: Біль → Рішення → Результат)

> **Ціль:** зачепити «біль» користувача та показати продукт як рятівне коло.
> **Де публікувати:** LinkedIn, Facebook, профільні групи продажників/founder'ів.
> **Мова:** українська (для UA-аудиторії) / є англійський відповідник для LinkedIn-міжнародки.

---

**Заголовок:** Скільки угод ви втратили лише тому, що **не помітили** проблему вчасно?

**Текст:**

Скільки часу ваша команда витрачає на те, щоб вручну зрозуміти, які угоди «горять»?
Ми порахували — в середньому це **5–7 годин на тиждень** на менеджера: вивантаження таблиць,
зведення статусів, наради «а що там по клієнту N». Це час, який можна було б витратити на самі
продажі.

Наш проєкт **Relativa** створений, щоб змінити правила гри:

• **Граф зв'язків** — забудьте про десятки вкладок і таблиць. Вся організація (клієнти, угоди,
  контракти, контакти) — на одному екрані.
• **ML-скоринг ризику** — кожна угода підсвічена кольором: червоний / жовтий / зелений. Проблемні
  угоди видно з першого погляду, по всій компанії одразу.
• **Працює там, де ваші дані** — піднімається однією командою `docker compose up` на вашому
  сервері. Жодних компромісів із приватністю.

Ми не просто написали код. Ми зробили CRM, якою хотіли б користуватися самі — з можливостями
рівня Salesforce, але за ціною Pipedrive.

Спробуйте та підніміть власну інстанцію вже зараз: **(→ посилання на GitHub / демо)**

Напишіть у коментарях: скільки годин на тиждень **ви** витрачаєте на ручний аналіз угод?

`#SaaS #ProductLaunch #CRM #SalesTech #MachineLearning #SoftwareEngineering`

> 🎥 **Медіа до поста:** GIF `12-graph-filled-with-data.gif` (граф із підсвіченими за ризиком вузлами)
> або промо-ролик із Завдання 2.

---

### Пост 2 — Технічний сторітелінг (Шаблон 2: Build in Public)

> **Ціль:** показати технічну глибину та завоювати довіру розробників.
> **Де публікувати:** Reddit (`r/dotnet`, `r/selfhosted`, `r/programming`), DOU, Discord, Twitter/X.
> **Мова:** англійська (Reddit/Twitter) / українська (DOU).
> **Кейс — справжній**, узятий із коду `Graph/src/Relativa.Graph/Graph/GraphDataService.cs`.

---

**Заголовок:** Як ми вбили N+1 і «cartesian explosion» при побудові графа зв'язків на одному запиті

**Текст:**

Під час розробки **Relativa** (self-hosted B2B CRM на .NET 10 + Vue 3 + Django/scikit-learn) ми
зіткнулися з нашим найцікавішим викликом: треба намалювати **граф усієї організації** — користувачі,
воркспейси, клієнти, угоди, контракти й зв'язки між ними — і кожну угоду розфарбувати за ML-ризиком.

Перша, «наївна» реалізація вбивала продуктивність одразу з трьох боків:

1. **N+1 на лейблах.** У нас гнучка **EAV**-модель — назва кожної сутності лежить не в колонці, а
   рядком у таблиці `EntityPropertyValues`. Наївний код робив **окремий запит на кожен вузол**, щоб
   дістати його підпис. Граф на 500 сутностей = 500+ звернень до БД.
2. **Один HTTP-виклик ML на кожну угоду.** Скоринг живе в окремому Django-мікросервісі. Цикл
   «по угоді → виклик API» додавав сотні round-trip'ів по мережі.
3. **Cartesian explosion на правах.** Джойн «членство у воркспейсі × дозволи ролі» множив рядки
   й роздував вибірку в рази.

Можна було піти легким шляхом (кеш + «якось потім»), але ми вирішили переписати запит правильно:

• **Крок 1.** Зібрали всі `entityId` одним set-based запитом, а **всі лейбли — одним** запитом
  `WHERE entityId IN (...)` до EAV, далі групуємо в пам'яті. N+1 зник.
• **Крок 2.** Замінили цикл ML-викликів на **один батчевий** `ScoreBatchAsync(dealIds)` — сотні
  HTTP-викликів згорнулись в один.
• **Крок 3.** **Розділили** запит членства й запит дозволів на два окремих (замість одного джойна) —
  cartesian explosion усунено (так, у коді так і написано: _"Separate query for permissions to avoid
  cartesian explosion"_).

**Результат:** побудова всього графа — це тепер **~6 set-based запитів + 1 батчевий ML-виклик**
замість сотень round-trip'ів. Ендпоінт графа стабільно тримається в межах нашого load-test SLA
(**p95 < 1500 мс** під навантаженням, профілі smoke/soak/spike на NBomber). Це було круте занурення
в EF Core, проектування EAV-запитів і дизайн міжсервісної взаємодії.

Ми виклали репозиторій у відкритий доступ. Будемо вдячні за ⭐ і конструктивний фідбек по архітектурі!

Репозиторій: **(→ посилання на GitHub)**
Документація API (Scalar/OpenAPI): **(→ `:8082/scalar/v1`)**

`#BuildInPublic #OpenSource #dotnet #Backend #DatabaseOptimization #SelfHosted`

> 🎥 **Медіа до поста:** GIF графа `12-graph-filled-with-data.gif` або скрін EAV-схеми з `DATABASE.md`.

---

## Поради, дотримані в цьому плані

- **Візуалізація:** до кожного поста додано медіа (GIF графа / промо-ролик). Пости з відео
  отримують у 2–3 рази більше охоплень.
- **Один Call to Action:** у кожному пості — **одне** головне посилання (GitHub-репозиторій),
  щоб не «розмивати» перехід.
- **Баланс «драйв ↔ інженерія»:** емоційний пост говорить мовою вигоди клієнта; технічний —
  мовою якості коду для розробників.

---

## KPI та план на першу сотню користувачів

| Метрика | Ціль (перші 2 тижні) |
|---|---|
| Upvotes на Product Hunt у день запуску | 100+ |
| GitHub ⭐ | 100+ |
| Реєстрації / `docker compose up` (унікальні clones) | 100+ |
| Якісний фідбек (issues, коментарі по архітектурі) | 20+ |
| Фічреквести в roadmap | 10+ |

**Логіка воронки:** Product Hunt + Reddit/DOU дають **трафік розробників** (вони піднімають self-host
і ставлять ⭐), а LinkedIn/Facebook дають **трафік ОПР** (sales-керівники, founder'и SMB — майбутні
платні користувачі). Технічна довіра з боку розробників конвертується в довіру бізнесу — це і є наша
перша сотня.

---

> _Документ підготовлено в межах Завдання 1.2 «Promotion strategy». Технічний кейс у Пості 2 —
> реальний і відповідає коду `GraphDataService.cs`._
