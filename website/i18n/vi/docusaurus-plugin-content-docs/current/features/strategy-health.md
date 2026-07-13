---
description: "Monitor strategy performance — drawdown, Sharpe, win rate. Alerts when performance degrades."
---

# Strategy health

Monitor strategy performance — drawdown, Sharpe, win rate. Alerts khi performance degrades.

## KPIs

Tracked per agent / profile:

- **ROI** — return on initial capital.
- **Sharpe ratio** — risk-adjusted return.
- **Max drawdown** — peak-to-trough loss.
- **Win rate** — % profitable trades.
- **Profit factor** — gross profit / gross loss.

## Health checks

- Drawdown approaching limit → alert.
- Win rate dropping below threshold → alert.
- Sharpe deteriorating → warning.

## AI risk guard

Background service monitors all agents. Nếu health check fails:

- Auto-pause agent.
- Alert owner.
- Recommendations (scale down, review strategy).

Xem [ai.md](./ai.md) cho risk guard details.
