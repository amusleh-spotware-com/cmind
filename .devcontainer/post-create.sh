#!/usr/bin/env bash
# One-time devcontainer setup: restore .NET, install Playwright browsers + site deps, wire git hooks.
set -euo pipefail

dotnet restore

# Playwright browsers for the E2E tier (needs a built E2E project first).
dotnet build tests/E2ETests -c Debug || true
PW="tests/E2ETests/bin/Debug/net10.0/playwright.ps1"
[ -f "$PW" ] && pwsh "$PW" install --with-deps chromium || echo "Playwright install skipped (build first)"

# Docs site deps.
( cd website && npm ci ) || true

# Git hooks (analyzer/format pre-commit gate).
bash scripts/install-hooks.sh || true

echo "Dev container ready. Try: bash scripts/test.sh unit"
