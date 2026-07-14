---
slug: /for-traders
title: cMind for cTrader traders
description: Why a cTrader trader should self-host cMind — own your stack and data, author, backtest, run and monitor cBots in one AI-powered console, on your laptop, VPS or phone.
keywords:
  - cTrader
  - algorithmic trading
  - self-hosted trading platform
  - cBot backtesting
  - AI trading bots
  - open source trading software
sidebar_position: 5
---

# cMind for cTrader traders 📈

You already trade on cTrader. You already juggle a code editor, a backtester, a VPS, and three
browser tabs. **cMind collapses all of that into one dark, keyboard-friendly console you run
yourself** — and it's open source, so nothing about your edge, your strategies, or your credentials
ever leaves your box.

:::tip[TL;DR]
Self-host cMind on a laptop, a cheap VPS, or a home server. Author, backtest, run, and monitor cBots
in one place, with an AI core doing the chores. → [Run it in 5 minutes](./deployment/local.md)
:::

## Why self-host instead of a hosted service?

- **Own your stack and your data.** Your cBots, credentials, tokens, and equity history live on
  **your** infrastructure — no third party, no lock-in, no "we're sunsetting this product" email.
- **It's genuinely yours to change.** C# 14 / .NET 10, strict DDD, EF Core + PostgreSQL, an MCP
  server — all open source and hackable. Fork it, extend it, send a PR.
- **No per-feature paywall.** Bring your own AI key for any provider; every AI feature is on.

Prefer not to run servers yourself? A hosting company can run a managed cMind for you —
see [For cloud & VPS providers](./for-cloud-providers.md).

## One console, no tab-juggling

- **Author** in a real Monaco IDE (the VS Code editor), with C# **and** Python templates and
  sandboxed `dotnet build` in throwaway containers. → [Build & backtest](./features/build-and-backtest.md)
- **Backtest** across a fleet of nodes and watch equity curves stream back live.
- **Run** strategies live and **monitor** them from one dashboard. → [Dashboard](./features/dashboard.md)
- **Copy** a master account onto many accounts across brokers and cTrader IDs, with reconciliation
  that survives dropped connections and rotating tokens. → [Copy trading](./features/copy-trading.md)

## AI that does chores, not small talk

Bring your own API key (any supported provider — cloud or a local model) and get plain-English → a
real compiling cBot with a self-repair loop, parameter tuning, backtest post-mortems, and a risk
guard that can auto-stop a misbehaving bot. → [Meet the AI core](./features/ai.md)

## Institutional-grade tooling, for one

The same rigor a desk pays for, on your own box:

- [Backtest integrity](./features/backtest-integrity.md) · [Position sizing](./features/position-sizing.md)
- [Strategy health](./features/strategy-health.md) · [Regime lab](./features/regime-lab.md)
- [Execution TCA](./features/execution-tca.md) · [Trading journal](./features/trading-journal.md)
- [Agent Studio](./features/agent-studio.md) · [Contrarian positioning](./features/contrarian-positioning.md)

## Runs where you do

Start on your laptop with `docker compose up`, graduate to a cheap VPS or a home server when you're
ready, and check on your bots from your phone — cMind is an installable, mobile-first
[PWA](./features/pwa.md). → [Run it locally](./deployment/local.md)

Want your AI client to drive it? There's a built-in [MCP server](./features/mcp.md).

## Help make it better

cMind is open source and MIT-licensed — the roadmap is community-shaped:

- File issues and feature requests, and vote on what matters.
- Add cBot templates, AI provider adapters, or UI translations.
- Send PRs — three test tiers (unit + integration + E2E) and strict DDD keep the bar high, and the
  [Contributing guide](./contributing.md) walks you through it.

Ready? → [Read the intro](./intro.md) then [run it locally](./deployment/local.md).
