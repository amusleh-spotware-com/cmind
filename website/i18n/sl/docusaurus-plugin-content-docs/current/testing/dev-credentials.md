---
description: "Vse poverilnice ki jih testni suite potrebuje živijo v eni gitignored datoteki: secrets/dev-credentials.local.json. Kopirajte commitment predlogo in napolnite kar imate — vsaka vrednost je izbirna in testi ki potrebujejo manjkajočo vrednost preskočijo gladko."
---

# Dev poverilnice — ena datoteka za vsak test

Vse poverilnice ki jih testni suite potrebuje živijo v eni gitignored datoteki:
`secrets/dev-credentials.local.json`. Kopirajte commitment predlogo in napolnite kar imate —
vsaka vrednost je izbirna in testi ki potrebujejo manjkajočo vrednost preskočijo gladko.

```bash
mkdir -p secrets
cp dev-credentials.example.json secrets/dev-credentials.local.json
# uredi secrets/dev-credentials.local.json
```

## Kaj vsaka testna plast bere

| Plast | Potrebuje | Od |
|------|-----------|-----|
| **Enote** (`tests/UnitTests`) | nič | — deterministično, brez skrivnosti, brez omrežja |
| **Integracija** (`tests/IntegrationTests`) | Postgres | Testcontainers (Docker) — avtomatsko |
| **Live copy** (`tests/IntegrationTests/CopyLive`) | OpenAPI app + predpomnilnik žetona | `OpenApi.App`, `OpenApi.Tokens` |
| **E2E onboarding** (`tests/E2ETests/CopyLive`) | OpenAPI app + cID prijave | `OpenApi.App`, `OpenApi.Cids` |
| **E2E real run/backtest** (`CBotRealRunBacktestTests`) | cID prijava + **demo** številka računa | `OpenApi.Cids[].{Username,Password,Accounts}` |
| **AI funkcije** | Anthropic ključ | `Ai.ApiKey` (nenastavljeno ⇒ AI funkcije vrnejo onemogočeno, app še vedno teče) |
| **Live economic-calendar sources** (`tests/IntegrationTests/Calendar/CalendarSourceLiveTests`) | FRED / BLS API keys | `Calendar.FredApiKey`, `Calendar.BlsApiKey` (unset ⇒ that source's live test skips; the keyless central-bank schedule still works) |

## Shema

Glej `dev-credentials.example.json` na korenu repo. Razdelki:

- `OpenApi.App` — `{ ClientId, ClientSecret }` cTrader Open API aplikacije.
- `OpenApi.Cids` — cTrader ID prijave ki jih headless OAuth onboarding uporablja. Vsak vnos prav tako
  nosi **`Accounts`** polje — številke trgovalnih računov cTrader (login/številka računa,
  npr. `3635817`) pod tem cID ki jih ima testna infrastruktura dovoljenje povezati v aplikacijo in
  pognati. `CBotRealRunBacktestTests` bere prvi vnos ki ima neprazen `Accounts` polje,
  doda ta cID + račun v aplikacijo, nato resnično zažene in backtesta cBot na njem. **Tukaj dajte samo
  demo številke računov** — nikoli live račun; testi run/backtest postavijo realna naročila na
  kar koli ste našteli. Prazen/izpuščen `Accounts` ⇒ real run/backtest test preskoči gladko.
- `OpenApi.Tokens` — predpomnilnik žetona več-cID (en vnos na avtoriziran cID z
  refresh/access žetonom + seznamom računov). Samodejno napisano od onboarding in od
  koraka osveževanja žetona; redko ga urejate ročno.
- `Owner` — natančen lastnik prijave za aplikacijo pod E2E.
- `Database.ConnectionString` — samo ko usmerjate teste na zunanjo Postgres namesto
  Testcontainers.
- `Ai.ApiKey` — Anthropic API ključ za AI funkcije.
- `Calendar.FredApiKey` — [FRED](https://fredaccount.stlouisfed.org/apikeys) (St. Louis Fed) API key. The primary economic-calendar value source (interest rates, inflation, employment).
- `Calendar.BlsApiKey` — [BLS](https://data.bls.gov/registrationEngine/) (US Bureau of Labor Statistics) v2 registration key (CPI, PPI, employment, JOLTS). Absent ⇒ the low-quota public tier.

  Both feed the exact `FredSource`/`BlsSource` the ingestion worker uses. With a key present, `CalendarSourceLiveTests` hits the real provider and asserts observations come back; absent, that source's test skips cleanly. The app also reads these at runtime via `App:Calendar:FredApiKey` / `App:Calendar:BlsApiKey` (environment variables override — e.g. `FRED_API_KEY`, `BLS_API_KEY`).

## Prioriteta

1. **Spremenljivke okolja** preglasijo vse (npr. `App__OwnerPassword`, `App:Ai:ApiKey`).
2. **`secrets/dev-credentials.local.json`** — enotna datoteka (prednost).
3. **Stare razdeljene datoteke** — `openapi-test-app.local.json`, `openapi-cids.local.json`,
   `openapi-tokens.local.json` se še berejo ko enotna datoteka manjka, torej obstoječi
   stroji še delujejo. Nova okolja naj uporabljajo enotno datoteko.

## Varnost

- `secrets/` in `*.local.json` sta v .gitignore — tukaj nič nikoli ni commitano.
- Live copy testi zavrnejo zagon proti ne-demo računom (`IsLive` računi so filtrirani
  iz `LiveCopyFixture`). V predpomnilniku žetonov hranite samo demo račune.
- Znotraj-gručni (Kubernetes) zagon mounta datoteko kot samo-branje Secret; osvežitve žetona so
  v spominu in branje-samo write-back je tiho brez-opravljanje.
