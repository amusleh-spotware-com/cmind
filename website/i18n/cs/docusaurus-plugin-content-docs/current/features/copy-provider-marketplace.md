---
description: "Procházitelný adresář kopírovacích strategií. Poskytovatel publikuje kopírovací profil jako nabídku s odznakem ověřeného-live (účet strategie obchoduje reálné peníze, ne demo) plus poplatek za výkon."
---

# Tržiště poskytovatelů kopírování (Fáze 4)

Procházetelný adresář kopírovacích strategií. Poskytovatel **publikuje** kopírovací profil jako nabídku s **odznakem ověřeného-live** (účet strategie obchoduje reálné peníze, ne demo) plus poplatek za výkon. Sledovatelé procházejí tržištěm, seřazení podle skóre výkonu vypočítaného z dat transparentnosti exekuce.

## Model

- `CopyProviderListing` = agregát: `UserId`, `ProfileId`, zobrazované jméno, popis, poplatek za výkon, `VerifiedLive`, `Published` + `PublishedAt`. Jedna nabídka na profil (unikátní index).
- **Ověřený-live** odvozen při publikování ze zdrojového `TradingAccount.IsLive` — poskytovatel nemůže self-assert.
- Statistiky výkonu **nejsou uloženy na nabídce** — projekce read modelu přes transparentnostní log `CopyExecution` (míra vyplnění, průměrná latence, průměrný realizovaný skluz), takže tržiště vždy odráží živou kvalitu exekuce.

## Ranking

`CopyEndpoints.MarketplaceScore(fillRate, avgLatencyMs, avgSlippagePoints, verifiedLive)` → skóre 0–100: míra vyplnění dominuje (×60), nízká latence + nízký skluz přidávají (×20 každé), odznak ověřeného-live přidává malý bonus důvěry. Deterministic + monotónní, takže řazení je stabilní.

## API

- `POST /api/copy/profiles/{id}/publish` — publikovat/aktualizovat nabídku profilu (`DisplayName`, `Description`, `PerformanceFeePercent`); ověřený-live nastaven ze zdrojového účtu.
- `DELETE /api/copy/profiles/{id}/publish` — zrušit publikování.
- `GET /api/copy/marketplace` — všechny publikované nabídky, seřazené, každá s přehledem výkonu (exekuce, míra vyplnění, průměrná latence, průměrný skluz, skóre) + odznak ověřeného-live.

## Testy

- **Unit** (`CopyProviderListingTests`) — invarianty agregátu: zobrazované jméno povinné; publikování nastaví časové razítko; zrušení publikování skryje; aktualizace nahradí zobrazovaná pole + poplatek + odznak.
- **Integration** (`CopyMarketplaceTests`, reálné Postgres) — publikovaná nabídka perzistuje s odznakem; jedna nabídka na profil (unifikátní index); ranking skóre preferuje ověřené/na-vysokou-míru-vyplnění poskytovatele.

Copy hostitel se nedotýká (pouze nabídky + read model), takže copy DST stress sada není ovlivněna.
