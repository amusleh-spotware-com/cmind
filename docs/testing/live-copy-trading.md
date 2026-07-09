# Copy-trading test suite (deterministic + live)

This is the full, reproducible test suite for copy trading. It has two layers:

1. **Deterministic tests** (xUnit, no network) — the copy math and copy engine logic. Fast, run in CI,
   no secrets. Cover every money-management mode, every filter/option, and the engine's resilience
   behaviour.
2. **Live E2E tests** (real cTrader demo accounts) — the production `CopyEngineHost` placing and
   copying real orders between real accounts. Fully automated and rerunnable "like a unit test":
   they read cached credentials from local gitignored files, refresh the access token themselves, and
   skip cleanly when the secrets are absent (so CI stays green).

Nothing here ever runs against a live-funded account — every provided account is a **demo** account,
and every live test closes the positions it opened.

## Layout

```
tests/UnitTests/CopyTrading/
  CopySizingCalculatorTests.cs   — every sizing mode + rounding + min/max lot
  CopyDecisionEngineTests.cs     — direction/reverse/slippage/delay/symbol filter/size-zero
  CopyEngineHostTests.cs         — host copy logic against an in-memory fake session
  FakeTradingSession.cs          — deterministic IOpenApiTradingSession (records orders/closes/amends)
  OpenApiConnectionTests.cs      — connect / reconnect / backoff / fatal fault (resilience)

tests/IntegrationTests/CopyLive/
  LiveCopySecrets.cs             — loads the gitignored secrets, saves refreshed tokens
  LiveTokenBootstrapTests.cs     — one-shot: decrypt tokens from the app DB into the token cache
  LiveCopyFixture.cs             — rotates the access token, exposes the demo account list
  LiveCopyScenario.cs            — runs one real copy scenario end to end (open → copy → verify → clean up)
  CopyTradingLiveTests.cs        — the live scenarios (1:1, 1:many, reverse, …)
```

## Secrets (local, gitignored — never committed)

All credentials live under `<repo>/secrets/` (already in `.gitignore`). The **dev only writes the first
two files**; the third (tokens) is produced automatically by the onboarding.

`secrets/openapi-test-app.local.json` — the Open API application:

```json
{ "ClientId": "2175_…", "ClientSecret": "…" }
```

`secrets/openapi-cids.local.json` — the cID login credentials to authorize (one or many):

```json
{ "Cids": [
  { "Cid": "amusleh",  "Username": "amusleh",  "Password": "…" },
  { "Cid": "afhacker", "Username": "afhacker", "Password": "…" }
] }
```

`secrets/openapi-tokens.local.json` — **written by the onboarding**, multi-cID, refreshed on every run:

```json
{ "Cids": [
  { "Cid": "amusleh", "RefreshToken": "…", "AccessToken": "…", "IsLive": false,
    "Accounts": [ { "CtidTraderAccountId": 25172589, "TraderLogin": 3635817, "IsLive": false }, … ] }
] }
```

The **refresh token does not expire**, so after the one-time onboarding the live tests keep working
indefinitely: each run exchanges each cID's refresh token for a fresh access token (rotation) — no
browser, no prompts.

## One-time onboarding (fully automated — no dev interaction beyond saving creds)

The onboarding drives the real cTrader ID login in a headless browser from the saved cID credentials,
captures the OAuth callback on a local HTTPS listener at the app's registered redirect
(`https://localhost:7080/openapi/callback`), exchanges the code for tokens, loads the account list, and
writes the multi-cID token cache. Run it once per machine (or whenever you add a cID):

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

That authorizes every cID in `openapi-cids.local.json` and writes `openapi-tokens.local.json`. From then
on the live copy tests need nothing else. (The cID's cTrader ID account must not have 2FA/captcha on
login for the automation to complete.)

**Alternative bootstrap** (if the accounts are already authorized in a running app): decrypt the stored
tokens straight out of the app's Postgres volume instead of re-authorizing:

```bash
docker run -d --name cmind-pg-extract -e POSTGRES_PASSWORD=appdev \
  -v app-pg-data:/var/lib/postgresql/data -p 5544:5432 postgres:17-alpine
CMIND_VOLUME_CONN="Host=127.0.0.1;Port=5544;Database=appdb;Username=postgres;Password=appdev" \
  dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveTokenBootstrapTests
docker rm -f cmind-pg-extract
```

## Safety — demo only

The live tests trade **only demo accounts**: the fixture filters the token cache to accounts with
`IsLive == false` and connects to the demo gateway, so an order can never be placed on a live/funded
account even if a live account is authorized. Every position a test opens is closed in cleanup.

## Running

```bash
# Deterministic copy tests only (fast, no secrets, CI-safe)
dotnet test tests/UnitTests --filter FullyQualifiedName~CopyTrading

# Live copy tests against the real demo accounts (needs the two secrets files)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests

# Everything
dotnet test
```

Without the secrets files the live tests print a skip reason and pass as no-ops, so the suite is safe
to run anywhere.

## Coverage

