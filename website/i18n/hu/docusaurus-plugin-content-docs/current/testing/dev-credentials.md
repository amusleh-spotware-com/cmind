---
description: "A tesztkészletek összes hitelesítési adata egyetlen gitignore-olt fájlban él: secrets/dev-credentials.local.json. Másold a verziókövetett sablont és töltsd ki amid van"
---

# Fejlesztői hitelesítési adatok — egy fájl minden teszthez

A tesztkészletek összes hitelesítési adata egyetlen gitignore-olt fájlban él:
`secrets/dev-credentials.local.json`. Másold a verziókövetett sablont és töltsd ki, amid
van — minden érték opcionális, és a hiányzó értéket igénylő tesztek tisztán kihagyódnak.

```bash
mkdir -p secrets
cp dev-credentials.example.json secrets/dev-credentials.local.json
# szerkeszd a secrets/dev-credentials.local.json-t
```

## Mit olvas az egyes teszt-szintek

| Szint | Kell neki | Forrás |
|------|-------|------|
| **Unit** (`tests/UnitTests`) | semmi | — determinisztikus, nincs titok, nincs hálózat |
| **Integráció** (`tests/IntegrationTests`) | Postgres | Testcontainers (Docker) — automatikus |
| **Élő másolás** (`tests/IntegrationTests/CopyLive`) | OpenAPI alkalmazás + token gyorsítótár | `OpenApi.App`, `OpenApi.Tokens` |
| **E2E onboarding** (`tests/E2ETests/CopyLive`) | OpenAPI alkalmazás + cID bejelentkezések | `OpenApi.App`, `OpenApi.Cids` |
| **E2E valós futtatás/backteszt** (`CBotRealRunBacktestTests`) | egy cID bejelentkezés + egy **demo** számlaszám | `OpenApi.Cids[].{Username,Password,Accounts}` |
| **AI funkciók** | Anthropic kulcs | `Ai.ApiKey` (ha nincs beállítva ⇒ az AI funkciók letiltva térnek vissza, az app tovább fut) |
| **Élő gazdasági naptár források** (`tests/IntegrationTests/Calendar/CalendarSourceLiveTests`) | FRED / BLS API kulcsok | `Calendar.FredApiKey`, `Calendar.BlsApiKey` (ha nincs beállítva ⇒ az adott forrás élő tesztje kihagyódik; a kulcs nélküli központi banki menetrend továbbra is működik) |

## Séma

Lásd a `dev-credentials.example.json`-t a repó gyökerében. Szekciók:

- `OpenApi.App` — a cTrader Open API alkalmazás `{ ClientId, ClientSecret }` értékei.
- `OpenApi.Cids` — a fejnélküli OAuth onboarding által használt cTrader ID bejelentkezések. Minden
  bejegyzés hordoz egy **`Accounts`** tömböt is — az adott cID alá tartozó cTrader kereskedési
  számlaszámok (a bejelentkezési/számlaszám, pl. `3635817`), amelyeket a teszt-infrastruktúra
  bekapcsolhat az appba és vezérelhet. A `CBotRealRunBacktestTests` az első nem üres `Accounts`
  tömbbel rendelkező bejegyzést olvassa, hozzáadja azt a cID + számlát az apphoz, majd valóban futtat
  és backtesztel rajta egy cBot-ot. **Csak demo számlaszámokat tegyél ide** — soha ne élő számlát; a
  futtatás/backteszt tesztek valós megbízásokat adnak arra a számlára, amit megadsz. Üres/hiányzó
  `Accounts` ⇒ a valós futtatás/backteszt teszt tisztán kihagyódik.
- `OpenApi.Tokens` — a több-cID-es token gyorsítótár (bejegyzésenként egy engedélyezett cID a
  refresh/access tokenjével + számlalistával). Az onboarding és a token-frissítő lépés automatikusan
  írja; ritkán kell kézzel szerkesztened.
- `Owner` — a kiinduló tulajdonosi bejelentkezés az apphoz E2E alatt.
- `Database.ConnectionString` — csak akkor, ha a teszteket külső Postgresre irányítod a
  Testcontainers helyett.
- `Ai.ApiKey` — Anthropic API kulcs az AI funkciókhoz.
- `Calendar.FredApiKey` — [FRED](https://fredaccount.stlouisfed.org/apikeys) (St. Louis Fed)
  API kulcs. Az elsődleges gazdasági naptár értékforrás (kamatok, infláció, foglalkoztatás).
- `Calendar.BlsApiKey` — [BLS](https://data.bls.gov/registrationEngine/) (US Bureau of Labor
  Statistics) v2 regisztrációs kulcs (CPI, PPI, foglalkoztatás, JOLTS). Hiányában az alacsony
  kvótájú nyilvános szint.

  Mindkettő ugyanazt a `FredSource`/`BlsSource` kódot hajtja meg, amelyet az adatbetöltő worker
  használ. Kulcs jelenlétében a `CalendarSourceLiveTests` a valódi szolgáltatóhoz fordul és
  ellenőrzi, hogy megfigyelések térnek vissza; hiányában az adott forrás tesztje tisztán kihagyódik.
  Az app futásidőben is olvassa ezeket az `App:Calendar:FredApiKey` / `App:Calendar:BlsApiKey`
  beállításból (a környezeti változók felülírják — pl. `FRED_API_KEY`, `BLS_API_KEY`).

## Elsőbbség

1. **Környezeti változók** mindent felülírnak (pl. `App__OwnerPassword`, `App:Ai:ApiKey`).
2. **`secrets/dev-credentials.local.json`** — az egyesített fájl (preferált).
3. **Régi különálló fájlok** — az `openapi-test-app.local.json`, `openapi-cids.local.json`,
   `openapi-tokens.local.json` továbbra is olvasva vannak, ha az egyesített fájl hiányzik, így a
   meglévő gépek tovább működnek. Új beállításoknál az egyetlen fájlt használd.

## Biztonság

- A `secrets/` és a `*.local.json` gitignore-olt — semmi sem kerül ide commitolásra.
- Az élő másolási tesztek nem futnak nem-demo számlák ellen (az `IsLive` számlákat a
  `LiveCopyFixture` kiszűri). Csak demo számlákat tarts a token gyorsítótárban.
- A klaszteren belüli (Kubernetes) futások a fájlt csak olvasható Secretként csatolják; a
  token-frissítések a memóriában maradnak, és a csak olvasható visszaírás csendes no-op.
