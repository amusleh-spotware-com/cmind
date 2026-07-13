---
description: "Brljivo direktorij od kopiranje strategije. Ponudnik objave kopiranje profil kot nabor z preverjena-živega značka (strategija vir račun trgovati pravi denar, ne…"
---

# Kopiranje ponudnik tržnica (Faza 4)

Brljivo direktorij od kopiranje strategije. Ponudnik **objave** kopiranje profil kot nabor z **preverjena-živega** značka (strategija vir račun trgovati pravi denar, ne demo) plus zmogljivost pristojbina. Sledilca brljivo tržnica, rangirani po zmogljivosti rezultat projicirani iz izvedbe-prosojnosti podatki.

## Model

- `CopyProviderListing` = agregat: `UserId`, `ProfileId`, pokaz ime, opis, zmogljivost pristojbina, `VerifiedLive`, `Published` + `PublishedAt`. Ena nabor na profil (edinstven indeks).
- **Preverjena-živega** izpeljana pri objavi čas od profila vir `TradingAccount.IsLive` — ponudnik ne more lastne-trdijo.
- Zmogljivosti statistika **ne shranjeno na nabor** — branja-model projekcija nad `CopyExecution` prosojnosti dnevnik (polni stopnja, povprečje zakasnitev, povprečje realizacija zdrsa), zato tržnica vedno odseva živo izvedbe kakovost.

## Rangiranje

`CopyEndpoints.MarketplaceScore(fillRate, avgLatencyMs, avgSlippagePoints, verifiedLive)` → 0–100 rezultat: polni stopnja dominira (×60), nizka zakasnitev + nizka zdrsa dodaj (×20 vsak), preverjena-živega značka dodaj majhen zaupanja premijo. Deterministična + monotoničen, zato naročanje stabilna.

## API

- `POST /api/copy/profiles/{id}/publish` — objavi/posodobiti profil nabor (`DisplayName`, `Description`, `PerformanceFeePercent`); proverjena-živega nastavljen iz vir račun.
- `DELETE /api/copy/profiles/{id}/publish` — razporediti.
- `GET /api/copy/marketplace` — vsi objavljeni nabori, rangirani, vsak z zmogljivosti povzetek (izvedbe, polni stopnja, povprečje zakasnitev, povprečje zdrsa, rezultat) + proverjena-živega značka.

## Testi

- **Enota** (`CopyProviderListingTests`) — agregat invarianti: pokaz ime potreben; objavi nastavljen časovni žig; razporediti skrit; posodobiti zamenja pokaz polja + pristojbina + značka.
- **Integracija** (`CopyMarketplaceTests`, pravi Postgres) — objavljeni nabor obstoj z značka; ena nabor na profil (edinstven indeks); rangiranje rezultat izbirka proverjena/visoko-polni ponudniki.

Kopiranje gostitelj nedotaknjen (nabori + branja model samo), zato kopiranje DST stresna niz neprisporen.
