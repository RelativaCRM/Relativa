# Relativa Local Docker Manual

This repository runs locally with Docker, so you do not need to install PostgreSQL or application servers directly on the OS.

## Quick Start

First run (PowerShell):

```powershell
Copy-Item .env.example .env
docker compose up --build -d
docker compose logs -f gateway
```

First run (Bash):

```bash
cp .env.example .env
docker compose up --build -d
docker compose logs -f gateway
```

Daily restart:

```bash
docker compose up -d
```

Stop everything:

```bash
docker compose down
```

## Prerequisites

- Docker Desktop (or Docker Engine with Compose plugin)
- Git

## 1) Initial setup

1. Open terminal in repository root.
2. Create local env file from template:
   - PowerShell: `Copy-Item .env.example .env`
   - Bash: `cp .env.example .env`
3. Update values in `.env` for your machine.

`.env` is personal and ignored by git. `.env.example` is committed as the template.

## 2) Start the stack

Run from repository root:

```bash
docker compose up --build
```

Use detached mode if needed:

```bash
docker compose up --build -d
```

## 3) Stop the stack

```bash
docker compose down
```

Also remove volumes (resets DB data):

```bash
docker compose down -v
```

## 4) Main URLs

- Client: `http://localhost:3000`
- Gateway: `http://localhost:8080`
- Gateway health: `http://localhost:8080/health`
- PostgreSQL: `localhost:5432`
- pgAdmin: `http://localhost:5050`

## 5) pgAdmin setup

Log in with:

- Email: `PGADMIN_DEFAULT_EMAIL` from `.env`
- Password: `PGADMIN_DEFAULT_PASSWORD` from `.env`

Then register PostgreSQL server with:

- Host: `postgres`
- Port: `5432`
- Username: `DB_USER` from `.env`
- Password: `DB_PASS` from `.env`
- Database: `DB_NAME` from `.env`

## 6) Current migration workflow

At this moment, compose starts DB and services, but migration execution is not yet automated as an init job.

Recommended current flow:

1. Start stack: `docker compose up --build`
2. Run migration container/job (when wired in compose) or apply EF migration manually.
3. Use services after schema is up to date.

## 7) Troubleshooting

- If Docker command fails with daemon/pipe errors, start Docker Desktop first.
- If any port is busy, change values in `.env` (for example `CLIENT_PORT` or `PGADMIN_PORT`) and restart.
- To inspect logs:

```bash
docker compose logs -f
```

- To inspect one service logs:

```bash
docker compose logs -f gateway
```

## 8) Implementation notes

- Gateway upstream routes are overridden in `docker-compose.yaml` to use Docker DNS service names (`auth`, `core`, `graph`, `ml`, `audit`).
- Core reads PostgreSQL connection from `ConnectionStrings__Default` injected by compose.
