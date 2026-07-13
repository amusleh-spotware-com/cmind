---
description: "Tutti i credenziali di cui le suite di test hanno bisogno vivono in un singolo file gitignored: secrets/dev-credentials.local.json. Copia il modello impegnato e compila quello che hai"
---

# Credenziali Dev — un file per ogni test

Tutti i credenziali di cui le suite di test hanno bisogno vivono in un singolo file gitignored: `secrets/dev-credentials.local.json`. Copia il modello impegnato e compila quello che hai — ogni valore è opzionale e i test che hanno bisogno di un valore mancante saltano in modo pulito.

```bash
mkdir -p secrets
cp dev-credentials.example.json secrets/dev-credentials.local.json
# modifica secrets/dev-credentials.local.json
```

## Cosa legge ogni livello di test

| Tier | Ha bisogno | Da |
|------|-------|------|
| **Unit** (`tests/UnitTests`) | niente | — deterministico, nessun segreto, nessuna rete |
| **Integration** (`tests/IntegrationTests`) | Postgres | Testcontainers (Docker) — auto |
| **Live copy** (`tests/IntegrationTests/CopyLive`) | app OpenAPI + cache di token | `OpenApi.App`, `OpenApi.Tokens` |
| **E2E onboarding** (`tests/E2ETests/CopyLive`) | app OpenAPI + accessi cID | `OpenApi.App`, `OpenApi.Cids` |
| **E2E run/backtest reale** (`CBotRealRunBacktestTests`) | un accesso cID + un numero di account **demo** | `OpenApi.Cids[].{Username,Password,Accounts}` |
| **Funzioni AI** | Chiave Anthropic | `Ai.ApiKey` (impostato ⇒ le funzioni AI restituiscono disabilitato, l'app funziona ancora) |

## Schema

Vedi `dev-credentials.example.json` nella radice del repo. Sezioni:

- `OpenApi.App` — `{ ClientId, ClientSecret }` dell'applicazione API cTrader Open.
- `OpenApi.Cids` — accessi ID cTrader utilizzati da onboarding OAuth headless. Ogni voce porta anche un array **`Accounts`** — i numeri di account di trading cTrader (il numero di accesso/account, ad es. `3635817`) in quel cID che l'infrastruttura di test è autorizzata a collegare nell'app e guidare. `CBotRealRunBacktestTests` legge la prima voce che ha un array `Accounts` non vuoto, aggiunge quel cID + account all'app, quindi realmente esegue e backtesta un cBot su di esso. **Metti qui solo i numeri di account demo** — mai un account dal vivo; i test di esecuzione/backtest reale piazzano ordini reali su qualunque account elenchì. `Accounts` vuoto/omesso ⇒ il test di esecuzione/backtest reale salta in modo pulito.
- `OpenApi.Tokens` — la cache di token multi-cID (una voce per cID autorizzato con il suo token di aggiornamento/accesso + elenco di account). Scritto automaticamente da onboarding e dal passaggio di aggiornamento del token; lo modifichi raramente a mano.
- `Owner` — accesso al proprietario del seme per l'app in E2E.
- `Database.ConnectionString` — solo quando puntì test a un Postgres esterno invece di Testcontainers.
- `Ai.ApiKey` — Chiave API Anthropic per le funzioni AI.

## Precedenza

1. **Le variabili di ambiente** sostituiscono tutto (ad es. `App__OwnerPassword`, `App:Ai:ApiKey`).
2. **`secrets/dev-credentials.local.json`** — il file unificato (preferito).
3. **File divisi legacy** — `openapi-test-app.local.json`, `openapi-cids.local.json`, `openapi-tokens.local.json` sono ancora letti quando il file unificato è assente, quindi le macchine esistenti continuano a funzionare. I nuovi setup dovrebbero usare il singolo file.

## Sicurezza

- `secrets/` e `*.local.json` sono gitignored — nulla qui è mai impegnato.
- I test di copia dal vivo si rifiutano di essere eseguiti su account non-demo (gli account `IsLive` vengono filtrati da `LiveCopyFixture`). Mantieni solo gli account demo nella cache di token.
- Le esecuzioni in-cluster (Kubernetes) montano il file come un Segreto di sola lettura; gli aggiornamenti di token vengono mantenuti in memoria e il write-back di sola lettura è un silent no-op.
