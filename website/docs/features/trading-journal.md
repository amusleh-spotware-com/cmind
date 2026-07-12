---
description: "Trading Journal & Coach — analyses your own runs and backtests for behavioural leaks (over-concentration, repeated failures, a losing bias) and coaches you on the strategy you already have. Deterministic, with optional AI narrative."
---

# Trading Journal & Coach

The newest genuinely-useful category of AI-for-trading is not predicting the market — it is analysing
*your own* behaviour. The Trading Journal turns your history of runs and backtests into honest feedback so
you can improve the strategy you already have.

Open **AI → Trading Journal** (`/journal`).

## What it surfaces

From your instances (runs and backtests) it computes, deterministically:

- **Win / loss / failure counts and win rate** across your backtests;
- **Behavioural insights** — the leaks that quietly cost retail traders:
  - **Over-concentration** — most of your activity is in one symbol;
  - **Repeated failures** — a high share of runs failed to build or configure;
  - **Losing bias** — more losing than winning backtests (with a nudge to run the Integrity Lab and
    check the edge is real);
  - a clean bill of health when none of the above applies.

```http
GET /api/journal
```

## Why it is reliable

The behavioural analysis is pure, deterministic domain code (`Core.Journal`) with no infrastructure
dependency — unit-tested for over-concentration, repeated failures, losing bias, the balanced case and
the empty account. The facts come first; the AI coach (Portfolio Digest) is an optional narrative layer
on top, gated on the Anthropic API key, so the journal works fully without AI configured.
