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
- Core health (via gateway, no JWT): `http://localhost:8080/core/health`
- Auth health (via gateway, no JWT): `http://localhost:8080/auth/health`
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

## 6) Migrations and database

The `migration` service applies EF Core migrations once Postgres is healthy, then exits successfully. The **auth** and **core** services wait for that job to finish before starting, so the schema is ready before they accept traffic.

Ensure `.env` includes the JWT and default-role variables from `.env.example` (`JWT_SECRET`, `JWT_ISSUER`, `JWT_AUDIENCE`, `DEFAULT_ROLE_ID`). They must stay in sync between the gateway and authentication services.

If you change database credentials, update `.env` and recreate containers: `docker compose up --build -d`.

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

- If **`migration` exits with a non-zero code** (for example 255), read its logs: `docker compose logs migration`. Typical causes are Postgres not reachable, wrong credentials in `.env`, or migrations failing against the database. The migration host uses the app output directory as its content root so `appsettings.json` and `ConnectionStrings__Default` from compose are applied correctly.

## 8) Implementation notes

- Gateway upstream routes are overridden in `docker-compose.yaml` to use Docker DNS service names (`auth`, `core`, `graph`, `ml`, `audit`).
- **Core** and **Authentication** read PostgreSQL `ConnectionStrings__Default` from compose (host `postgres`, credentials from `.env`).
- **Gateway** and **Authentication** share JWT settings via compose environment variables so issued tokens validate at the gateway.
