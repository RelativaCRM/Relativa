# Relativa CRM — Feature to Benefit Analysis

> Three technical features from the core development stages, translated into user-facing value propositions.

---

## Feature 1 — ML-Powered Deal Scoring & Churn Prediction

### Technology

`POST /api/ml/score/batch` — a dedicated ML microservice endpoint that accepts a batch of entity IDs and returns two probability scores per record: **deal closure likelihood** and **churn risk**. The model runs as an independent service behind the API Gateway, keeping inference latency separate from the main CRM response path.

### User Benefit

Know which deals are about to close and which clients are at risk of leaving — before it's too late.

Relativa scores every record in your CRM automatically. Instead of guessing where to focus next, your team gets a ranked list: these deals need attention today, these clients are losing interest. Stop spreading effort evenly across hundreds of records. Focus on the 20 % that drives 80 % of your revenue.

---

## Feature 2 — Real-Time Network Graph (SignalR + Graph RPC)

### Technology

Two SignalR WebSocket hubs (`/graph/hubs/graph` and `/core/hubs/core`) push live entity and relationship updates to every connected client simultaneously. The `POST /api/v1/workspaces/{workspaceId}/entity-graph/create` endpoint performs composite graph operations — creating entities and their relationships atomically in a single transaction, with the graph index updated immediately. The `GET /api/v1/graph/` endpoint returns the full node-and-edge structure with optional risk-level filtering (`high | medium | low`).

### User Benefit

See your entire client network update in real time — no refreshes, no stale data, no one working from yesterday's version.

When your colleague adds a new deal and connects it to an existing contact, your graph updates on screen within milliseconds. Switch on the risk filter and the dangerous relationships light up immediately. Spot hidden connections and warning signs the moment they appear — not after your next team meeting.

---

## Feature 3 — Granular Role-Based Access Control + Full Audit Trail

### Technology

Custom role definitions at both the organization level (`POST /api/v1/organizations/{id}/roles`) and the workspace level (`POST /api/v1/workspaces/{id}/roles`), with 8 total role-management endpoints covering create, update, and archive across both scopes. Combined with the Audit Log service (`GET /audit-log`), which provides a paginated, fully-searchable change history with 13 filter parameters — including actor, target user, workspace, organization, date range, and action type — down to the individual entity (`GET /entities/{entityId}/audit-log`).

### User Benefit

Every action in your CRM is logged, attributed, and searchable in seconds. Grant each person exactly the permissions they need — nothing more.

A sales rep sees their own deals. A team lead sees their workspace. An executive sees everything. When an incident happens — a record deleted, a deal modified unexpectedly — you find out who did it, when, and from which workspace in two clicks. No more spreadsheet-based access lists. No more "who changed this?" conversations. Pass your next compliance audit without scrambling for records.
