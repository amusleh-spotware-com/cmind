---
description: "Backtest Integrity Lab — deterministic, fund-grade overfitting statistics (Probabilistic & Deflated Sharpe, t-stat) that turn a raw backtest into a Robust / Fragile / Overfit verdict, correcting for how many configurations you tried."
---

# Backtest Integrity Lab

Retail platforms show you a backtest's Sharpe or net profit and stop there. Institutions never trust a
raw backtest — they ask whether the result survives **correction for selection bias and the number of
configurations tried**. The Backtest Integrity Lab brings that check to cMind. It is **deterministic
math** (no AI, no external calls), so the verdict is reproducible and every number is explainable.

Open it at **cBots → Integrity** (`/quant/integrity`).

## What it computes

Given a return series (or an equity/balance curve) and the number of parameter sets you tried to arrive
at it, the analyzer reports:

- **Sharpe ratio** — per-period and annualized (square-root-of-time).
- **Probabilistic Sharpe Ratio (PSR)** — the confidence that the *true* Sharpe beats the benchmark,
  accounting for track-record length, skewness and kurtosis (Bailey & López de Prado, 2012). A short or
  fat-tailed record lowers it.
- **Deflated Sharpe Ratio (DSR)** — PSR measured against a **deflated benchmark**: the Sharpe you would
  expect from the *best of N random trials* under the null (the False Strategy Theorem). The more
  configurations you tried, the higher the bar — this is what catches overfitting.
- **t-statistic** of the mean return. Following Harvey, Liu & Zhu, a genuine edge should clear **t ≥ 3.0**,
  not the textbook 2.0.
- **Skewness / kurtosis** of the returns, which feed the PSR/DSR corrections.

## The verdict

| Verdict | Meaning | Rule |
|---|---|---|
| **Robust** | The edge survives the trials you ran. | DSR ≥ 95% **and** PSR ≥ 95% **and** \|t\| ≥ 3.0 |
| **Fragile** | Statistically alive but not convincingly so — do not size up on this alone. | between the two |
| **Overfit** | Most likely an artefact of selection bias, not a real edge. | DSR < 90% |

Every result carries a plain-English rationale so the "why" is never hidden.

## Trials — the number that matters

`Trials` is **how many parameter sets you tested** before picking this one. Testing one strategy and
testing ten thousand and keeping the best are wildly different things: the second manufactures a
high in-sample Sharpe by chance. Feeding the honest trial count is the whole point — it raises the
deflation and can move a "great" backtest to **Overfit**. When the native cTrader Console optimizer
lands, cMind feeds it the sweep's real grid size automatically.

## Inputs

- **Periodic returns** — one number per period (e.g. `0.01` = +1%). At least two.
- **Equity / balance curve** — cMind derives the consecutive simple returns for you.
- Or run it straight on a completed backtest: `POST /api/quant/integrity/backtest/{instanceId}` reads the
  stored report's equity curve.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Returns the verdict, all metrics, and the rationale. `POST /api/quant/integrity/backtest/{id}` runs the
same analysis on a completed backtest you own.

## Why it is reliable

The statistics are pure functions in the domain core (`Core.Quant`) with zero infrastructure
dependencies — they cannot be taken down by a network blip, and they are pinned by golden-vector unit
tests against the published formulas. The normal CDF/inverse are closed-form approximations
(Abramowitz-Stegun / Acklam), so the same inputs always yield the same verdict.
