---
description: "Agent Studio — vytvářejte persona-driven, no-code trading agenty s charakterem a archetype, kteří spravují účty k vašim cílům pod Autonomy & Safety Kernel (risk envelope, circuit breaker, kill switch, verzovaný souhlas s disclaimerem)."
---

# Agent Studio

Agent Studio vám umožňuje vytvořit **trading agenta s charakterem** — bez kódu — a dát mu správu
vašich účtů k měřitelným cílům. Agent je jako personality-driven cBot: zvolíte archetype
a attitude, nastavíte guardrails, a běží pod **Autonomy & Safety Kernel**.

Otevřete **AI → Agent Studio** (`/agent-studio`).

## Vytvořte agenta

**New agent** dialog sbírá, no-code:

- **Name** a **archetype** — Scalper, Day Trader, Swing Trader, Position Trader, News Trader,
  Contrarian, Mean Reversion nebo Breakout/Momentum. Každý preset fixuje rozumnou kadenci a posture.
- **Attitude** — aggressiveness, patience and trend-following sliders.
- **Úroveň autonomie** — **Advisory** (pouze navrhuje) nebo **Approval-gated** (jedná pouze po vašem
  per-action approval). **Full Auto** (bez per-trade approval) navíc vyžaduje **risk envelope**
  a acceptance of the risk disclaimer before it can arm.

Persona se **deterministicky** kompiluje do agent's system prompt (žádný LLM ho neautoruje), takže the
same configuration vždy produces the same instructions — reproducibilní a auditabilní.

## Roster

Každý agent se ukazuje v control-room table: **který agent, jeho typ, kolik účtů spravuje, jeho
cíle, run status, a poslední akce**, s **Start / Stop / Kill** ovládacími prvky. Kill switch zastaví
běžícího agenta okamžitě.

## Bezpečnost je doménový invariant, ne nastavení

Vše co se dotýká peněz prochází přes **Autonomy & Safety Kernel**:

- **Risk envelope** — hard per-order limity (max denní ztráta, otevřená expozice, velikost pozice, leverage,
  po sobě jdoucí ztráty, objednávky/hodina, povolené symboly). Každá objednávka je proti němu validována před dispatch;
  breach je odmítnut, ne clamped. Vyžadováno před tím než agent může dosáhnout Full Auto.
- **Circuit breaker** — deterministicky zastaví nový risk na loss streak, denní-loss breach, **hard
  performance-goal breach**, nebo **AI-provider nedostupnost** (down nebo hallucinating model never opens
  fresh positions).
- **Verzovaný souhlas s disclaimerem** — one-time, verzované acceptance je required to arm Full Auto
  (legálně required consent, not per-trade approval); bumping the disclaimer forces re-consent.
- **Kill switch** — idempotentní emergency halt na každém běžícím agentovi.

## Cíle

Dejte agentovi **měřitelné objectives** — např. *drž max drawdown pod 4%*, *profit factor alespoň
1.5*, *win rate ≥ 55%*. Každý target je **Hard** (guardrail — breach trips circuit breaker) nebo
**Soft** (steers reasoning only), vyhodnoceno jako On-track / At-risk / Breached.

## Decision pipeline

Jakmile je spuštěn, agent běží **24/7 supervised loop** (`AgentRuntimeService`). Každý tick, pro každý
spravovaný účet, it: reads the **deterministic account state** (ground truth, never the model's memory);
asks the decision engine for a move; passes it through the **safety gate** (`AgentDecisionProcessor`) —
autonomy level → circuit breaker → risk envelope; writes an append-only **`AgentDecisionRecord`**; and
halts or executes as the gate directs. Loop je **fault-isolated** (one agent's failure never touches
another or the host) and **safe by default**: it is inert unless AI is configured *and*
`App:Ai:AgentRuntimeEnabled` is set, and it never opens fresh risk while the AI provider is unavailable.

- **Approval gate** — **Approval-gated** agent's proposed order is recorded as **Pending** and does
  nothing until the owner approves it (`POST /api/agent-studio/{id}/decisions/{seq}/approve` or
  `/reject`); **Full Auto** clears through the envelope with no per-trade approval; **Advisory** only
  proposes.
- **Audit ledger** — každé rozhodnutí je replayable: reasoning (XAI), the evidence it cited, the gate
  verdict, the order intent and whether it executed, na `GET /api/agent-studio/{id}/decisions`.
- **Research desk** — an on-demand multi-agent debate: Alpha/Sentiment/Technical/Risk analysts each give
  a view and a Reviewer synthesises a proposal (`POST /api/agent-studio/{id}/debate`).
- **Memory** — agent remembers each decision and recalls recent memory into its next prompt for
  continuity (`GET /api/agent-studio/{id}/memory`).

Každý roster row's **Details** otevírá agent's decision feed (s Approve/Reject on pending orders),
jeho memory, and a Run-debate tab.

## Rozsah

Odesláno: full agent lifecycle, the deterministic safety gate, 24/7 runtime, human-in-the-loop
approval gate, audit ledger, and the **live cTrader Open API integration** — account-state store
(reads real balance, positions and open exposure in lots) and order executor (places real market
orders, lots→volume via symbol lot size), both resolving each managed account's OAuth credentials and
degrading safely when an account is not linked. **Vyžaduje Anthropic API klíč** pro model to
generate orders (until then the engine holds); still to come are multi-agent debate roles and layered
memory/reflection. Runtime is off unless `App:Ai:AgentRuntimeEnabled` is set, takže live trading only
happens on an explicit, fully-consented opt-in.
