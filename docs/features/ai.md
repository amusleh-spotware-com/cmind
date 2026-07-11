# AI features

cMind's AI layer is Claude-powered and gated on whether an Anthropic API key is available. The key
may be set two ways: at deploy time via `App:Ai:ApiKey`, or at runtime by an owner in **Settings →
AI** (`/settings/ai`). A key entered in the UI is stored **encrypted** as an `AppSetting`
(`IAiKeyStore` + `ISecretProtector`, `EncryptionPurposes.AiApiKey`) and **overrides** the config
value. With no key from either source, every AI feature returns a disabled result and the rest of
the app runs unchanged (no key is needed to build, test, or run the platform). The API is called
over raw HTTP via a typed `HttpClient` (`AnthropicAiClient`), which reads the current key from
`IAiKeyStore` per request; `AiFeatureService` is the single orchestrator shared by the Web
endpoints, the MCP `AiTools`, and the background risk guard.

When AI is unconfigured, the AI pages dim their actions and show a banner plus a one-time dialog
prompting the owner to add a key in Settings → AI (`AiFeatureNotice`). Status is exposed at
`GET /api/ai/status`; the key is managed (owner-only) via `GET/PUT/DELETE /api/ai/key`.

## Capabilities

- **Build Bot** — plain-English prompt → runnable cBot via a **generate → build → AI-fix**
  self-repair loop (`build-strategy`). Lives as a tab on the Assistant page (the former standalone
  Strategy Builder page was folded in to avoid redundancy).
- **Parameter optimization** — closed loop: AI proposes param sets, each is persisted and
  backtested across nodes, results feed the next round (`optimize-run` / `optimize-params`).
- **Autonomous portfolio agent** — mandate-driven proposals with a full decision journal
  (`AgentMandate` → `AgentProposal`).
- **Acting risk guard** — `AiRiskGuard` background service assesses running bots and can
  **auto-stop** on critical risk (opt-in).
- **Prop-firm exposure guardian** — drawdown/exposure limits with auto-flatten.
- **Market alerts** — `AlertRule` engine with AI sentiment (web-search grounded).
- **Analysis** — cBot review, backtest analysis, post-mortems, market sentiment, chart-vision
  design, marketplace curation.

## Surfaces

- Web endpoints under `/api/ai/*` (generate, generate-project, review, analyze-backtest,
  optimize-params, optimize-run, post-mortem, sentiment, vision, curate).
- MCP tools (`AiTools`) for AI clients — see [mcp.md](mcp.md).
- The **AI** (Assistant) Blazor page, grouped in the nav under **AI** alongside Portfolio Agent,
  Alerts, and MCP Keys. Its tabs: Build Bot, Generate cBot, Review, Debate, Market Sentiment,
  Exposure Check, Portfolio Digest, Tune Advisor, Optimize.
- **Settings → AI** (`/settings/ai`, owner-only) to add/replace/remove the API key.

## Configuration

`App:Ai` — `ApiKey`, `Model` (default `claude-opus-4-8`), `BaseUrl`, `MaxTokens`,
`RiskGuardEnabled`, `RiskGuardAutoStop`, `RiskGuardInterval`. A key set at runtime in Settings → AI
is stored encrypted and takes precedence over `ApiKey`. For tests/dev, the config key lives in the
unified [dev-credentials file](../testing/dev-credentials.md) under `Ai.ApiKey`.
