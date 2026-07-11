#!/usr/bin/env bash
# Analyzer sweep — surfaces info-level CA/IDE rules `dotnet build` hides (CLAUDE.md mandate).
# Usage: scripts/sweep.sh [project.csproj ...]   (no args = every src/**/*.csproj)
# Exit non-zero if any project would be changed by the analyzers.
set -euo pipefail
cd "$(dirname "$0")/.."

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
