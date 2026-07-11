---
description: Run the analyzer sweep (info CA/IDE) on given or changed projects
allowed-tools: Bash(bash scripts/sweep.sh*), Bash(dotnet format*)
---

Run the analyzer sweep per CLAUDE.md. Surfaces info-level CA/IDE diagnostics `dotnet build` hides.

Arguments: `$ARGUMENTS` = optional list of `.csproj` paths (default: every `src/**/*.csproj`).

Do this:
1. Run `bash scripts/sweep.sh $ARGUMENTS`.
2. For each diagnostic in a file **I** touched, fix it (autofix scope:
   `dotnet format analyzers <proj> --include <file> --severity info`, or by hand).
3. Leave pre-existing diagnostics in untouched files alone.
4. Re-run until touched files are clean.
