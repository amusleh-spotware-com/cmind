---
title: Architecture Decision Records
description: The non-obvious design decisions behind cMind — context, decision, and consequences — that you can't read off the code.
---

# Architecture Decision Records

These record the design decisions you **can't infer from the code** — the trade-offs, the roads not
taken, and why. Each is short: *Context → Decision → Consequences*. New structural decision → add an
ADR here (next number) so the next engineer (human or AI) inherits the reasoning, not just the
result.

| # | Decision |
|---|---|
| [0001](./0001-strict-ddd-pure-core.md) | Strict DDD with a pure `Core` |
| [0002](./0002-tph-instance-replaces-entity.md) | Instance state is TPH; a transition replaces the entity |
| [0003](./0003-external-nodes-http-jwt.md) | cTrader CLI nodes are HTTP + JWT, no SSH/shell |
| [0004](./0004-cbotbuilder-on-web-host.md) | `CBotBuilder` runs on the web host in a sandbox container |
| [0005](./0005-anthropic-raw-http.md) | The AI client uses raw HTTP, not the Anthropic SDK |
| [0006](./0006-copy-profile-db-lease.md) | Copy hosting is coordinated by an atomic DB lease |

<!-- [ZH-HANS] Translation needed -->
