---
title: 0001 — Strict DDD with a pure Core
description: Why domain logic lives on aggregates in a Core project with zero infrastructure dependencies.
---

# 0001 — Strict DDD with a pure `Core`

## Context

This app moves real money. Business rules scattered across endpoints, background services, and Razor
components rot into untestable, inconsistent behavior — exactly where a bug costs a user capital.

## Decision

Domain logic lives **on aggregates, value objects, and domain services** in `src/Core`, which
compiles with **zero infrastructure dependencies** (no EF, HttpClient, Docker, or ASP.NET). Endpoints,
MCP tools, components, and `BackgroundService`s **orchestrate** — they never decide. Rules:

- No public setters; state changes through intention-revealing methods that guard invariants.
- Aggregates reference each other by **strong ID**, not navigation property.
- One `SaveChanges` mutates **one** aggregate; cross-aggregate flows use domain events.
- Primitives crossing a domain boundary are wrapped in value objects.
- Invariant violations throw a Core `DomainException`, not a framework exception.

## Consequences

- Domain rules are unit-testable without a database or a web host.
- `Core` purity is machine-enforced by `ArchitectureGuardTests` and would fail the build if broken.
- There is more ceremony (value objects, strong IDs, domain events) than an anemic model — this is
  the deliberate cost of keeping the money-moving rules correct and in one place.
