---
description: Run the repo's definition-of-done checklist before declaring work complete
allowed-tools: Bash(dotnet build*), Bash(bash scripts/*), Bash(git status*), Bash(git diff*)
---

Walk the CLAUDE.md **Definition of done** for the current change. Do NOT claim done until every box
is green. Steps:

1. **Build** — `dotnet build --configuration Release` → must be 0 warnings, 0 errors.
2. **Analyzer sweep** — `/sweep` on every project I touched; fix diagnostics on my files.
3. **Rider problems** — run `get_file_problems` (jetbrains MCP) on every `.cs`/`.razor` I changed; fix all.
4. **Tests** — new behavior covered unit + integration + E2E; failure paths included; bug fix has a
   regression test; new route added to `PageSmokeTests`. Run the affected tiers via `/test-tier`.
5. **DDD** — domain logic on aggregates/value objects, not endpoints/hosts; `src/Core` has no infra deps.
6. **Mandates** — no `DateTime.UtcNow`/`.Now`; no secrets/magic strings; no direct `ILogger.Log*`; modern C#.
7. **Docs** — `website/docs/**` updated in the same change; `/site` link-check passes; EF migration added if schema changed.

Report each item as pass/fail with evidence. List anything that could not be verified and why.
