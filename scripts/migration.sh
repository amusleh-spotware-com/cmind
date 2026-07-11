#!/usr/bin/env bash
# Add an EF Core migration with the repo's canonical layout.
# Usage: scripts/migration.sh <MigrationName>
set -euo pipefail
cd "$(dirname "$0")/.."

name="${1:?Usage: scripts/migration.sh <MigrationName>}"
dotnet ef migrations add "$name" \
  -p src/Infrastructure -s src/Infrastructure -o Persistence/Migrations
