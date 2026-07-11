# Summary

<!-- What does this PR do and why? -->

Closes #

## Changes

<!-- Key changes, grouped by layer (Core / Infrastructure / Nodes / Web / Mcp / ExternalNode). -->

-

## Checklist

<!-- Full standard: CONTRIBUTING.md#pull-request-standard -->

- [ ] Scoped to one logical change
- [ ] `dotnet build` passes, no new warnings (`TreatWarningsAsErrors=true`, `.razor` inspections included)
- [ ] `dotnet test` green (incl pre-existing)
- [ ] Added/updated tests for new behavior (unit under `tests/UnitTests/`, plus integration/E2E where fits; regression test for bug fixes)
- [ ] Domain logic on aggregate / value object / domain service, not endpoint/tool/component/hosted service ([DDD checklist](../blob/main/CLAUDE.md#domain-driven-design--mandatory))
- [ ] No `DateTime.UtcNow`/`Now` in production code (inject `TimeProvider`)
- [ ] No secrets committed
- [ ] Docs updated (`docs/features/*.md` / `README.md` / `CLAUDE.md`) if behavior or setup changed
- [ ] EF migration added if schema changed

## AI assistance

<!-- AI-assisted PRs welcome. Disclosure helps reviewers — not a penalty. See CONTRIBUTING.md#contributing-with-agentic-ai -->

- [ ] Written or assisted by AI agent. Reviewed every line, understand it, stand behind it.

## Notes for reviewers

<!-- Anything non-obvious, trade-offs, or follow-ups. -->