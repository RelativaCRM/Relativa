#!/bin/sh
set -e
echo "Starting Relativa.Migration..."
dotnet run --project src/Relativa.Migration/Relativa.Migration.csproj -c Release --no-build
echo "Migrations finished."
exit 0
