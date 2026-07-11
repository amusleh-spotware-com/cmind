---
slug: /intro
title: Welcome to cMind
description: A friendly introduction to cMind — the open-source, self-hostable trading operations platform for cTrader.
sidebar_position: 1
---

# Welcome to cMind 👋

So you want to build trading bots, backtest them without melting your laptop, run them across a
few machines, mirror trades onto a dozen accounts, and have an AI keep an eye on the risk while
you sleep. **You&apos;re in exactly the right place.**

cMind is an **open-source, self-hostable trading operations platform for cTrader**. Think of it as
your whole trading desk — authoring, execution, a compute fleet, copy trading, and an AI core —
packed into one calm, dark, mobile-friendly app that you own end to end.

:::tip In one sentence
Build → backtest → run → copy your cTrader strategies at scale, with AI built in, on your own
servers, under your own brand.
:::

## What can it actually do?

| You want to… | cMind does it | Read more |
|---|---|---|
| Write a cBot in the browser | Monaco IDE + C#/Python templates, sandboxed builds | [Build & backtest](./features/build-and-backtest.md) |
| Backtest across machines | A self-healing node fleet picks the least-busy box | [Scaling](./deployment/scaling.md) |
| Copy one account onto many | Robust mirroring with resync, not double-trades | [Copy trading](./features/copy-trading.md) |
| Let AI do the grunt work | Strategy gen, self-repair, risk guard, post-mortems | [AI core](./features/ai.md) |
| Stay inside prop-firm rules | Live equity tracking + challenge rule simulation | [Prop-firm](./features/prop-firm.md) |
| Ship it as *your* product | Full white-label: name, colors, logo, favicon | [White-label](./features/white-label.md) |
| Run it on your phone | Installable, mobile-first PWA | [PWA](./features/pwa.md) |
| Drive it from an AI client | Built-in MCP server (HTTP + SSE) | [MCP](./features/mcp.md) |

## The 5-minute path ⏱️

If you have Docker and five minutes, you can be poking at a real cMind instance right now:

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
cp .env.example .env        # set OWNER_EMAIL + OWNER_PASSWORD
docker compose up --build
```

Then open **<http://localhost:8080>**, sign in, and you&apos;re off. The full walkthrough (with
troubleshooting for when Docker inevitably has opinions) lives in
**[Run it locally](./deployment/local.md)**.

## New here? Follow the yellow-brick road 🟡

1. **[Who is this for?](./audience.md)** — make sure you&apos;re our kind of trouble.
2. **[Run it locally](./deployment/local.md)** — get a real instance up.
3. **[Features](./features/README.md)** — the full tour of what&apos;s inside.
4. **[Deploy for real](./deployment/cloud.md)** — Docker, Kubernetes, Azure, AWS.
5. **[Make it yours](./white-label-for-business.md)** — white-label it for your business.
6. **[Contribute](./contributing.md)** — PRs (human *and* AI-assisted) very welcome.

## A quick word on money 💸

cMind moves **real capital**. We take that seriously — every change ships with unit, integration,
and end-to-end tests, failure paths included (dropped connections, rejected orders, dead nodes).
You should take it seriously too: **test on a demo account first**, and read the
[compliance notes](./features/compliance.md) before you point it at anything live. Trading is
risky; this software is a tool, not financial advice.

Right — enough preamble. Let&apos;s go build something. →
