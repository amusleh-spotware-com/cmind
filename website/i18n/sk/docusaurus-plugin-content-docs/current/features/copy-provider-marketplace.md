---
description: "Prehliadateľný adresár copy stratégií. Poskytovateľ publikuje copy profil ako inzerát s verified-live badge (zdrojový účet stratégie obchoduje skutočné peniaze, nie demo) plus performance fee."
---

# Copy provider marketplace (Fáza 4)

Prehliadateľný adresár copy stratégií. Poskytovateľ **publikuje** copy profil ako inzerát s **verified-live**
badge (zdrojový účet stratégie obchoduje skutočné peniaze, nie demo) plus performance fee. Sledovatelia
prehliadajú marketplace, hodnotené podľa performance score projektovanej z execution-transparency dát.

## Model

- `CopyProviderListing` = aggregate: `UserId`, `ProfileId`, display name, description, performance fee,
  `VerifiedLive`, `Published` + `PublishedAt`. Jeden inzerát na profil (unique index).
- **Verified-live** odvodená pri publish-time z profile source `TradingAccount.IsLive` — poskytovateľ
  nemôže self-assert.
- Performance štatistiky **nie sú uložené na inzeráte** — read-model projekcia cez `CopyExecution`
  transparency log (fill rate, avg latency, avg realized slippage), takže marketplace vždy
  odráža live execution quality.

## Hodnotenie

`CopyEndpoints.MarketplaceScore(fillRate, avgLatencyMs, avgSlippagePoints, verifiedLive)` → 0–100 skóre:
fill rate dominuje (×60), nízka latencia + nízky slippage pridávajú (×20 každé), verified-live badge
pridáva malý trust bonus. Deterministic + monotonic, takže ordering stabilný.

## API

- `POST /api/copy/profiles/{id}/publish` — publikovať/aktualizovať profil inzerát (`DisplayName`,
  `Description`, `PerformanceFeePercent`); verified-live nastavené zo source účtu.
- `DELETE /api/copy/profiles/{id}/publish` — odpublikovať.
- `GET /api/copy/marketplace` — všetky publikované inzeráty, hodnotené, každý s performance
  súhrnom (executions, fill rate, avg latency, avg slippage, score) + verified-live badge.

## Testy

- **Jednotka** (`CopyProviderListingTests`) — aggregate invariants: display name required; publish
  nastaví timestamp; unpublish skryje; update nahradí display fields + fee + badge.
- **Integrácia** (`CopyMarketplaceTests`, reálny Postgres) — publikovaný inzerát perzistuje s badge;
  jeden inzerát na profil (unique index); ranking skóre preferuje verified/high-fill poskytovateľov.

Copy host nedotknutý (inzeráty + read model iba), takže copy DST stress suite nezávislá.
