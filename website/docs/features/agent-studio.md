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
- **Managed account(s)** — **at least one is required to create the agent** (an agent with no account
  could never start, so *Create* stays disabled until you pick one). If you have not linked a trading
  account yet, the dialog says so and points you to link one first.
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

## The decision pipeline

Once started, an agent runs a **24/7 supervised loop** (`AgentRuntimeService`). Each tick, for every
managed account, it: reads the **deterministic account state** (ground truth, never the model's memory);
asks the decision engine for a move; passes it through the **safety gate** (`AgentDecisionProcessor`) —
autonomy level → circuit breaker → risk envelope; writes an append-only **`AgentDecisionRecord`**; and
halts or executes as the gate directs. The loop is **fault-isolated** (one agent's failure never touches
another or the host) and **safe by default**: it is inert unless AI is configured *and*
`App:Ai:AgentRuntimeEnabled` is set, and it never opens fresh risk while the AI provider is unavailable.

- **Approval gate** — an **Approval-gated** agent's proposed order is recorded as **Pending** and does
  nothing until the owner approves it (`POST /api/agent-studio/{id}/decisions/{seq}/approve` or
  `/reject`); **Full Auto** clears through the envelope with no per-trade approval; **Advisory** only
  proposes.
- **Audit ledger** — every decision is replayable: reasoning (XAI), the evidence it cited, the gate
  verdict, the order intent and whether it executed, at `GET /api/agent-studio/{id}/decisions`.
- **Research desk** — an on-demand multi-agent debate: Alpha/Sentiment/Technical/Risk analysts each give
  a view and a Reviewer synthesises a proposal (`POST /api/agent-studio/{id}/debate`).
- **Memory** — the agent remembers each decision and recalls recent memory into its next prompt for
  continuity (`GET /api/agent-studio/{id}/memory`).

Each roster row's **Details** opens the agent's decision feed (with Approve/Reject on pending orders),
its memory, and a Run-debate tab.

## Scope

Shipped: the full agent lifecycle, the deterministic safety gate, the 24/7 runtime, the human-in-the-loop
approval gate, the audit ledger, and the **live cTrader Open API integration** — the account-state store
(reads real balance, positions and open exposure in lots) and the order executor (places real market
orders, lots→volume via the symbol lot size), both resolving each managed account's OAuth credentials and
degrading safely when an account is not linked. **Requires the Anthropic API key** for the model to
generate orders (until then the engine holds); still to come are multi-agent debate roles and layered
memory/reflection. The runtime is off unless `App:Ai:AgentRuntimeEnabled` is set, so live trading only
happens on an explicit, fully-consented opt-in.

## Managed accounts and editing

When creating an agent you pick the trading account(s) it manages — **at least one is required at
creation** (the *Create* button is disabled until one is selected, and the create endpoint rejects an
empty selection). Every agent can be **edited** afterwards (name, temperament, autonomy, and managed accounts) from the
pencil icon on its roster row. Lifecycle controls (details, edit, start, stop, kill) are icon buttons,
each disabled in states where the action does not apply.
