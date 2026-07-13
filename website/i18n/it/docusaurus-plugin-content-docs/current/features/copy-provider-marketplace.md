---
description: "Directory sfogliabile di strategie di copia. Il provider pubblica il profilo di copia come inserzione con badge verified-live (account sorgente della strategia scambia denaro reale, non…"
---

# Marketplace del provider di copia (Fase 4)

Directory sfogliabile di strategie di copia. Il provider **pubblica** il profilo di copia come inserzione con badge **verified-live** (account sorgente della strategia scambia denaro reale, non demo) più commissione di performance. I follower sfogliano il marketplace, classificati per punteggio di performance proiettato dai dati del registro di trasparenza dell'esecuzione.

## Modello

- `CopyProviderListing` = aggregato: `UserId`, `ProfileId`, nome visualizzato, descrizione, commissione di performance, `VerifiedLive`, `Published` + `PublishedAt`. Un inserzione per profilo (indice univoco).
- **Verified-live** derivato al momento della pubblicazione dal `TradingAccount.IsLive` di sorgente del profilo — il provider non può auto-asserire.
- Le statistiche di performance **non sono archiviate sull'inserzione** — proiezione del modello di lettura sul registro di trasparenza `CopyExecution` (tasso di fill, latenza media, slippage realizzato medio), così il marketplace riflette sempre la qualità di esecuzione live.

## Ranking

`CopyEndpoints.MarketplaceScore(fillRate, avgLatencyMs, avgSlippagePoints, verifiedLive)` → punteggio 0–100: il tasso di fill domina (×60), bassa latenza + basso slippage aggiungono (×20 ciascuno), badge verified-live aggiunge piccolo bonus di fiducia. Deterministico + monotonicamente, quindi l'ordinamento è stabile.

## API

- `POST /api/copy/profiles/{id}/publish` — pubblica/aggiorna l'inserzione del profilo (`DisplayName`, `Description`, `PerformanceFeePercent`); verified-live impostato dall'account sorgente.
- `DELETE /api/copy/profiles/{id}/publish` — depubblica.
- `GET /api/copy/marketplace` — tutte le inserzioni pubblicate, classificate, ognuna con riepilogo di performance (esecuzioni, tasso di fill, latenza media, slippage medio, punteggio) + badge verified-live.

## Test

- **Unità** (`CopyProviderListingTests`) — invarianti di aggregato: nome visualizzato richiesto; pubblica il timestamp di set; depubblica nascondi; aggiorna campi di visualizzazione di sostituzione + fee + badge.
- **Integrazione** (`CopyMarketplaceTests`, Postgres reale) — l'inserzione pubblicata persiste con badge; un'inserzione per profilo (indice univoco); il punteggio di ranking preferisce i provider verificati/high-fill.

L'host di copia non viene toccato (solo inserzioni + modello di lettura), quindi la suite di stress DST della copia è inalterata.
