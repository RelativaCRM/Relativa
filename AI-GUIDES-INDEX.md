# Relativa -- AI Agent Context Guides

> **Last verified:** 2026-05-01

Relativa is a multi-tenant CRM / sales-workspace platform built as a microservice monorepo. It includes .NET 10 backend services, a Vue 3 SPA client, a Django ML service, PostgreSQL 16, a YARP API gateway, and SignalR for real-time graph updates. Everything runs locally via Docker Compose.

---

## Maintenance Obligation (READ THIS FIRST)

**Any AI agent that modifies this project must update the corresponding guide(s) before finishing its task.**

| What you changed | Update these guides |
|---|---|
| Added, removed, or changed an **endpoint** | [MICROSERVICES.md](docs/ai-guides/MICROSERVICES.md) |
| Added or removed a **service** | [MICROSERVICES.md](docs/ai-guides/MICROSERVICES.md), [DOCKER-SETUP.md](docs/ai-guides/DOCKER-SETUP.md), [PROJECT-OVERVIEW.md](docs/ai-guides/PROJECT-OVERVIEW.md) |
| Changed **Docker Compose, Dockerfiles, networking, or env vars** | [DOCKER-SETUP.md](docs/ai-guides/DOCKER-SETUP.md) |
| Changed **architecture patterns, layers, persistence model, validation, or auth flows** | [ARCHITECTURE.md](docs/ai-guides/ARCHITECTURE.md) |
| **Implemented a feature** that was listed as stub/TODO, or introduced a **new known issue** | [PROJECT-STATUS.md](docs/ai-guides/PROJECT-STATUS.md) |
| Changed the **general purpose, domain model, or tech stack** | [PROJECT-OVERVIEW.md](docs/ai-guides/PROJECT-OVERVIEW.md) |
| Added or changed **database entities/tables** | [AUDIT-COVERAGE-MATRIX.md](docs/ai-guides/AUDIT-COVERAGE-MATRIX.md), [AUDIT-AGENT-REQUIREMENTS.md](docs/ai-guides/AUDIT-AGENT-REQUIREMENTS.md) |

**Audit policy:** For any DB model change, AI agents must read and follow [AUDIT-AGENT-REQUIREMENTS.md](docs/ai-guides/AUDIT-AGENT-REQUIREMENTS.md) before finishing the task.

**Always** update the "Last verified" date at the top of any guide you modify.

---

## Guide Index

| Guide | Path | What it covers |
|---|---|---|
| **Project Overview** | [docs/ai-guides/PROJECT-OVERVIEW.md](docs/ai-guides/PROJECT-OVERVIEW.md) | General purpose, business domain, tech stack, repo layout, existing docs map |
| **Microservices** | [docs/ai-guides/MICROSERVICES.md](docs/ai-guides/MICROSERVICES.md) | Per-service catalog: purpose, endpoints, status, key files |
| **Architecture** | [docs/ai-guides/ARCHITECTURE.md](docs/ai-guides/ARCHITECTURE.md) | Layered structure, persistence library, validation, auth flow, domain model, conventions |
| **Docker Setup** | [docs/ai-guides/DOCKER-SETUP.md](docs/ai-guides/DOCKER-SETUP.md) | Compose topology, dependency order, networking, Dockerfiles, environment config |
| **Project Status** | [docs/ai-guides/PROJECT-STATUS.md](docs/ai-guides/PROJECT-STATUS.md) | What is implemented, what is stubbed, known issues, roadmap |
| **Audit Coverage Matrix** | [docs/ai-guides/AUDIT-COVERAGE-MATRIX.md](docs/ai-guides/AUDIT-COVERAGE-MATRIX.md) | Current auditable-table scope and gap tracking |
| **Audit Agent Requirements** | [docs/ai-guides/AUDIT-AGENT-REQUIREMENTS.md](docs/ai-guides/AUDIT-AGENT-REQUIREMENTS.md) | Mandatory rules for AI agents when creating/changing DB entities/tables |

---

## How to Use

Point an AI agent to this file (`AI-GUIDES-INDEX.md`) to give it full project context. For scoped tasks, point to a single guide instead -- each guide is self-contained and repeats the maintenance obligation.
