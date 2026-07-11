#!/usr/bin/env bash
# Build the Docusaurus site (also reports broken links — run before any docs PR).
# Usage: scripts/site.sh [build|serve|start]
set -euo pipefail
cd "$(dirname "$0")/../website"

cmd="${1:-build}"
[ -d node_modules ] || npm ci
case "$cmd" in
  build) npm run build ;;
  serve) npm run build && npm run serve ;;
  start) npm start ;;
  *) echo "Unknown '$cmd' (build|serve|start)" >&2; exit 2 ;;
esac
