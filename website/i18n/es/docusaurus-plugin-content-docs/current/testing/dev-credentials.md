---
description: "Todas las credenciales que las suites de prueba necesitan viven en un solo archivo gitignored: secrets/dev-credentials.local.json. Copia la plantilla comprometida y rellena lo que tengas"
---

# Dev credentials — un archivo para cada prueba

Todas las credenciales que las suites de prueba necesitan viven en un solo archivo gitignored: `secrets/dev-credentials.local.json`. Copia la plantilla comprometida y rellena lo que tengas — cada valor es opcional y las pruebas que necesitan un valor faltante saltan limpiamente.

```bash
mkdir -p secrets
cp dev-credentials.example.json secrets/dev-credentials.local.json
# editar secrets/dev-credentials.local.json
```

## Qué cada tier de prueba lee

| Tier | Necesita | De |
|------|----------|--------|
| **Unit** (`tests/UnitTests`) | nada | — determinista, sin secretos, sin red |
| **Integration** (`tests/IntegrationTests`) | Postgres | Testcontainers (Docker) — auto |
| **Live copy** (`tests/IntegrationTests/CopyLive`) | app OpenAPI + caché de token | `OpenApi.App`, `OpenApi.Tokens` |
| **E2E onboarding** (`tests/E2ETests/CopyLive`) | app OpenAPI + logins de cID | `OpenApi.App`, `OpenApi.Cids` |
| **E2E run/backtest real** (`CBotRealRunBacktestTests`) | un login de cID + un número de cuenta **demo** | `OpenApi.Cids[].{Username,Password,Accounts}` |
| **Características de IA** | clave Anthropic | `Ai.ApiKey` (sin establecer ⇒ características de IA devuelven deshabilitadas, app aún se ejecuta) |
| **Live economic-calendar sources** (`tests/IntegrationTests/Calendar/CalendarSourceLiveTests`) | FRED / BLS API keys | `Calendar.FredApiKey`, `Calendar.BlsApiKey` (unset ⇒ that source's live test skips; the keyless central-bank schedule still works) |

## Esquema

Ver `dev-credentials.example.json` en la raíz del repositorio. Secciones:

- `OpenApi.App` — `{ ClientId, ClientSecret }` de la aplicación cTrader Open API.
- `OpenApi.Cids` — logins de cTrader ID usados por onboarding de OAuth sin interfaz. Cada entrada también lleva una matriz **`Accounts`** — los números de cuenta de operaciones de cTrader (el login/número de cuenta, ej. `3635817`) bajo ese cID que la infraestructura de prueba puede vincular a la app y conducir. `CBotRealRunBacktestTests` lee la primera entrada que tiene una matriz `Accounts` no vacía, agrega ese cID + cuenta a la app, luego realmente ejecuta y backtests un cBot en ella. **Pon solo números de cuenta demo aquí** — nunca una cuenta en vivo; las pruebas de run/backtest colocan órdenes reales en cualquier cuenta que enumeres. `Accounts` vacío/omitido ⇒ la prueba real de run/backtest salta limpiamente.
- `OpenApi.Tokens` — la caché de token multi-cID (una entrada por cID autorizado con su token de actualización/acceso + lista de cuentas). Escrito automáticamente por onboarding y por el paso de actualización de token; raramente lo editas a mano.
- `Owner` — login del propietario de semilla para la app bajo E2E.
- `Database.ConnectionString` — solo cuando apuntas las pruebas a un Postgres externo en lugar de Testcontainers.
- `Ai.ApiKey` — clave de API de Anthropic para las características de IA.
- `Calendar.FredApiKey` — [FRED](https://fredaccount.stlouisfed.org/apikeys) (St. Louis Fed) API key. The primary economic-calendar value source (interest rates, inflation, employment).
- `Calendar.BlsApiKey` — [BLS](https://data.bls.gov/registrationEngine/) (US Bureau of Labor Statistics) v2 registration key (CPI, PPI, employment, JOLTS). Absent ⇒ the low-quota public tier.

  Both feed the exact `FredSource`/`BlsSource` the ingestion worker uses. With a key present, `CalendarSourceLiveTests` hits the real provider and asserts observations come back; absent, that source's test skips cleanly. The app also reads these at runtime via `App:Calendar:FredApiKey` / `App:Calendar:BlsApiKey` (environment variables override — e.g. `FRED_API_KEY`, `BLS_API_KEY`).

## Precedencia

1. **Las variables de entorno** anulan todo (ej. `App__OwnerPassword`, `App:Ai:ApiKey`).
2. **`secrets/dev-credentials.local.json`** — el archivo unificado (preferido).
3. **Archivos divididos heredados** — `openapi-test-app.local.json`, `openapi-cids.local.json`, `openapi-tokens.local.json` aún se leen cuando el archivo unificado está ausente, por lo que las máquinas existentes siguen funcionando. Las nuevas configuraciones deben usar el archivo único.

## Seguridad

- `secrets/` y `*.local.json` son gitignored — nada aquí se compromete nunca.
- Las pruebas de copia en vivo se niegan a ejecutarse contra cuentas que no son demo (`IsLive` cuentas son filtradas por `LiveCopyFixture`). Mantén solo cuentas demo en la caché de token.
- Las ejecuciones en clúster (Kubernetes) montan el archivo como un Secret de solo lectura; las actualizaciones de token se mantienen en memoria y la escritura de solo lectura es un no-op silencioso.
