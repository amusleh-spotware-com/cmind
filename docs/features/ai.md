# AI features

cMind's AI layer is Claude-powered and gated entirely on `App:Ai:ApiKey`. With no key set,
every AI feature returns a disabled result and the rest of the app runs unchanged (no key is
needed to build, test, or run the platform). The API is called over raw HTTP via a typed
`HttpClient` (`AnthropicAiClient`); `AiFeatureService` is the single orchestrator shared by the
Web endpoints, the MCP `AiTools`, and the background risk guard.

## Capabilities

- **Strategy Builder** — plain-English prompt → runnable cBot via a **generate → build →
  AI-fix** self-repair loop (`generate-project`).
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
- The **Assistant** Blazor page.

## Configuration

`App:Ai` — `ApiKey`, `Model` (default `claude-opus-4-8`), `BaseUrl`, `MaxTokens`,
`RiskGuardEnabled`, `RiskGuardAutoStop`, `RiskGuardInterval`. For tests/dev, the key lives in the
unified [dev-credentials file](../testing/dev-credentials.md) under `Ai.ApiKey`.
