---
description: Add an EF Core migration with the repo's canonical layout
allowed-tools: Bash(bash scripts/migration.sh*), Bash(dotnet ef*)
---

Add an EF Core migration. Arguments: `$ARGUMENTS` = the migration name (PascalCase, no spaces).

Run `bash scripts/migration.sh $ARGUMENTS`. This targets `src/Infrastructure` for project and
startup and writes to `Persistence/Migrations`. After it succeeds, review the generated `Up`/`Down`
and the `DataContextModelSnapshot` diff, then build to confirm 0 warnings.
