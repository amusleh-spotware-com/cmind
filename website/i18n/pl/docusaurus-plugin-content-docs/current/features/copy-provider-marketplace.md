---
description: "Browsable directory copy strategies. Provider publishes copy profile jako listing z verified-live badge (strategy source account trade real money, nie…"
---

# Copy provider marketplace (Phase 4)

Browsable directory copy strategies. Provider **publishes** copy profile jako listing z **verified-live** badge (strategy source account trade real money, nie demo) plus performance fee. Followers browse marketplace, ranked przez performance score projected z execution-transparency data.

## Model

- `CopyProviderListing` = aggregate: `UserId`, `ProfileId`, display name, description, performance fee, `VerifiedLive`, `Published` + `PublishedAt`. One listing per profile (unique index).
- **Verified-live** derived przy publish time z profile source `TradingAccount.IsLive` — provider nie może self-assert.
- Performance stats **nie stored na listing** — read-model projection nad `CopyExecution` transparency log (fill rate, avg latency, avg realized slippage), więc marketplace zawsze odzwierciedlanie live execution quality.

## Ranking

`CopyEndpoints.MarketplaceScore(fillRate, avgLatencyMs, avgSlippagePoints, verifiedLive)` → 0–100 score: fill rate dominates (×60), low latency + low slippage add (×20 each), verified-live badge add small trust bonus. Deterministyczne + monotonic, więc ordering stable.

## API

- `POST /api/copy/profiles/{id}/publish` — publish/update profile listing (`DisplayName`, `Description`, `PerformanceFeePercent`); verified-live set z source account.
- `DELETE /api/copy/profiles/{id}/publish` — unpublish.
- `GET /api/copy/marketplace` — wszystkie published listings, ranked, każdy z performance summary (executions, fill rate, avg latency, avg slippage, score) + verified-live badge.

## Testy

- **Unit** (`CopyProviderListingTests`) — aggregate invariants: display name required; publish set timestamp; unpublish hide; update replace display fields + fee + badge.
- **Integracja** (`CopyMarketplaceTests`, real Postgres) — published listing persist z badge; one listing per profile (unique index); ranking score prefer verified/high-fill providers.

Copy host untouched (listings + read model tylko), więc copy DST stress suite unaffected.
