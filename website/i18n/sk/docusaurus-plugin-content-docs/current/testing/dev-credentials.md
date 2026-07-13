---
description: "Všetky poverenia test suite potreby živí v jeden gitignored súbor: secrets/dev-credentials.local.json. Kopírovať commit šablónu a vyplniť, čo vy"
---

# Dev poverenia — jeden súbor pre každý test

Všetky poverenia test suite potreby živí v jeden gitignored súbor:
`secrets/dev-credentials.local.json`. Kopírovať commit šablónu a vyplniť, čo vy
mať — každá hodnota je voliteľný a testy, ktorí potreba chýbajúci hodnota preskočiť čisto.

```bash
mkdir -p secrets
cp dev-credentials.example.json secrets/dev-credentials.local.json
# upraviť secrets/dev-credentials.local.json
```

## Čo každý test vrstva čítajú

| Vrstva | Potreby | Z |
|------|-------|------|
| **Jednotka** (`tests/UnitTests`) | nič | — deterministický, žádny tajomstvo, žádny sieť |
| **Integrácia** (`tests/IntegrationTests`) | Postgres | Testcontainers (Docker) — auto |
| **Live kopírovať** (`tests/IntegrationTests/CopyLive`) | OpenAPI app + token cache | `OpenApi.App`, `OpenApi.Tokens` |
| **E2E onboarding** (`tests/E2ETests/CopyLive`) | OpenAPI app + cID prihláš | `OpenApi.App`, `OpenApi.Cids` |
| **E2E reálny spustiť/backtest** (`CBotRealRunBacktestTests`) | a cID prihláš + a **demo** číslo účtu | `OpenApi.Cids[].{Username,Password,Accounts}` |
| **AI funkcie** | Anthropic kľúč | `Ai.ApiKey` (nastavený ⇒ AI funkcie vrátenie zakázaný, aplikácia stále beží) |

## Schéma

Vidieť `dev-credentials.example.json` v repo root. Sekcie:

- `OpenApi.App` — `{ ClientId, ClientSecret }` z cTrader Open API aplikácia.
- `OpenApi.Cids` — cTrader ID prihláš používaný headless OAuth onboarding. Každý zápisom tiež
  niesol a **`Accounts`** pole — cTrader obchodný-účet čísla (prihláš/číslo účtu,
  napr. `3635817`) pod, že cID, že test infraštruktúra je povolené linkovať do aplikácia a
  jednotka. `CBotRealRunBacktestTests` čítajú prvý zápisom, ktorý má non-empty `Accounts` pole,
  pridajú, že cID + účtu na aplikácia, potom naozaj spustiť a backtest a cBot na to. **Umiestniť iba
  demo čísla účtu tu** — nikdy live účet; spustiť/backtest testy miesto reálny objednávky na
  čokoľvek účet vy zoznam. Prázdny/vynechaný `Accounts` ⇒ reálny spustiť/backtest test preskočiť čisto.
- `OpenApi.Tokens` — multi-cID token cache (jeden zápisom za autorizovaný cID s Its
  refresh/prístupový token + účet zoznam). Napísané automaticky podľa onboarding a podľa
  token-refresh krok; vy zriedka upraviť to manuálne.
- `Owner` — semeno vlastník prihláš na aplikácia pod E2E.
- `Database.ConnectionString` — iba keď smerujúce testy na extern Postgres namiesto
  Testcontainers.
- `Ai.ApiKey` — Anthropic API kľúč pre AI funkcie.

## Prednosť

1. **Premenné prostredia** prepísať všetko (napr. `App__OwnerPassword`, `App:Ai:ApiKey`).
2. **`secrets/dev-credentials.local.json`** — jednotný súbor (uprednostnené).
3. **Dedičný rozdelené súbory** — `openapi-test-app.local.json`, `openapi-cids.local.json`,
   `openapi-tokens.local.json` sú stále čítajú keď jednotný súbor chýba, takže existujúce
   stroje udržiavať pracovný. Nový nastavenie by malo byť rovnakého súboru.

## Bezpečnosť

- `secrets/` a `*.local.json` sú gitignored — nič tu nikdy nie je commit.
- Live kopírovať testy odmietnuť spustiť voči non-demo účtov (`IsLive` účty sú filtrovaní
  von podľa `LiveCopyFixture`). Udržiavať iba demo účty v token cache.
- In-cluster (Kubernetes) spustí montáž súbor ako read-only Secret; token refreshes sú
  udržiavať v pamäti a read-only write-back je ticho no-op.
