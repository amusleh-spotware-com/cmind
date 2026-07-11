---
slug: /audience
title: Who is cMind for?
description: cMind is built for algorithmic traders, quant-leaning developers, and prop firms / trading desks who self-host and want full control.
sidebar_position: 2
---

# Who is cMind for?

Short version: **people with money on the line who like being in control.** cMind is a serious
algorithmic-trading operations console — not a consumer fintech toy with a confetti animation when
you make $3. If you live in dark dashboards, charts, terminals, and IDEs, keep reading. You&apos;ll
feel at home.

## 📈 Algorithmic traders

You already trade on cTrader and you&apos;re tired of juggling a code editor, a backtester, a VPS,
and three browser tabs. cMind puts authoring, backtesting, live execution, and monitoring in one
place — with equity curves and logs streaming back in real time.

- Write a cBot, backtest it across a fleet, run it live — same app, no context-switching.
- Let the AI draft a strategy or explain why last night&apos;s backtest fell apart.
- Watch everything from your phone (it&apos;s an installable app).

**Start here:** [Build & backtest](./features/build-and-backtest.md) → [Copy trading](./features/copy-trading.md).

## 🧑‍💻 Quant-leaning developers

You want to *own* your stack. cMind is C# 14 / .NET 10, strict DDD, EF Core + PostgreSQL, .NET
Aspire, and an MCP server — all open source and hackable.

- Real Monaco IDE, C# **and** Python templates, sandboxed `dotnet build`.
- A built-in [MCP server](./features/mcp.md) so your AI client can drive it.
- Clean domain model, three test tiers, and docs (hi 👋) that don&apos;t lie to you.

**Start here:** [Contributing](./contributing.md) → [MCP server](./features/mcp.md).

## 🏢 Prop firms & trading desks

You need multi-tenant, branded, auditable infrastructure that onboards traders under *your* name.

- **[White-label](./features/white-label.md)** every surface — name, colors, logo, favicon.
- **[Prop-firm rule simulation](./features/prop-firm.md)** — daily loss, drawdown, targets, live equity.
- **[Compliance](./features/compliance.md)** logs and [feature toggles](./features/feature-toggles.md) to shape what each tenant sees.
- **[Performance fees](./features/copy-performance-fees.md)** and a **[provider marketplace](./features/copy-provider-marketplace.md)** for copy-trading businesses.

**Start here:** [White-label for business](./white-label-for-business.md).

## Is cMind *not* for you?

Be honest with yourself. cMind might be overkill if:

- You&apos;ve never touched a terminal and don&apos;t want to start (self-hosting means *you* run the server).
- You only trade manually and don&apos;t care about bots, backtests, or copy trading.
- You want a hosted SaaS with a support hotline — cMind is self-hosted; *you* are the ops team
  (though it tries very hard to make that painless).

If you&apos;re still nodding along, welcome aboard. → [Run it locally](./deployment/local.md)
