---
description: "Pretraživi direktorijum strategija kopiranja. Provajder objavljuje profil kopiranja kao oglas sa badge-om verifikovanog-live (strategija izvornog računa trguje pravim novcem, ne demo) plus naknada za učinak."
---

# Marketplace provajdera kopiranja (Faza 4)

Pretraživi direktorijum strategija kopiranja. Provajder **objavljuje** profil kopiranja kao oglas sa **badge-om verifikovanog-live** (strategija izvornog računa trguje pravim novcem, ne demo) plus naknada za učinak. Pratioci pregledaju marketplace, rangirani po score-u performansi projektovanom iz podataka o transparentnosti izvršenja.

## Model

- `CopyProviderListing` = agregat: `UserId`, `ProfileId`, display name, description, performance fee, `VerifiedLive`, `Published` + `PublishedAt`. Jedan oglas po profilu (jedinstveni indeks).
- **Verified-live** izveden u trenutku objave iz izvornog `TradingAccount.IsLive` — provajder ne može sam sebi da dodeli.
- Performanse **nisu uskladištene na oglasu** — read-model projekcija preko `CopyExecution` transparency log-a (stopa popunjavanja, avg latencija, avg ostvareno proklizanje), tako da marketplace uvek odražava live kvalitet izvršenja.

## Rangiranje

`CopyEndpoints.MarketplaceScore(fillRate, avgLatencyMs, avgSlippagePoints, verifiedLive)` → 0–100 score: stopa popunjavanja dominira (×60), niska latencija + nisko proklizanje dodaju (×20 svako), verified-live badge dodaje mali trust bonus. Deterministički + monotoni, tako da je redosled stabilan.

## API

- `POST /api/copy/profiles/{id}/publish` — objavi/ažuriraj oglas profila (`DisplayName`, `Description`, `PerformanceFeePercent`); verified-live postavljen iz izvornog računa.
- `DELETE /api/copy/profiles/{id}/publish` — poništi objavu.
- `GET /api/copy/marketplace` — svi objavljeni oglasi, rangirani, svaki sa performansnim rezimeom (izvršenja, stopa popunjavanja, avg latencija, avg proklizanje, score) + verified-live badge.

## Testovi

- **Unit** (`CopyProviderListingTests`) — agregatni invarijanti: display name obavezan; publish postavlja timestamp; unpublish skriva; update menja display polja + fee + badge.
- **Integration** (`CopyMarketplaceTests`, real Postgres) — objavljen oglas perzistira sa badge-om; jedan oglas po profilu (jedinstveni indeks); rangiranje score preferira verified/high-fill provajdere.

Copy host nedodirnut (samo oglasi + read model), tako da copy DST stress suite nije afectiran.
