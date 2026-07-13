---
description: "Browsable directory of copy strategies. Provider publishes copy profile as listing with verified-live badge (strategy source account trade real money, not…"
---

# Copy provider marketplace (Phase 4)

Browsable directory των copy strategies. Provider **publishes** copy profile ως listing με **verified-live** badge (strategy source account χρηματοδοτεί πραγματικά χρήματα, όχι demo) plus performance fee. Followers browse marketplace, ranked by performance score projected από execution-transparency data.

## Model

- `CopyProviderListing` = aggregate: `UserId`, `ProfileId`, display name, description, performance fee, `VerifiedLive`, `Published` + `PublishedAt`. Ένα listing ανά profile (unique index).
- **Verified-live** derived κατά publish time από profile source `TradingAccount.IsLive` — ο provider δεν μπορεί να self-assert.
- Performance stats **δεν αποθηκεύονται σε listing** — read-model projection πάνω από `CopyExecution` transparency log (fill rate, avg latency, avg realized slippage), ώστε το marketplace πάντα αντικατοπτρίζει live execution quality.

## Ranking

`CopyEndpoints.MarketplaceScore(fillRate, avgLatencyMs, avgSlippagePoints, verifiedLive)` → 0–100 score: fill rate dominates (×60), low latency + low slippage add (×20 κάθε), verified-live badge add small trust bonus. Deterministic + monotonic, ώστε ordering σταθερή.

## API

- `POST /api/copy/profiles/{id}/publish` — publish/update profile listing (`DisplayName`, `Description`, `PerformanceFeePercent`); verified-live set από source account.
- `DELETE /api/copy/profiles/{id}/publish` — unpublish.
- `GET /api/copy/marketplace` — όλα published listings, ranked, κάθε ένα με performance summary (executions, fill rate, avg latency, avg slippage, score) + verified-live badge.

## Tests

- **Unit** (`CopyProviderListingTests`) — aggregate invariants: display name required; publish set timestamp; unpublish hide; update replace display fields + fee + badge.
- **Integration** (`CopyMarketplaceTests`, real Postgres) — published listing persist με badge; ένα listing ανά profile (unique index); ranking score prefer verified/high-fill providers.

Copy host untouched (listings + read model μόνο), ώστε το copy DST stress suite unaffected.
