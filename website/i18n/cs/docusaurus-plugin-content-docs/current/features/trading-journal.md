---
description: "Trader journal — annotate trades, capture decision logic, review outcomes, build playbook."
---

# Trading Journal

Nejlepší tradeři si píší journal. Capture "why I entered", "what I expected", "did I follow plan?" — offline learning.

## Entry

Trader může annotate closed trade na run detail page:

- What was the setup?
- Market conditions
- Risk/reward expectancy
- Outcome vs plan

Open POST /api/journal → store annotation.

## Review

GET /api/journal?symbol=EURUSD&tags=trend — search past trades, spot patterns.

Export → spreadsheet → offline analysis.

Gated na User+ role. No compliance-level requirement (personal learning tool).

Journal entries append-only (never deleted, GDPR erase só anonymizes).

Viz run detail page.
