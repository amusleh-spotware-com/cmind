---
description: "Public marketplace — traders browse copy providers by track record, filters (drawdown, win-rate), follow + auto-manage copy profile."
---

# Provider marketplace

Tradovejte nejlepší copy strategisty — seřazeno podle track recordu.

## Listing

Copy-provider chce být veřejný `GET /api/copy/marketplace/providers`:

1. Opt-in: `PUT /api/copy/profiles/{id}` set `IsPublic: true`
2. Set metadata: descripton, risk level, target audience
3. Appear na `/copy-trading/marketplace` — seznam + cards

Tradera vidí:
- Equity curve (30d, 90d, YTD)
- Win rate, Sharpe, max drawdown
- Fee rate (if charged)
- Follow button

## Follow (Subscribe)

Traders `POST /api/copy/subscribe` — uživatél crates nový `CopyProfile` pointing master, auto-configure recommended settings.

Master vidí počet subscribers, total AUM (sum of slave equities).

## Rankings & Discovery

Public `/api/copy/marketplace/top` — top 10 by Sharpe / win-rate.

Explore — filter by drawdown, symbols, account size requirements.

## Fraud protection

- Veřejný track record je append-only (audit-trail)
- Drawdown + equity podepřeno live Open API readerů (nemůže fake)
- Report button — odebrat podezřelého providera

Regulátor compliance: seznam + čas registrace veřejného.

Viz [features/copy-trading.md](copy-trading.md) + [features/copy-performance-fees.md](copy-performance-fees.md).
