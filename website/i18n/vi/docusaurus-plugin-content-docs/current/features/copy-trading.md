---
description: "Mirror master cTrader account lên một+ slave accounts — cross-broker, cross-cID — với per-destination control + money-grade reconciliation."
---

# Copy trading

Mirror **master** cTrader account lên một+ **slave** accounts — cross-broker, cross-cID — với per-destination control + money-grade reconciliation.

## Concepts

- **Copy profile** — một master + một+ **destinations**. Lifecycle: Draft → Running → Paused → Stopped. Aggregate root: CopyProfile.
- **Destination** — một slave account + full rule set. Tất cả config per-destination.
- **Copy engine host** — running worker cho profile. Subscribes master execution stream.
- **Supervisor** — background service trên mỗi node. Hosts assigned profiles.

## Cái gì được mirrored

| Master event | Slave action |
|--------------|--------------|
| Market / market-range position open | Open copy được sized |
| Limit / stop pending order | Place matching pending order |
| Pending order amend | Amend mirrored pending order |
| Pending order cancel / expiry | Cancel mirrored pending order |
| Partial close | Close cùng proportion |
| Stop-loss / trailing-stop change | Amend slave position's protection |
| Full close | Close slave copy |

Mỗi copy labelled với source position id. Sau reconnect host rebuilds state từ reconcile mà không duplicating trades.

## Tạo một profile

**New Profile** dialog collects: profile name, source account, destination accounts, + per-destination options. Tất cả inputs validated trước khi saving. Row actions respect lifecycle.

## Per-destination options

- **Sizing** — fixed lot, proportional balance/equity, fixed risk %, risk-from-stop.
- **Direction filter** — both / long-only / short-only, reverse.
- **Manage-only** — mirror closes nhưng không open new.
- **Symbol map + filter** — whitelist / blacklist.
- **Account protection** — watch equity, trigger actions.
- **Prop-firm rule guard** — daily-loss cap, trailing-drawdown limit.
- **Execution jitter** — de-correlate order timestamps.
- **Config lock** — freeze settings.
- **Rejection circuit breaker** — stop on repeated rejections.
- **Order-type filter** — market, limit, stop.
- **Copy SL / Copy TP** — mirror or manage independently.
- **Copy pending expiry** — mirror expiry timestamp.
- **Copy master slippage** — mirror slippage-in-points.
- **Guards** — max drawdown, daily loss, max copy delay.

## Reliability & edge cases

Engine handles failures gracefully:

- **Slave-pending fill-correlation timeout** — cancelled sau timeout.
- **Robust close/flatten** — tolerates already-closed positions.
- **Start with master in trades** — reconciles + opens copies.
- **Connection drops** — reconciles on reconnect.
- **Token invalidation** — swaps in place, continues copying.

## Auditability

Structured logs với profile id, destination cID, order ids. OpenTelemetry metrics trên cMind.Copy meter.

## API

- GET /api/copy/profiles
- POST /api/copy/profiles
- GET /api/copy/profiles/{id}
- POST /api/copy/profiles/{id}/destinations
- DELETE /api/copy/profiles/{id}/destinations/{destinationId}
- POST /api/copy/profiles/{id}/{start|pause|stop}

## Tests

- **Unit** — sizing, filters, token swap, reconcile.
- **Integration** — lease claim, token propagation.
- **E2E** — round-trip through API + UI.
- **Stress** — fault injection, convergence.
- **Live** — real cTrader demo accounts.

Xem dev-credentials.md cho single credentials file.
