#!/usr/bin/env bash
# Analyzer sweep — surfaces info-level CA/IDE rules `dotnet build` hides (CLAUDE.md mandate).
#
# Usage:
#   scripts/sweep.sh [project.csproj ...]   # sweep whole projects (default: every src/**/*.csproj)
#   scripts/sweep.sh --changed [baseRef]    # sweep only .cs files changed vs baseRef (default origin/main)
#
# The --changed mode is what CI gates on: it enforces the mandate ("fix diagnostics on files you
# touched") without failing on pre-existing debt in files nobody edited.
set -euo pipefail
cd "$(dirname "$0")/.."

if [ "${1:-}" = "--changed" ]; then
  base="${2:-origin/main}"
  git rev-parse --verify --quiet "$base" >/dev/null || base="$(git rev-parse HEAD~1 2>/dev/null || echo HEAD)"
  mapfile -t files < <(git diff --name-only "$base"...HEAD -- 'src/**/*.cs' 'tests/**/*.cs')
  if [ ${#files[@]} -eq 0 ]; then
    echo "No changed C# files vs $base — nothing to sweep."
    exit 0
  fi
  include_args=()
  for f in "${files[@]}"; do include_args+=(--include "$f"); done
  echo "== analyzer sweep: ${#files[@]} changed file(s) vs $base =="
  dotnet format analyzers cmind.slnx --verify-no-changes --severity info "${include_args[@]}"
  exit $?
fi

projects=("$@")
if [ ${#projects[@]} -eq 0 ]; then
  mapfile -t projects < <(git ls-files 'src/**/*.csproj')
fi

fail=0
for proj in "${projects[@]}"; do
  echo "== analyzer sweep: $proj =="
  if ! dotnet format analyzers "$proj" --verify-no-changes --severity info; then
    echo "!! analyzer diagnostics in $proj (run without --verify-no-changes to autofix)" >&2
    fail=1
  fi
done

if [ "$fail" -ne 0 ]; then
  echo "Analyzer sweep FAILED." >&2
fi
exit "$fail"
