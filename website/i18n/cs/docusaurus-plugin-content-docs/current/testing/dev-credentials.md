---
description: "Všechny přihlašovací údaje, které testovací sady potřebují, jsou v jediném souboru ignorovaném git: secrets/dev-credentials.local.json. Zkopírujte z commitované šablony a vyplňte, co máte — každá hodnota je volitelná a testy, které potřebují chybějící hodnotu, čistě přeskočí."
---

# Dev přihlašovací údaje — jeden soubor pro každý test

Všechny přihlašovací údaje, které testovací sady potřebují, žijí v jediném souboru ignorovaném gitem:
`secrets/dev-credentials.local.json`. Zkopírujte z commitované šablony a vyplňte, co máte — každá hodnota je volitelná a testy, které potřebují chybějící hodnotu, čistě přeskočí.

```bash
mkdir -p secrets
cp dev-credentials.example.json secrets/dev-credentials.local.json
# upravte secrets/dev-credentials.local.json
```

## Co každá testovací úroveň čte

| Úroveň | Potřebuje | Od | |
|------|-------|------|-----|
| **Unit** (`tests/UnitTests`) | nic | — deterministické, bez tajemství, bez sítě |
| **Integration** (`tests/IntegrationTests`) | Postgres | Testcontainers (Docker) — automaticky |
| **Live copy** (`tests/IntegrationTests/CopyLive`) | OpenAPI aplikace + cache tokenů | `OpenApi.App`, `OpenApi.Tokens` |
| **E2E onboarding** (`tests/E2ETests/CopyLive`) | OpenAPI aplikace + cID přihlášení | `OpenApi.App`, `OpenApi.Cids` |
| **E2E reálný běh/backtest** (`CBotRealRunBacktestTests`) | cID přihlášení + **demo** číslo účtu | `OpenApi.Cids[].{Username,Password,Accounts}` |
| **AI funkce** | Anthropic klíč | `Ai.ApiKey` (není nastaveno ⇒ AI funkce vracejí disabled, aplikace stále běží) |

## Schema

Viz `dev-credentials.example.json` v kořeni repozitáře. Sekce:

- `OpenApi.App` — `{ ClientId, ClientSecret }` cTrader Open API aplikace.
- `OpenApi.Cids` — cTrader ID přihlášení používaná headless OAuth onboardingu. Každá položka také
  nese pole **`Accounts`** — cTrader obchodní čísla účtů (přihlašovací/číslo účtu,
  např. `3635817`) pod tím cID, které je testovací infrastruktura oprávněna propojit do aplikace a
  ovládat. `CBotRealRunBacktestTests` čte první položku, která má neprázdné pole `Accounts`,
  přidá toto cID + účet do aplikace a pak skutečně spustí a backtestuje cBot na něm. **Sem dávejte pouze
  demo účty** — nikdy živý účet; testy běhu/backtestu umisťují reálné příkazy na jakýkoli účet,
  který uvedete. Prázdné/chybějící `Accounts` ⇒ test reálného běhu/backtestu čistě přeskočí.
- `OpenApi.Tokens` — cache tokenů pro více cID (jedna položka na autorizované cID s jeho
  refresh/access tokenem + seznam účtů). Automaticky zapisováno onboardingu a krokem obnovy
  tokenu; zřídka editováno ručně.
- `Owner` — seed vlastník přihlášení pro aplikaci pod E2E.
- `Database.ConnectionString` — pouze když směřujete testy na externí Postgres místo
  Testcontainers.
- `Ai.ApiKey` — Anthropic API klíč pro AI funkce.

## Priorita

1. **Proměnné prostředí** přepisují vše (např. `App__OwnerPassword`, `App:Ai:ApiKey`).
2. **`secrets/dev-credentials.local.json`** — sjednocený soubor (preferovaný).
3. **Legacy split soubory** — `openapi-test-app.local.json`, `openapi-cids.local.json`,
   `openapi-tokens.local.json` jsou stále čteny, když sjednocený soubor chybí, takže existující
   stroje fungují dál. Nové setupy by měly používat jediný soubor.

## Bezpečnost

- `secrets/` a `*.local.json` jsou gitignored — nic se tu nikdy necommituje.
- Testy live copy odmítají běžet proti nedemo účtům (`IsLive` účty jsou odfiltrovány
  `LiveCopyFixture`). V cache tokenů mějte pouze demo účty.
- In-cluster (Kubernetes) běhy mountují soubor jako read-only Secret; obnovy tokenů jsou
  drženy v paměti a read-only write-back je tichý no-op.
