#!/usr/bin/env bash
# Point git at the repo's tracked hooks (scripts/hooks). Opt-in — run once per clone.
set -euo pipefail
cd "$(dirname "$0")/.."
git config core.hooksPath scripts/hooks
chmod +x scripts/hooks/* 2>/dev/null || true
echo "Git hooks enabled (core.hooksPath = scripts/hooks). Disable: git config --unset core.hooksPath"
