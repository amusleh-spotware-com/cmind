# AI features

cMind AI layer Claude-powered, gated on Anthropic API key present. Key set two ways: deploy-time via `App:Ai:ApiKey`, or runtime by owner in **Settings → AI** (`/settings/ai`). UI-entered key stored **encrypted** as `AppSetting` (`IAiKeyStore` + `ISecretProtector`, `EncryptionPurposes.AiApiKey`) and **overrides** config value. No key either source → every AI feature returns disabled result, rest of app unchanged (no key needed to build, test, run platform). API called over raw HTTP via typed `HttpClient` (`AnthropicAiClient`), reads current key from `IAiKeyStore` per request. `AiFeatureService` = single orchestrator shared by Web endpoints, MCP `AiTools`, background risk guard.

AI unconfigured → AI pages dim actions, show banner plus one-time dialog prompting owner add key in Settings → AI (`AiFeatureNotice`). Status at `GET /api/ai/status`; key managed (owner-only) via `GET/PUT/DELETE /api/ai/key`.

## Capabilities

- **Build Bot** — plain-English prompt → runnable cBot via **generate → build → AI-fix** self-repair loop (`build-strategy`). Tab on Assistant page (former standalone Strategy Builder page folded in, killed redundancy).
- **Parameter optimization** — closed loop: AI proposes param sets, each persisted + backtested across nodes, results feed next round (`optimize-run` / `optimize-params`).
- **Autonomous portfolio agent** — mandate-driven proposals with full decision journal (`AgentMandate` → `AgentProposal`).
- **Acting risk guard** — `AiRiskGuard` background service assesses running bots, can **auto-stop** on critical risk (opt-in).
- **Prop-firm exposure guardian** — drawdown/exposure limits with auto-flatten.
- **Market alerts** — `AlertRule` engine with AI sentiment (web-search grounded).
- **Analysis** — cBot review, backtest analysis, post-mortems, market sentiment, chart-vision design, marketplace curation.

## Surfaces

- Web endpoints under `/api/ai/*` (generate, generate-project, review, analyze-backtest, optimize-params, optimize-run, post-mortem, sentiment, vision, curate).
- MCP tools (`AiTools`) for AI clients — see [mcp.md](mcp.md).
- **AI** (Assistant) Blazor page, grouped in nav under **AI** alongside Portfolio Agent, Alerts, MCP Keys. Tabs: Build Bot, Generate cBot, Review, Debate, Market Sentiment, Exposure Check, Portfolio Digest, Tune Advisor, Optimize.
- **Settings → AI** (`/settings/ai`, owner-only) to add/replace/remove API key.

## Configuration

`App:Ai` — `ApiKey`, `Model` (default `claude-opus-4-8`), `BaseUrl`, `MaxTokens`, `RiskGuardEnabled`, `RiskGuardAutoStop`, `RiskGuardInterval`. Runtime key set in Settings → AI stored encrypted, precedes `ApiKey`. For tests/dev, config key lives in unified [dev-credentials file](../testing/dev-credentials.md) under `Ai.ApiKey`.