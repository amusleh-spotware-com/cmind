---
title: 0005 — The AI client uses raw HTTP, not the Anthropic SDK
description: Why IAiClient calls the Anthropic API over a typed HttpClient instead of the official SDK, and why AI is fully gated on a key.
---

# 0005 — The AI client uses raw HTTP, not the Anthropic SDK

## Context

Every AI feature (strategy generation, self-repair, risk guard, post-mortems) calls the Anthropic
API. An SDK dependency adds a transitive surface we don't control, couples our release cadence to
theirs, and hides the exact wire contract we need to reason about for resilience and cost.

## Decision

`IAiClient` calls Anthropic over **raw HTTP** through a typed `HttpClient` — deliberately **not** the
SDK. `AiFeatureService` is the single orchestrator shared by Web endpoints, the MCP `AiTools`, and
`AiRiskGuard`. The whole surface is **gated on `AppOptions.Ai.ApiKey`**: with no key, every feature
returns `AiResult.Fail` and the app runs unchanged.

## Consequences

- No key is required for build, test, or E2E — CI and local dev run the full app without AI.
- We own the request/response shape, retry/timeout policy, and token accounting explicitly.
- New Anthropic features must be wired by hand; we trade convenience for control and a smaller
  dependency surface. See the `claude-api` reference for current model ids and parameters.

<!-- [ZH-HANS] Translation needed -->