### Money management / sizing (deterministic — `CopySizingCalculatorTests`)
FixedLot · LotMultiplier · NotionalMultiplier (contract-size / currency) · ProportionalBalance ·
ProportionalEquity · ProportionalFreeMargin · AutoProportional · FixedRiskPercent · FixedLeverage ·
scale **up** and **down** for balance/leverage/capacity mismatch (the "golden rule") · lot-step
rounding · min-lot skip vs force-to-min · max-lot cap · tighter-of bound-vs-spec min & max · zero
master balance skip.

### Decision filters (deterministic — `CopyDecisionEngineTests`)
Symbol whitelist / blacklist / allow · LongOnly / ShortOnly · reverse flips the effective side ·
slippage over limit skip + exactly-at-limit allowed · stale-signal (max delay) skip · size-zero skip ·
reconnect reconciliation (open-missing dedup, close-orphaned).

### Copy engine host (deterministic — `CopyEngineHostTests`, in-memory session)
Open mirrors a market order (side / volume / label) · **reverse** flips side and **swaps SL/TP** ·
**symbol mapping** resolves the destination symbol · **order-failure on one slave still copies to the
others** · source close closes the mirrored copy · reconnect resync closes orphaned copies.

### Connection resilience (deterministic — `OpenApiConnectionTests`)
Reaches Connected after app auth · dropped connection reconnects and re-auths · fatal auth error faults ·
exponential backoff.

### Live, real cTrader demo accounts (`CopyTradingLiveTests`)
Token refresh + account listing · **1:1** copy executes · **1:many** copy mirrors to every slave ·
**reverse** turns a master buy into a slave sell · **cross-cID** copy (master under one cID mirrors to a
slave under another cID, each authenticating with its own token). Each opens a real min-lot position on
the master, waits for the engine to mirror it (matched by the source-position-id label on the slave),
asserts, and closes everything. A closed market is reported **Inconclusive** rather than failing.

## Logging & auditability

Every copy trading operation is logged through source-generated structured events
(`Core/Logging/LogMessages.cs`, event IDs 1043–1055) so the full trail is auditable:

| Event | Id | Meaning |
|-------|----|---------|
| CopyHostStarted | 1046 | a profile's engine came up (source + destination count) |
| CopySourceOpen | 1047 | master opened a position (symbol / side / lots) |
| CopyOrderPlaced | 1048 | copy order sent to a slave (symbol / side / volume / source id) |
| CopySkipped | 1049 | a copy was skipped and why (slippage / direction / symbol_filter / size_zero / …) |
| CopyProtectionApplied | 1050 | SL/TP applied to a slave copy |
| CopyOpenFailed | 1051 | a slave copy-open failed (isolated — other slaves continue) |
| CopySourceClose / CopyPositionClosed | 1052 / 1053 | master closed → slave copy closed |
| CopyCloseFailed | 1054 | a slave copy-close failed |
| CopyResync | 1055 | reconnect reconciliation (source open count, orphans closed) |

Logs are emitted as Serilog compact JSON (structured properties: `ProfileId`, `DestinationCtid`,
`SourcePositionId`, `Symbol`, `Side`, `Volume`, …) and shipped to OTLP when
`OTEL_EXPORTER_OTLP_ENDPOINT` is set. **Fully configurable** per category via standard config — e.g. to
raise or lower copy-engine verbosity without touching code:

```jsonc
// appsettings.json — Serilog level overrides
"Serilog": { "MinimumLevel": { "Override": {
  "CopyEngine": "Information",              // the CopyEngineHost audit trail
  "Nodes.CopyTrading": "Information"        // supervisor / token refresh
} } }
```

The `Audit_log_records_every_trading_operation` host test asserts the trail fires for open, order,
protection, and close.

## Edge cases (validated against how real copy/MAM platforms fail)

Slippage & latency, symbol suffix/mismatch, duplicate trades on reconnect, leverage mismatch &
margin-safe sizing, deposit-currency/contract-size differences, min/max lot & rounding, rejected orders,
direction filters, and orphan cleanup after a disconnect are all covered above. Sources:
[leverage mismatch](https://copygram.app/blog/education/the-truth-about-leverage-mismatches-copying-high-leverage-low-leverage-accounts) ·
[cross-broker copying](https://www.mt4copier.com/cross-broker-trade-copying-efficient-forex-replication/) ·
[copier pitfalls](https://www.mt4copier.com/copy-trading-pitfalls-every-account-manager-must-avoid/) ·
[slippage & latency](https://copygram.app/blog/education/understanding-slippage-latency-copy-trading) ·
[why copy trading fails](https://xtsupport.zendesk.com/hc/en-us/articles/51566808595993-Why-Copy-Trading-Fails-Causes-Prevention-Guide) ·
[risk parameters](https://www.mt4copier.com/risk-parameters/).

## Known gaps / next

- **Token rotation across external nodes.** Each run rotates the access token via the refresh token
  (exercised by `LiveCopyFixture`). `OpenApiTokenRefreshService` refreshes near expiry in-process; a test
  that a refreshed token propagates to the distributed `CopyAgent` on external nodes without disrupting a
  running profile is still to be written.
- **Partial close / order types / SL-trailing.** The host copies on position open as a market order and
  closes the full copy when the source closes; partial-close mirroring, non-market order types, and
  trailing-stop replication are not implemented and therefore not tested.
