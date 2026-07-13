#!/usr/bin/env bash
# Self-hoster deploy smoke (WS-9, public-launch-readiness.md): proves the one-command Docker Compose
# stack (Postgres + Web + MCP) boots and serves. Brings the stack up from a throwaway .env, waits for
# the Web /alive health endpoint to return 200, asserts it, and tears everything down. Idempotent and
# CI-friendly — the nightly workflow runs it so a broken compose file / image / startup fails the build.
#
# Usage:
#   scripts/compose-smoke.sh            # build + up + probe + down
#   KEEP_STACK=1 scripts/compose-smoke.sh   # leave the stack running for inspection
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

WEB_URL="${WEB_URL:-http://localhost:8080}"
KEEP_STACK="${KEEP_STACK:-0}"
TIMEOUT_SECONDS="${TIMEOUT_SECONDS:-240}"

log() { printf '\n\033[1;36m==> %s\033[0m\n' "$*"; }

# Throwaway .env so the smoke never depends on a developer's real secrets.
ENV_FILE="$ROOT/.env"
CREATED_ENV=0
if [[ ! -f "$ENV_FILE" ]]; then
  cat > "$ENV_FILE" <<'EOF'
PG_PASSWORD=smoke-pg-pass-Str0ng!
OWNER_EMAIL=owner@smoke.local
OWNER_PASSWORD=Smoke-Owner-Str0ng!1
EOF
  CREATED_ENV=1
fi

cleanup() {
  if [[ "$KEEP_STACK" != "1" ]]; then
    log "Tearing down compose stack"
    docker compose down -v >/dev/null 2>&1 || true
    [[ "$CREATED_ENV" == "1" ]] && rm -f "$ENV_FILE"
  fi
}
trap cleanup EXIT

log "Building + starting compose stack (postgres + web + mcp)"
docker compose up -d --build

log "Waiting for Web /alive (timeout ${TIMEOUT_SECONDS}s)"
deadline=$(( $(date +%s) + TIMEOUT_SECONDS ))
until curl -fsS "$WEB_URL/alive" >/dev/null 2>&1; do
  if (( $(date +%s) > deadline )); then
    log "FAIL: Web /alive did not return 200 within ${TIMEOUT_SECONDS}s"
    docker compose logs --tail=50 web || true
    exit 1
  fi
  sleep 3
done

log "PASS: self-hoster compose stack is up and Web /alive is healthy"
