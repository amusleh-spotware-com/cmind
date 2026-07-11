# Copy provider marketplace (Phase 4)

Browsable directory of copy strategies. Provider **publishes** copy profile as listing with **verified-live** badge (strategy source account trade real money, not demo) plus performance fee. Followers browse marketplace, ranked by performance score projected from execution-transparency data.

## Model

- `CopyProviderListing` = aggregate: `UserId`, `ProfileId`, display name, description, performance fee, `VerifiedLive`, `Published` + `PublishedAt`. One listing per profile (unique index).
- **Verified-live** derived at publish time from profile source `TradingAccount.IsLive` — provider can't self-assert.
- Performance stats **not stored on listing** — read-model projection over `CopyExecution` transparency log (fill rate, avg latency, avg realized slippage), so marketplace always reflect live execution quality.

## Ranking

`CopyEndpoints.MarketplaceScore(fillRate, avgLatencyMs, avgSlippagePoints, verifiedLive)` → 0–100 score: fill rate dominates (×60), low latency + low slippage add (×20 each), verified-live badge add small trust bonus. Deterministic + monotonic, so ordering stable.

## API

- `POST /api/copy/profiles/{id}/publish` — publish/update profile listing (`DisplayName`, `Description`, `PerformanceFeePercent`); verified-live set from source account.
- `DELETE /api/copy/profiles/{id}/publish` — unpublish.
- `GET /api/copy/marketplace` — all published listings, ranked, each with performance summary (executions, fill rate, avg latency, avg slippage, score) + verified-live badge.

## Tests

- **Unit** (`CopyProviderListingTests`) — aggregate invariants: display name required; publish set timestamp; unpublish hide; update replace display fields + fee + badge.
- **Integration** (`CopyMarketplaceTests`, real Postgres) — published listing persist with badge; one listing per profile (unique index); ranking score prefer verified/high-fill providers.

Copy host untouched (listings + read model only), so copy DST stress suite unaffected.