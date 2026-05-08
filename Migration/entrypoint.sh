#!/bin/sh
set -e

TRUNCATE_DB=false

for arg in "$@"; do
  if [ "$arg" = "--truncate" ] || [ "$arg" = "--truncate-db" ]; then
    TRUNCATE_DB=true
  fi
done

DB_HOST="${DB_HOST:-postgres}"
DB_PORT="${DB_PORT:-5432}"
DB_NAME="${DB_NAME:-relativa}"
DB_USER="${DB_USER:-relativa}"
DB_PASS="${DB_PASS:-relativa}"

if [ "${TRUNCATE_DB}" = "true" ] || [ "${MIGRATION_TRUNCATE_DB:-false}" = "true" ]; then
  echo "Force truncation requested. Waiting for PostgreSQL at ${DB_HOST}:${DB_PORT}..."
  until pg_isready -h "${DB_HOST}" -p "${DB_PORT}" -U "${DB_USER}" -d postgres >/dev/null 2>&1; do
    sleep 1
  done

  echo "Dropping and recreating database '${DB_NAME}'..."
  PGPASSWORD="${DB_PASS}" psql -h "${DB_HOST}" -p "${DB_PORT}" -U "${DB_USER}" -d postgres -v ON_ERROR_STOP=1 -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '${DB_NAME}' AND pid <> pg_backend_pid();"
  PGPASSWORD="${DB_PASS}" psql -h "${DB_HOST}" -p "${DB_PORT}" -U "${DB_USER}" -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE IF EXISTS \"${DB_NAME}\";"
  PGPASSWORD="${DB_PASS}" psql -h "${DB_HOST}" -p "${DB_PORT}" -U "${DB_USER}" -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE \"${DB_NAME}\";"
fi

echo "Starting Relativa.Migration..."
dotnet Relativa.Migration.dll
echo "Migrations finished."
