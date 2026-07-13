---
slug: /features
title: Features — the full tour
description: Everything cMind can do — copy trading, AI, build & backtest, prop-firm guards, white-label, PWA, MCP, and more.
sidebar_label: Overview
---

# Features — the full tour 🧭

Welcome to the grand tour. cMind packs a *lot* into one app, so here&apos;s the map. Each capability
has its own deep-dive doc — click through to whatever itch you&apos;re scratching.

## 🔁 Copy trading

The crown jewel. Mirror a master account onto many, and keep them in sync even when the internet
misbehaves.

- **[Copy trading](./copy-trading.md)** — the core: mirroring, order types, SL/TP, slippage, desync/resync.
- **[Execution transparency](./copy-execution-transparency.md)** — see exactly what was copied, when, and why.
- **[Performance fees](./copy-performance-fees.md)** — charge for your signal, high-water-mark style.
- **[Provider marketplace](./copy-provider-marketplace.md)** — let traders discover and follow providers.
- **[Notifications](./copy-notifications.md)** — get told when something needs you.
- **[AI copy recommender](./ai-copy-recommender.md)** — let the AI suggest who to copy.
- **[Open API token lifecycle](./token-lifecycle.md)** — how cMind keeps exactly one valid token per cID.

## 📊 Your home base

- **[Dashboard](./dashboard.md)** — the live, mobile-first command center: KPIs with sparklines, an activity chart, a status ring, a live feed, and (for admins) cluster health. It refreshes itself.

## 🧠 AI core

Not a chat box bolted on the side — AI that actually *does the work*.

- **[AI assistant, agent, risk guard & alerts](./ai.md)** — strategy generation, self-repairing builds, a background risk guard that can auto-stop bots, and smart alerts.

## 🛠️ Build & run

- **[Build & backtest cBots](./build-and-backtest.md)** — the in-browser Monaco IDE, C#/Python templates, sandboxed builds, and live equity curves.
- **[MCP server](./mcp.md)** — expose cMind&apos;s tools over HTTP + SSE so AI clients can drive it.

## 🏢 Run it as a business

- **[White-label / branding](./white-label.md)** — rebrand every surface via config.
- **[Prop-firm challenge simulation](./prop-firm.md)** — enforce daily-loss, drawdown, and target rules with live equity.
- **[Feature toggles](./feature-toggles.md)** — decide what each deployment/tenant sees.
- **[Compliance / legal](./compliance.md)** — the audit trail and legal surface.

## 📱 The experience

- **[Installable app (PWA)](./pwa.md)** — mobile-first, offline shell, add-to-home-screen.
- **[UI design system & mobile-first](../ui-guidelines.md)** — the design tokens and rules behind the look.

## ⚙️ Under the hood

The operational bits that keep it all running:

- **[Node fleet & discovery](../operations/node-discovery.md)** — how nodes self-register and heal.
- **[Horizontal scaling](../deployment/scaling.md)** — add replicas, no external coordinator needed.
- **[Logging & audit](../operations/logging.md)** — structured logs + OpenTelemetry.
- **[Deployment](../deployment/local.md)** — get it running anywhere.

:::note Keeping docs honest
Every feature doc is kept in lockstep with the code — change the behavior, update the doc, same
commit. If you ever spot drift, that&apos;s a bug: please
[open an issue](https://github.com/amusleh-spotware-com/cmind/issues/new/choose) or send a PR. 🙏
:::

<!-- [ZH-HANS] Translation needed -->
