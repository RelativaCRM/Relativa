#!/bin/sh
set -e
echo "relativa-migrations init container"
if [ -n "${ConnectionStrings__Default:-}" ]; then
  echo "ConnectionStrings__Default is set (PostgreSQL)."
else
  echo "ConnectionStrings__Default not set."
fi
echo "Skeleton: no migrations applied. Use EF Core bundle or dotnet ef against Core when available."
exit 0
