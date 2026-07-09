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

All credentials live under `<repo>/secrets/` (already in `.gitignore`). Two files:

`secrets/openapi-test-app.local.json` — the Open API application:

```json
{ "ClientId": "2175_…", "ClientSecret": "…" }
```

`secrets/openapi-tokens.local.json` — the cached OAuth tokens + demo accounts (written by the
bootstrap, refreshed on every live run):

```json
{
  "RefreshToken": "…",
  "AccessToken": "…",
  "IsLive": false,
  "Accounts": [ { "CtidTraderAccountId": 25172589, "TraderLogin": 3635817, "IsLive": false }, … ]
}
```

The **refresh token does not expire**, so after the one-time bootstrap the live tests keep working
indefinitely: each run exchanges the refresh token for a fresh access token (rotation) with no browser
and no prompts.

## One-time bootstrap (getting the tokens)

The tokens are obtained by authorising the demo accounts through the app's OAuth flow (Accounts →
"Add via Open API" → log in with the cID → the accounts are linked). Once that has been done in the
running app, decrypt the stored tokens straight out of the app's Postgres into the token cache:

```bash
# 1. Start a Postgres on the app's data volume (same major version as Aspire's postgres)
docker run -d --name cmind-pg-extract -e POSTGRES_PASSWORD=appdev \
  -v app-pg-data:/var/lib/postgresql/data -p 5544:5432 postgres:17-alpine

# 2. Decrypt + cache (uses the app's own DataProtection setup)
CMIND_VOLUME_CONN="Host=127.0.0.1;Port=5544;Database=appdb;Username=postgres;Password=appdev" \
  dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveTokenBootstrapTests

# 3. Clean up
docker rm -f cmind-pg-extract
```

That writes `secrets/openapi-tokens.local.json`. From then on the live tests need nothing else.

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
**reverse** turns a master buy into a slave sell. Each opens a real min-lot position on the master,
waits for the engine to mirror it (matched by the source-position-id label on the slave), asserts, and
closes everything. A closed market is reported **Inconclusive** rather than failing.

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

- **Cross-cID copying (multiple cIDs).** The engine already supports a different access token per
  account, so a master under one cID can copy to a slave under another. To exercise it live, onboard the
  second cID's token: authorise its accounts in the app (Add via Open API), then re-run the bootstrap so
  the token cache holds both cIDs. A Playwright-driven OAuth onboarding (log in with a cID + password,
  capture the callback code) would make adding a cID fully hands-off — not yet built.
- **Token rotation across external nodes.** Each run rotates the access token via the refresh token
  (exercised by `LiveCopyFixture`). `OpenApiTokenRefreshService` refreshes near expiry in-process; a test
  that a refreshed token propagates to the distributed `CopyAgent` on external nodes without disrupting a
  running profile is still to be written.
- **Partial close / order types / SL-trailing.** The host copies on position open as a market order and
  closes the full copy when the source closes; partial-close mirroring, non-market order types, and
  trailing-stop replication are not implemented and therefore not tested.
