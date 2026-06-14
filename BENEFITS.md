# Relativa CRM — Features

![Version](https://img.shields.io/badge/Version-2.0.0-0078D4) ![Status](https://img.shields.io/badge/Status-Stable-22C55E) ![License](https://img.shields.io/badge/License-MIT-F59E0B) ![Tests](https://img.shields.io/badge/Tests-Passing-22C55E?logo=githubactions&logoColor=white) ![Coverage](https://img.shields.io/badge/Coverage-85%25-EAB308)

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white) ![Vue.js](https://img.shields.io/badge/Vue.js-3.5-4FC08D?logo=vuedotjs&logoColor=white) ![TypeScript](https://img.shields.io/badge/TypeScript-5-3178C6?logo=typescript&logoColor=white) ![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?logo=postgresql&logoColor=white) ![SignalR](https://img.shields.io/badge/SignalR-10-0078D4?logo=microsoft&logoColor=white) ![Vite](https://img.shields.io/badge/Vite-5-646CFF?logo=vite&logoColor=white) ![Chart.js](https://img.shields.io/badge/Chart.js-4-FF6384?logo=chartdotjs&logoColor=white)

---

> **A CRM that tells your team what to do next — not just what happened.**

---

## 🤖 AI-Powered Sales Intelligence

*Technology: `POST /api/ml/score/batch` — dedicated ML microservice returning deal closure probability and churn risk per entity*

- 🎯 **Focus on what closes** — Relativa ranks every deal by closure likelihood so your team stops guessing and starts acting on the right records today
- 📉 **Stop churn before it starts** — built-in client risk scoring surfaces at-risk accounts before they go silent, not after they've left
- ⚡ **Zero analysis time** — ML runs in the background; salespeople see a prioritized list, not a raw database they have to interpret themselves

---

## 🌐 Real-Time Network Graph

*Technology: SignalR WebSocket hubs (`/graph/hubs/graph`, `/core/hubs/core`) + atomic composite entity creation (`POST /entity-graph/create`) + risk-filtered graph query (`GET /graph/`)*

- 👁️ **See your entire client network live** — every relationship and every update is visible to the whole team instantly, with no page refresh required
- ⚠️ **Spot risk the moment it appears** — filter the graph by risk level (high / medium / low) and problem areas light up on screen in real time
- 🤝 **True team collaboration** — when a colleague adds a deal or links a contact, every team member's screen reflects it within milliseconds

---

## 🔐 Granular Access Control + Full Audit Trail

*Technology: 8 custom role-management endpoints across organization and workspace levels + Audit Log with 13 filter parameters (actor, target, date range, workspace, action type, and more)*

- 🛡️ **Precise permissions, zero guesswork** — define exactly what each role can see and do, assignable at the organization level or per individual workspace
- 📋 **Every action, permanently logged** — who changed what, when, and from which workspace — fully searchable in two clicks
- ✅ **Compliance-ready out of the box** — pass security audits without scrambling; filter the audit log by date, user, or entity in seconds

---

## Technical Reference

*For the next documentation stage.*

| Feature | API Endpoint | Service |
|---------|-------------|---------|
| ML Deal + Churn Scoring | `POST /api/ml/score/batch` | ML Service |
| Graph Visualization | `GET /api/v1/graph/` | Graph Service |
| Composite Graph Creation | `POST /api/v1/workspaces/{id}/entity-graph/create` | Graph Service |
| Real-time Graph Updates | `/graph/hubs/graph` (SignalR) | Graph Service |
| Real-time Entity Updates | `/core/hubs/core` (SignalR) | Core Service |
| Global Audit Log | `GET /audit-log` | Audit Service |
| Entity Audit Log | `GET /entities/{id}/audit-log` | Audit Service |
| Custom Org Roles | `POST /api/v1/organizations/{id}/roles` | Core Service |
| Custom Workspace Roles | `POST /api/v1/workspaces/{id}/roles` | Core Service |
| Organization Dashboard | `GET /api/v1/dashboard/summary` (+ 5 endpoints) | Graph Service |
| Workspace Dashboard | `GET /api/v1/dashboard/workspace/{id}/summary` (+ 5 endpoints) | Graph Service |
