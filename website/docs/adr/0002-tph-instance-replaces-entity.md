---
title: 0002 — Instance state is TPH; a transition replaces the entity
description: Why an instance's id changes as it moves through its lifecycle, and why the container id is the stable key.
---

# 0002 — Instance state is TPH; a transition replaces the entity

## Context

A run/backtest instance moves through states (pending → scheduled → starting → running → terminal).
We model state with EF Core **Table-Per-Hierarchy (TPH)**: each state is a subtype
(`StartingRunInstance`, `RunningRunInstance`, …). EF's TPH discriminator column **cannot change** on
an existing row.

## Decision

A state transition **replaces the entity** with a new subtype instance rather than mutating a status
field. Because the row is replaced, the **instance id changes** across starting → running → terminal.
The **container id is stable** and is carried across transitions; the HTTP node agent is keyed by
container id for status/report/stop/logs.

## Consequences

- Each state is a distinct type with only the fields and methods valid in that state — illegal
  transitions and nonsensical field access are compile errors, not runtime checks.
- Callers must **not** cache an instance id across a transition; use the container id as the stable
  handle for anything that spans states.
- Transition logic lives in `InstanceTransitions`; the id change is intentional, not a bug.
