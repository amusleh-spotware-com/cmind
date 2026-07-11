---
description: Run one test tier (unit|integration|e2e|stress|all)
allowed-tools: Bash(bash scripts/test.sh*), Bash(dotnet test*)
---

Run a test tier via the shared runner. Arguments: `$ARGUMENTS` = `unit` | `integration` | `e2e` |
`stress` | `all` (default `unit`).

Run `bash scripts/test.sh $ARGUMENTS`. For `e2e` the runner installs Playwright browsers first.
Optional env: `FILTER=<xunit filter>`, `CONFIG=Debug|Release`. Report pass/fail counts and, on
failure, the first failing test and its message — do not declare done on red.
