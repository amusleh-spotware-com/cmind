# Commitment of Traders (COT)

cMind ships a built-in **Commitment of Traders** report — the weekly CFTC breakdown of who is long and
short in the U.S. futures market (commercial hedgers, large speculators, funds), with interactive
historical charts, a normalized **COT index**, an authenticated REST API for cBots and MCP tools for AI
clients. Data comes straight from the **CFTC public Socrata datasets** — no API key, no aggregator. Like
the economic calendar it is a decoupled module that can be disabled with zero effect on the trading core.

## What it gives you

- **All three report families, futures-only and futures+options combined:**
  - **Legacy** — Non-Commercial (large speculators), Commercial (hedgers), Non-Reportable.
  - **Disaggregated** — Producer/Merchant, Swap Dealers, Managed Money, Other Reportables.
  - **Traders in Financial Futures (TFF)** — Dealer, Asset Manager, Leveraged Funds, Other Reportables.
- **A curated market catalog** — FX majors, gold/silver/copper, crude & natural gas, Treasuries, equity
  indices, crypto and the main grains/softs — each mapped to its stable CFTC contract code and, where
  unambiguous, to a tradeable symbol (e.g. Euro FX → `EURUSD`, Gold → `XAUUSD`).
- **The COT index (0–100)** — where the current speculator net position sits inside its historical range
  (default ~3-year lookback). Readings near the extremes flag crowded positioning that often precedes a
  reversal; the report labels a **long extreme** (≥80) or **short extreme** (≤20).
- **Point-in-time correctness.** A weekly report is measured on a Tuesday but only becomes public the
  following Friday; every read honours that release instant, so a backtested positioning signal never
  sees a report before it was published (no look-ahead).

## Using the page

Open **Commitment of Traders** from the left navigation. Pick a **market**, a **report type** (Legacy /
Disaggregated / Financial) and toggle **Futures + options** to switch between futures-only and the
combined variant. The page shows:

- **Net positioning over time** — an interactive line chart of each trader category's net position
  (long − short) across the history window.
- **COT index** — a line chart of the 0–100 index, with the latest reading and its extreme label.
- **Latest snapshot** — a table of long / short / net / % of open interest per trader category, plus
  total open interest and the report date.

## How the data flows

A weekly ingestion worker pulls the six CFTC datasets for the tracked markets, upserts the market catalog
and appends each new report **idempotently** (re-running never duplicates a snapshot). The first run
back-fills several years of history; later runs re-sync the most recent weeks to catch late revisions.
Everything runs out of the box with no key; an optional Socrata app token only raises the rate limit.

## Configuration

All keys live under `App:Cot` (see [feature toggles](./feature-toggles.md) and
[white-label owner settings](./white-label-owner-settings.md)):

| Key | Default | Purpose |
|-----|---------|---------|
| `IngestionEnabled` | `true` | Whether the weekly ingestion worker runs. |
| `PollInterval` | `6h` | How often the worker polls the CFTC datasets. |
| `BackfillYears` | `5` | Years of history pulled on the first run. |
| `ReconcileLookbackWeeks` | `4` | Recent weeks re-synced each cycle to catch revisions. |
| `SocrataAppToken` | — | Optional token that raises the anonymous rate limit. |
| `CotIndexLookbackWeeks` | `156` | Weekly reports used as the COT-index range (~3 years). |

## Gating

Visibility is a two-tier gate, identical to the economic calendar: the white-label hard gate
`App:Branding:EnableCot` (build-level) **and** the runtime feature toggle `App:Features:Cot`. With either
off the nav link, page, REST API and MCP tools all disappear (the API returns `404`). Because the data
source is keyless, there is no data-source-key gate — enabled means visible.

## For developers

- Domain: `Core.Cot` — `CotMarket` and `CotReport` aggregates, the `CotPositions` value object, the
  `CotIndexCalculator` domain service, and the `ICotReports` / `ICotSource` ports.
- Infrastructure: `Infrastructure.Cot` — the `CftcSocrataSource` anti-corruption parser, the rate gate,
  the append-only write service, the read side and the weekly ingestion worker (EF `cot` schema).
- cBot & AI access: the [COT cBot API](./cot-cbot-api.md) (REST, `market:read` JWT) and the MCP tools
  `CotMarkets`, `CotLatest`, `CotHistory`, `CotHealth`.
