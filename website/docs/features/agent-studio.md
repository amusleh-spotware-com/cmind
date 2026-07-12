---
description: "Agent Studio — create persona-driven, no-code trading agents with a character and archetype that manage accounts toward your goals under the Autonomy & Safety Kernel (risk envelope, circuit breaker, kill switch, versioned disclaimer consent)."
---

# Agent Studio

Agent Studio lets you create a **trading agent with a character** — no code — and give it management of
your accounts toward measurable goals. An agent is like a personality-driven cBot: you pick an archetype
and attitude, set the guardrails, and it runs under the **Autonomy & Safety Kernel**.

Open **AI → Agent Studio** (`/agent-studio`).

## Create an agent

The **New agent** dialog collects, no-code:

- **Name** and **archetype** — Scalper, Day Trader, Swing Trader, Position Trader, News Trader,
  Contrarian, Mean Reversion or Breakout/Momentum. Each preset fixes a sensible cadence and posture.
- **Attitude** — aggressiveness, patience and trend-following sliders.
- **Autonomy level** — **Advisory** (proposes only) or **Approval-gated** (acts only after your
  per-action approval). **Full Auto** (no per-trade approval) additionally requires a **risk envelope**
  and acceptance of the risk disclaimer before it can arm.

The persona compiles **deterministically** into the agent's system prompt (no LLM authors it), so the
same configuration always produces the same instructions — reproducible and auditable.

## The roster

Every agent shows in a control-room table: **which agent, its type, how many accounts it manages, its
goals, run status, and last action**, with **Start / Stop / Kill** controls. The Kill switch halts a
running agent immediately.

## Safety is a domain invariant, not a setting

Everything money-touching routes through the **Autonomy & Safety Kernel**:

- **Risk envelope** — hard per-order limits (max daily loss, open exposure, position size, leverage,
  consecutive losses, orders/hour, allowed symbols). Every order is validated against it before dispatch;
  a breach is refused, not clamped. Required before an agent can reach Full Auto.
- **Circuit breaker** — deterministically halts new risk on a loss streak, a daily-loss breach, a **hard
  performance-goal breach**, or **AI-provider unavailability** (a down or hallucinating model never opens
  fresh positions).
- **Versioned disclaimer consent** — a one-time, versioned acceptance is required to arm Full Auto
  (legally-required consent, not per-trade approval); bumping the disclaimer forces re-consent.
- **Kill switch** — an idempotent emergency halt on every running agent.

## Goals

Give an agent **measurable objectives** — e.g. *keep max drawdown below 4%*, *profit factor at least
1.5*, *win rate ≥ 55%*. Each target is **Hard** (a guardrail — a breach trips the circuit breaker) or
**Soft** (steers reasoning only), evaluated as On-track / At-risk / Breached.

## Scope

Today Agent Studio ships the full agent lifecycle: create/configure/persist agents, manage their goals
and accounts, and Start/Stop/Kill them, with every safety invariant enforced by the aggregate and the
kernel. The autonomous 24/7 decision loop — the multi-account, point-in-time Open-API state store
(read-only to the model), the ReAct tool loop over the quant tools, layered memory/reflection, and the
`AgentDecisionRecord` audit ledger — is the next increment; it is gated on the Anthropic API key and the
live Open API, so the app runs unchanged without them.
