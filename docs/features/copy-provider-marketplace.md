# Copy provider marketplace (Phase 4)

A browsable directory of copy strategies. A provider **publishes** one of their copy profiles as a listing
with a **verified-live** badge (the strategy's source account trades real money, not demo) and a performance
fee; followers browse the marketplace ranked by a performance score projected from the execution-transparency
data.

## Model

- `CopyProviderListing` is an aggregate: `UserId`, `ProfileId`, display name, description, performance fee,
  `VerifiedLive`, `Published` + `PublishedAt`. One listing per profile (unique index).
- **Verified-live** is derived at publish time from the profile's source `TradingAccount.IsLive` — the
  provider can't self-assert it.
- Performance stats are **not stored on the listing** — they are a read-model projection over the
  `CopyExecution` transparency log (fill rate, average latency, average realized slippage), so the
  marketplace always reflects live execution quality.

## Ranking

`CopyEndpoints.MarketplaceScore(fillRate, avgLatencyMs, avgSlippagePoints, verifiedLive)` → a 0–100 score:
fill rate dominates (×60), low latency and low slippage add (×20 each), and a verified-live badge adds a
small trust bonus. Deterministic and monotonic, so ordering is stable.

## API

- `POST /api/copy/profiles/{id}/publish` — publish/update the profile's listing (`DisplayName`,
  `Description`, `PerformanceFeePercent`); verified-live is set from the source account.
- `DELETE /api/copy/profiles/{id}/publish` — unpublish.
- `GET /api/copy/marketplace` — all published listings, ranked, each with its performance summary
  (executions, fill rate, avg latency, avg slippage, score) and verified-live badge.

## Tests

- **Unit** (`CopyProviderListingTests`) — the aggregate invariants: display name required; publish sets the
  timestamp; unpublish hides; update replaces the display fields + fee + badge.
- **Integration** (`CopyMarketplaceTests`, real Postgres) — a published listing persists with its badge;
  only one listing per profile (unique index); the ranking score prefers verified/high-fill providers.

The copy host is untouched (listings + read model only), so the copy DST stress suite is unaffected.
