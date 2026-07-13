---
description: "Trading Journal & Coach — analyses Twoje own runs i backtests dla behavioural leaks (over-concentration, repeated failures, losing bias) i coaches Cię na strategy którą masz już. Deterministyczne, z optional AI narrative."
---

# Trading Journal & Coach

Newest genuinely-useful category z AI-for-trading to nie predicting market — to analysing
*Twoje* behaviour. Trading Journal turns Twoją history z runs i backtests do honest feedback więc
możesz improve strategy którą już masz.

Otwórz **AI → Trading Journal** (`/journal`).

## Co surfaces

Z Twoich instances (runs i backtests) to computes, deterministycznie:

- **Win / loss / failure counts i win rate** across Twoje backtests;
- **Behavioural insights** — leaks które quietly cost retail traders:
  - **Over-concentration** — most z Twojej activity to w jeden symbol;
  - **Repeated failures** — high share z runs failed do build lub configure;
  - **Losing bias** — more losing niż winning backtests (z nudge do run Integrity Lab i
    check czy edge to real);
  - clean bill z health gdy none z above applies.

```http
GET /api/journal
```

## Dlaczego jest niezawodny

Behavioural analysis to pure, deterministyczne domain code (`Core.Journal`) z żadną infrastrukturą
dependency — unit-tested dla over-concentration, repeated failures, losing bias, balanced case i
empty account. Facts come first; AI coach (Portfolio Digest) to optional narrative layer
na top, gated na Anthropic API key, więc journal works fully bez AI configured.
