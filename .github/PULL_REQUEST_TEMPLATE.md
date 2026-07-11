# Summary

<!-- What does this PR do and why? -->

Closes #

## Changes

<!-- Bullet the key changes, grouped by layer (Core / Infrastructure / Nodes / Web / Mcp / ExternalNode). -->

-

## Checklist

<!-- Full standard: CONTRIBUTING.md#pull-request-standard -->

- [ ] Scoped to one logical change
- [ ] `dotnet build` passes with no new warnings (`TreatWarningsAsErrors=true`, `.razor` inspections included)
- [ ] `dotnet test` is green (including pre-existing tests)
- [ ] Added/updated tests for new behavior (unit under `tests/UnitTests/`, plus integration/E2E where applicable; regression test for bug fixes)
- [ ] Domain logic lives on an aggregate / value object / domain service, not an endpoint/tool/component/hosted service ([DDD checklist](../blob/main/CLAUDE.md#domain-driven-design--mandatory))
- [ ] No `DateTime.UtcNow`/`Now` in production code (inject `TimeProvider`)
- [ ] No secrets committed
- [ ] Docs updated (`docs/features/*.md` / `README.md` / `CLAUDE.md`) if behavior or setup changed
- [ ] EF migration added if the schema changed

## AI assistance

<!-- We welcome AI-assisted PRs. Disclosure helps reviewers — it is not a penalty. See CONTRIBUTING.md#contributing-with-agentic-ai -->

- [ ] This PR was written or assisted by an AI agent. I have reviewed every line, understand it, and stand behind it.

## Notes for reviewers

<!-- Anything non-obvious, trade-offs, or follow-ups. -->
