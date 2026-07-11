#!/usr/bin/env bash
# Run one test tier (or all). Single source of truth shared by CI, slash commands, humans.
# Usage: scripts/test.sh <unit|integration|e2e|stress|all>
# Env: CONFIG (default Release), FILTER (xunit --filter passthrough).
set -euo pipefail
cd "$(dirname "$0")/.."

tier="${1:-all}"
config="${CONFIG:-Release}"
filter_args=()
[ -n "${FILTER:-}" ] && filter_args=(--filter "$FILTER")

run() { dotnet test "$1" -c "$config" "${filter_args[@]}"; }

install_browsers() {
  # Playwright browsers are NOT bundled — install after build so E2E can launch a real browser.
  dotnet build tests/E2ETests -c "$config"
  local ps1="tests/E2ETests/bin/$config/net10.0/playwright.ps1"
  if command -v pwsh >/dev/null 2>&1 && [ -f "$ps1" ]; then
    pwsh "$ps1" install --with-deps chromium
  else
    echo "!! pwsh or $ps1 missing — cannot install Playwright browsers" >&2
    exit 1
  fi
}

case "$tier" in
  unit)        run tests/UnitTests ;;
  integration) run tests/IntegrationTests ;;
  e2e)         install_browsers; run tests/E2ETests ;;
  stress)      run tests/StressTests ;;
  all)         run tests/UnitTests; run tests/IntegrationTests; install_browsers; run tests/E2ETests ;;
  *) echo "Unknown tier '$tier' (unit|integration|e2e|stress|all)" >&2; exit 2 ;;
esac
