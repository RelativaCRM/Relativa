<!-- =========================================================================
     HERO BANNER
     Frontend: replace the placeholder below with the rendered hero banner
     (recommended 2560×640, exported to assets/branding/hero-banner.png).
     ========================================================================= -->
<p align="center">
  <!-- PLACEHOLDER: Hero banner — assets/branding/hero-banner.png -->
  <img src="assets/branding/hero-banner.png" alt="Relativa — Turn relationships into revenue" width="100%">
</p>

<h1 align="center">Relativa</h1>

<p align="center">
  <strong>The CRM where you see every deal and every relationship on one graph — painted with real ML risk — for a fraction of the price of Salesforce.</strong>
</p>

<p align="center">
  <em>Turn relationships into revenue.</em>
</p>

<!-- =========================================================================
     TECH BADGES (Shields.io) — palette matched to brand blue (#2F5BEA)
     Backend: regenerate if versions change.
     ========================================================================= -->
<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-2F5BEA?style=flat-square&logo=dotnet&logoColor=white" alt=".NET 10">
  <img src="https://img.shields.io/badge/Vue.js-3-2F5BEA?style=flat-square&logo=vuedotjs&logoColor=white" alt="Vue 3">
  <img src="https://img.shields.io/badge/Django-5.1-2F5BEA?style=flat-square&logo=django&logoColor=white" alt="Django 5.1">
  <img src="https://img.shields.io/badge/PostgreSQL-16-2F5BEA?style=flat-square&logo=postgresql&logoColor=white" alt="PostgreSQL 16">
  <img src="https://img.shields.io/badge/RabbitMQ-3.13-2F5BEA?style=flat-square&logo=rabbitmq&logoColor=white" alt="RabbitMQ 3.13">
  <img src="https://img.shields.io/badge/Docker-Compose-2F5BEA?style=flat-square&logo=docker&logoColor=white" alt="Docker Compose">
  <img src="https://img.shields.io/badge/scikit--learn-ML-2F5BEA?style=flat-square&logo=scikitlearn&logoColor=white" alt="scikit-learn">
</p>

<p align="center">
  <img src="https://img.shields.io/badge/license-TBD-lightgrey?style=flat-square" alt="License TBD">
  <img src="https://img.shields.io/badge/status-MVP-success?style=flat-square" alt="Status: MVP">
  <img src="https://img.shields.io/badge/PRs-welcome-2F5BEA?style=flat-square" alt="PRs welcome">
</p>

---

## Table of Contents

- [What is Relativa?](#what-is-relativa)
- [Why Relativa? (What makes us different)](#why-relativa-what-makes-us-different)
- [Features](#features)
- [Screenshots & Demos](#screenshots--demos)
- [Tech Stack](#tech-stack)
- [Architecture](#architecture)
- [Quick Start](#quick-start)
- [Environment Configuration](#environment-configuration)
- [Database & Seed Data](#database--seed-data)
- [API Documentation](#api-documentation)
- [Service & Port Reference](#service--port-reference)
- [Documentation](#documentation)
- [Roadmap](#roadmap)
- [Contributing](#contributing)
- [License](#license)

---

## What is Relativa?

**Relativa** is a self-hostable, multi-tenant **B2B CRM platform** for sales teams that need structured pipelines, granular access control, and predictive analytics — all in one place.

Instead of forcing managers to read through endless tables, Relativa renders the whole organization as an **interactive relationship graph**: clients, deals, contracts, contacts, and the people behind them, connected on a single canvas and color-coded by **machine-learning risk**. One glance shows you which deals are in trouble across the entire company.

Built for **small and mid-sized B2B companies** that manage a portfolio of clients across multiple teams, Relativa replaces hours of manual analysis with a visual, data-driven way of working.

> **Positioning:** _Salesforce-level features, Pipedrive-level price._

---

## Why Relativa? (What makes us different)

Every feature below is **already implemented in Relativa** but missing — or locked behind expensive enterprise tiers — in HubSpot, Pipedrive, and Salesforce.

| 🔵 Unique advantage | The real benefit |
|---|---|
| **Org-level relationship graph** | See *who is connected to whom* across the entire organization on one canvas — no clicking through records. Critical for complex B2B deals with many stakeholders. |
| **Combined ML closure + churn scoring** | Every deal gets a closure-probability **and** a churn-risk score from a real scikit-learn model — not rule-based guesswork. When a score can't be computed, Relativa tells you *exactly which data is missing*. |
| **Risk-colored graph nodes** | Deals on the graph light up by ML risk — red / amber / green. Spot problem deals across the whole org at a glance. A UX paradigm no competitor offers. |
| **Two-level RBAC (Organization → Workspace)** | One user, different roles in different workspaces. Perfect for companies with several sales teams that should each see only their own data. |
| **Flexible EAV data model** | Add new entity types, properties, and relationships by configuration — no schema migrations, no code. Adapt Relativa to any industry. |
| **Full transactional audit log** | Every state change is captured (with old / new values and author) via a transactional outbox → RabbitMQ → dedicated audit tables. GDPR-ready compliance out of the box. |
| **Self-hosted via Docker Compose** | The full stack comes up with a single `docker compose up`. Ideal for regulated industries and strict data-residency requirements where SaaS-only competitors simply can't go. |

💡 **Bottom line:** Relativa Growth-tier features cost **2–9× less** than competitor plans that offer a comparable RBAC + Audit + ML feature set.

---

## Features

### 🕸️ Relationship Graph
- Interactive, RBAC-filtered network of all users, workspaces, clients, deals, contracts, and their links.
- Click any node to **view, edit, or archive** the record directly from the canvas.
- Filter by **risk level, manager, workspace, or entity type**; a live counter shows visible vs. total nodes.
- Deal nodes are **colored by ML risk**, with a legend generated dynamically.

### 🤖 ML Risk Scoring
- Batch **closure-probability** and **churn-risk** scoring powered by scikit-learn models in a Python/Django microservice.
- Structured **"why is this score unavailable"** explanations so reps know what to fill in.
- Per-workspace, configurable risk thresholds.

### 🔐 Role-Based Access Control
- Separate, granular permissions at the **organization** and **workspace** levels.
- System roles (`org_owner`, `org_admin`, `org_member`, `ws_admin`, `ws_manager`, `ws_analyst`, `ws_member`) plus fully **custom roles**.
- Invitations, join-request flow, and member management.

### 🧩 Flexible Entities (EAV)
- Clients, deals, contracts, contacts, tasks, notes — all driven by a runtime-configurable schema.
- Custom properties, allowed-value validation, and typed relationships between entity types.

### 📒 Audit & Compliance
- Transactional **outbox pattern** guarantees every change is recorded in the same DB transaction.
- Four domain-specific audit logs (entity / workspace / organization / user) with a RBAC-filtered read API.

### 🏢 Multi-Tenant Workspaces
- Organizations contain isolated workspaces; data never leaks across team boundaries.

### ⚡ Real-Time Updates
- SignalR pushes live graph updates as data changes.

---

## Screenshots & Demos

> **Frontend / QA:** drop the optimized media into `assets/` and swap the placeholders below.
> Capture clean demo data (no `asdf` / `test` records) and check the console for errors before recording.

### 🎬 Relationship Graph in action
<!-- PLACEHOLDER: GIF — assets/demos/graph-walkthrough.gif -->
<p align="center">
  <img src="assets/demos/graph-walkthrough.gif" alt="Navigating and filtering the relationship graph" width="80%">
</p>

### 🎬 Creating a deal & reading ML scores
<!-- PLACEHOLDER: GIF — assets/demos/deal-scoring.gif -->
<p align="center">
  <img src="assets/demos/deal-scoring.gif" alt="Creating a deal and viewing closure / churn scores" width="80%">
</p>

### 🖼️ Key screens

| Dashboard | Entity detail + Scores | Audit log |
|:---:|:---:|:---:|
| <!-- PLACEHOLDER --> ![Dashboard](assets/screenshots/dashboard.png) | <!-- PLACEHOLDER --> ![Entity detail](assets/screenshots/entity-detail.png) | <!-- PLACEHOLDER --> ![Audit log](assets/screenshots/audit-log.png) |

---

## Tech Stack

| Layer | Technology | Version |
|---|---|---|
| **Backend services** (Authentication, Core, Gateway, Graph, Audit, Migration) | .NET / ASP.NET Core | **10.0** |
| **API Gateway** | YARP reverse proxy | .NET 10 |
| **ML service** | Python · Django REST Framework · scikit-learn | Python **3.12** · Django **5.1** |
| **Frontend SPA** | Vue 3 + Vite | Node.js **22** |
| **Database** | PostgreSQL | **16** (Alpine) |
| **Message broker** | RabbitMQ | **3.13** (Management) |
| **Real-time** | SignalR | .NET 10 |
| **DB admin** | pgAdmin 4 | **8.x** |
| **Mail capture (dev)** | MailHog | latest |
| **Orchestration** | Docker Compose | Compose 2.x |

---

## Architecture

Relativa is a **microservice monorepo**. A YARP gateway is the single entry point; backend services communicate asynchronously over RabbitMQ, and all audit events flow through a transactional outbox.

```
                              ┌─────────────────┐
                              │   Vue 3 SPA      │  :3000
                              │   (Client)       │
                              └────────┬─────────┘
                                       │  HTTPS / JWT
                              ┌────────▼─────────┐
                              │  Gateway (YARP)  │  :8080
                              └───┬───┬───┬───┬──┘
              ┌───────────────────┘   │   │   └───────────────────┐
     ┌────────▼───────┐   ┌───────────▼┐ ┌▼───────────┐  ┌────────▼───────┐
     │ Authentication │   │    Core    │ │   Graph    │  │      ML        │
     │     :8081      │   │   :8082    │ │   :8083    │  │ (Django) :8084 │
     └────────┬───────┘   └─────┬──────┘ └─────┬──────┘  └────────┬───────┘
              │                 │              │ SignalR          │
              │           ┌─────▼──────────────▼──────┐           │
              │           │        RabbitMQ            │◀──────────┘
              │           │   (domain + audit events)  │
              │           └─────────────┬──────────────┘
              │                         │
        ┌─────▼─────┐            ┌───────▼────────┐
        │ Audit svc │            │   PostgreSQL   │  :5432
        │   :8086   │───────────▶│   (16-alpine)  │
        └───────────┘            └────────────────┘
```

**Key patterns**
- **EAV** core data model — runtime-configurable entity shapes (see [DATABASE.md](DATABASE.md)).
- **Two parallel RBAC hierarchies** — organization-scoped and workspace-scoped.
- **Soft deletes** everywhere (`is_archived`).
- **Transactional outbox** → RabbitMQ → four domain audit logs, with idempotency on both producer and consumer sides.

> 📚 Full design details live in [`AI-GUIDES-INDEX.md`](AI-GUIDES-INDEX.md) → `docs/ai-guides/ARCHITECTURE.md`.

---

## Quick Start

The entire stack runs in Docker — you **do not** need .NET, Python, Node, or PostgreSQL installed locally.

### Prerequisites

| Tool | Minimum version |
|---|---|
| **Docker Desktop** (Win/macOS) or **Docker Engine + Compose plugin** (Linux) | Docker 24.x · Compose 2.x |
| **Git** | 2.x |

> Recommended host resources: **8 GB+ free RAM**, **4 CPU cores**, **10 GB+ disk** (the stack runs 12 containers).
> On Windows, enable **"Use the WSL 2 based engine"** in Docker Desktop for noticeably faster startup.

### Run it

**PowerShell (Windows):**
```powershell
git clone <repository-url> relativa
cd relativa
Copy-Item .env.example .env
docker compose up --build -d
docker compose logs -f gateway
```

**Bash (Linux/macOS):**
```bash
git clone <repository-url> relativa
cd relativa
cp .env.example .env
docker compose up --build -d
docker compose logs -f gateway
```

Then open **http://localhost:3000** and register your first account. 🎉

| Command | What it does |
|---|---|
| `docker compose up -d` | Daily restart (no rebuild) |
| `docker compose down` | Stop everything (keeps data) |
| `docker compose down -v` | Stop **and wipe** the database |
| `docker compose logs -f <service>` | Tail one service's logs |

📖 Full setup walkthrough: [DOCKER-BUILD.md](DOCKER-BUILD.md) · [SETUP-GUIDE.md](SETUP-GUIDE.md)

---

## Environment Configuration

Configuration is driven by a single `.env` file at the repo root. Copy the template and adjust as needed:

```bash
cp .env.example .env
```

`.env` is personal and git-ignored; **`.env.example` is the committed template** with a comment on every value. Key variables:

| Variable | Purpose | Default |
|---|---|---|
| `DB_NAME` / `DB_USER` / `DB_PASS` | PostgreSQL credentials | `relativa` |
| `DB_PORT` | Postgres host port | `5432` |
| `PGADMIN_DEFAULT_EMAIL` / `_PASSWORD` | pgAdmin login | `admin@relativa.com` / `admin123` |
| `CLIENT_PORT` | Vue SPA host port | `3000` |
| `VITE_GATEWAY_URL` | Gateway URL the SPA calls | `http://localhost:8080` |
| `JWT_SECRET` | JWT signing key (HMAC-SHA256, ≥ 32 chars) | _change me_ |
| `JWT_ISSUER` / `JWT_AUDIENCE` | JWT claims (must match across auth & gateway) | `relativa-auth` / `relativa` |
| `CORS_ORIGIN_1` / `_2` | Allowed browser origins | `:5173` / `:3000` |
| `SMTP_HOST` | SMTP server (MailHog captures mail in dev) | `mailhog` |

> ⚠️ **Security:** `JWT_SECRET`, `JWT_ISSUER`, and `JWT_AUDIENCE` **must be identical** for the `auth` and `gateway` services — a mismatch returns `401` on every protected endpoint. Generate a unique `JWT_SECRET` per environment and never commit `.env`.

---

## Database & Seed Data

Migrations and demo data are applied **automatically** on the first `docker compose up`. A dedicated `migration` service waits for PostgreSQL to become healthy, runs all EF Core migrations, and exits — only then do the app services start.

After the first run the database is **not empty**; it comes pre-seeded with:

- **System roles** for organizations and workspaces (with priorities and permissions).
- **Permissions** catalog (org- and workspace-scoped).
- **Demo entities** — realistic clients, deals, contracts, and analysis rows — so you can explore the product immediately.

**Inspect the data** via pgAdmin at **http://localhost:5050** (log in with the `PGADMIN_*` values from `.env`, then register a server with host `postgres`).

**Reset to a clean state:**
```bash
docker compose down -v        # ⚠️ irreversibly deletes all Postgres data
docker compose up --build -d  # migrations + seed re-apply from scratch
```

📖 Full schema reference: [DATABASE.md](DATABASE.md)

---

## API Documentation

Every backend service ships interactive **Scalar** API docs and a raw OpenAPI spec:

| Service | Scalar UI | OpenAPI JSON |
|---|---|---|
| **Authentication** | http://localhost:8081/scalar/v1 | `:8081/openapi/v1.json` |
| **Core** | http://localhost:8082/scalar/v1 | `:8082/openapi/v1.json` |
| **Gateway** | http://localhost:8080/scalar/v1 | `:8080/openapi/v1.json` |

**Recommended flow:** open the Authentication Scalar → register → log in → copy the JWT → switch to the Core Scalar, click **Authorize**, paste the token, and exercise organization / workspace / RBAC endpoints.

📖 Step-by-step API guide: [SCALAR-GUIDE.md](SCALAR-GUIDE.md)

---

## Service & Port Reference

| Service | Port | Description |
|---|---|---|
| Client (Vue SPA) | `3000` | Main web app |
| Gateway (YARP) | `8080` | Single entry point / reverse proxy |
| Authentication | `8081` | Registration, login, JWT, profile |
| Core | `8082` | Organizations, workspaces, entities, RBAC |
| Graph | `8083` | Relationship graph data + SignalR |
| ML (Django) | `8084` | Closure & churn scoring |
| Audit | `8086` | Audit log consumer + read API |
| PostgreSQL | `5432` | Primary database |
| pgAdmin | `5050` | Database admin UI |
| RabbitMQ | `5672` / `15672` | AMQP / Management UI |
| MailHog | `1025` / `8025` | SMTP sink / web inbox (dev) |

> If a port is taken, change the matching variable in `.env` (e.g. `CLIENT_PORT`, `PGADMIN_PORT`, `DB_PORT`) and restart.

---

## Documentation

| Document | What it covers |
|---|---|
| [SETUP-GUIDE.md](SETUP-GUIDE.md) | Prerequisites, system requirements, database setup |
| [DOCKER-BUILD.md](DOCKER-BUILD.md) | Local Docker manual — start, stop, troubleshoot |
| [DATABASE.md](DATABASE.md) | Full schema, EAV model, RBAC, audit tables |
| [SCALAR-GUIDE.md](SCALAR-GUIDE.md) | Interactive API walkthrough |
| [docs/relativa_user_guide](docs/relativa_user_guide.docx) | End-user scenarios for every feature |
| [AI-GUIDES-INDEX.md](AI-GUIDES-INDEX.md) | Deep technical guides (architecture, microservices, runbooks) |

---

## Roadmap

Planned to close gaps with mainstream CRMs (currently on the roadmap):

- 🔴 **Data import/export**
- 🟡 **Custom dashboards**
- 🟢 **SSO / SAML**, **Webhooks**, **integration marketplace**
- 🟢 **Revenue forecasting** & **AI sales assistant**

---

## Contributing

Contributions are welcome! Please:

1. Fork the repo and create a feature branch.
2. Follow the maintenance obligations in [AI-GUIDES-INDEX.md](AI-GUIDES-INDEX.md) — update the relevant guide(s) when you change endpoints, services, schema, or UI.
3. Open a pull request against `main` with a clear description.

---

<p align="center">
  <!-- PLACEHOLDER: small footer logo — assets/branding/logo-mark.png -->
  <img src="assets/branding/logo-mark.png" alt="Relativa" width="120">
</p>

<p align="center">
  <sub>Built with .NET 10, Vue 3, Django & a lot of graphs. · <em>Turn relationships into revenue.</em></sub>
</p>
