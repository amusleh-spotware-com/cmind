---
description: "Full reproducible copy-trading test suite. Dua layer:"
---

# Suite test copy-trading (deterministic + live)

Full reproducible copy-trading test suite. Dua layer:

1. **Deterministic test** (xUnit, tidak ada network) — copy math + engine logic. Cepat, CI, tidak ada secret. Cover setiap money-management mode, setiap filter/option, engine resilience.
2. **Live E2E test** (real cTrader demo account) — production `CopyEngineHost` placing + copying real order antara real account. Fully automated, rerunnable seperti unit test: baca cached cred dari local gitignored file, self-refresh access token, skip clean saat secret absent (CI tetap green).

Tidak pernah jalankan terhadap live-funded account — setiap account **demo**, setiap live test close posisi yang dibuka.

## Layout

```
tests/UnitTests/CopyTrading/
  CopySizingCalculatorTests.cs   — setiap sizing mode + rounding + min/max lot
  CopyDecisionEngineTests.cs     — direction/reverse/slippage/delay/symbol filter/size-zero
  CopyEngineHostTests.cs         — copy logic host terhadap in-memory fake session
  FakeTradingSession.cs          — deterministic IOpenApiTradingSession (record order/close/amend)
  OpenApiConnectionTests.cs      — connect / reconnect / backoff / fatal fault (resilience)

tests/IntegrationTests/CopyLive/
  LiveCopySecrets.cs             — loads gitignored secret, save refreshed token
  LiveTokenBootstrapTests.cs     — one-shot: decrypt token dari app DB ke token cache
  LiveCopyFixture.cs             — rotate access token, expose demo account list
  LiveCopyScenario.cs            — jalankan satu real copy scenario end to end (open → copy → verify → clean up)
  CopyTradingLiveTests.cs        — live scenario (1:1, 1:many, reverse, …)
```

## Secret (local, gitignored — tidak pernah committed)

Semua cred di bawah `<repo>/secrets/` (sudah di `.gitignore`). Dev menulis **dua file pertama saja**; ketiga (token) auto-produced oleh onboarding.

`secrets/openapi-test-app.local.json` — Open API app:

```json
{ "ClientId": "2175_…", "ClientSecret": "…" }
```

`secrets/openapi-cids.local.json` — cID login cred untuk authorize (satu atau banyak):

```json
{ "Cids": [
  { "Cid": "amusleh",  "Username": "amusleh",  "Password": "…" },
  { "Cid": "afhacker", "Username": "afhacker", "Password": "…" }
] }
```
