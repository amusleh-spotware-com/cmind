---
slug: /features
title: Features — plný tour
description: Všetko, čo cMind môže robiť — copy trading, AI, build & backtest, prop-firm guards, white-label, PWA, MCP a viac.
sidebar_label: Prehľad
---

# Features — plný tour 🧭

Vitajte na veľkom ture. cMind balí *veľa* do jednej aplikácie, takže tu je mapa. Každá schopnosť
má svoju vlastnú deep-dive dokumentáciu — kliknite cez na akúkoľvek svrbť, ktorú škrábete.

## 🔁 Copy trading

Klenot koruny. Zrkadlo master účtu na mnoho a udržiavajte ich v sync aj keď internet
sa misbehaves.

- **[Copy trading](./copy-trading.md)** — jadro: mirroring, order types, SL/TP, slippage, desync/resync.
- **[Execution transparency](./copy-execution-transparency.md)** — vidieť presne, čo bolo kopírované, kedy a prečo.
- **[Performance fees](./copy-performance-fees.md)** — napoplaťte za váš signál, high-water-mark štýl.
- **[Provider marketplace](./copy-provider-marketplace.md)** — nechajte obchodníkov objaviť a nasledovať poskytovateľov.
- **[Notifications](./copy-notifications.md)** — dostanite povedaní keď sa niečo potrebuje.
- **[AI copy recommender](./ai-copy-recommender.md)** — nechajte AI navrhnúť koho kopírovať.
- **[Open API token lifecycle](./token-lifecycle.md)** — ako cMind udržiava presne jeden platný token per cID.

## 📊 Váš domov

- **[Dashboard](./dashboard.md)** — živý, mobile-first command center: KPIs s sparklines, activity chart, status ring, live feed a (pre admins) cluster health. Obnovuje sa sám.

## 🧠 AI jadro

Nie chat box zvracaný na stranu — AI, ktorá *skutočne robí prácu*.

- **[AI asistent, agent, risk guard & alerts](./ai.md)** — stratégia generácia, self-repairing builds, background risk guard, ktorá môže auto-stop bots a smart alerts.

## 🛠️ Build & run

- **[Build & backtest cBots](./build-and-backtest.md)** — in-browser Monaco IDE, C#/Python šablóny, sandboxed builds a live equity curves.
- **[MCP server](./mcp.md)** — vystaviť cMind nástroje cez HTTP + SSE, takže AI klienti ho môžu riadiť.

## 🏢 Spustite to ako obchod

- **[White-label / branding](./white-label.md)** — rebrand každý povrch cez config.
- **[Prop-firm challenge simulation](./prop-firm.md)** — vynúťte daily-loss, drawdown a target pravidlá s live equity.
- **[Feature toggles](./feature-toggles.md)** — rozhodnite čo každá deployment/tenant vidí.
- **[Compliance / legal](./compliance.md)** — audit trail a legal surface.

## 📱 Skúsenosť

- **[Installable app (PWA)](./pwa.md)** — mobile-first, offline shell, add-to-home-screen.
- **[UI design system & mobile-first](../ui-guidelines.md)** — design tokens a pravidlá za vzhľadom.

## ⚙️ Pod kapotou

Operačné bity, ktoré to všetko drží spustené:

- **[Node fleet & discovery](../operations/node-discovery.md)** — ako uzly self-register a heal.
- **[Horizontal scaling](../deployment/scaling.md)** — pridajte repliky, žiadny externý koordinátor potrebný.
- **[Logging & audit](../operations/logging.md)** — structured logs + OpenTelemetry.
- **[Deployment](../deployment/local.md)** — spustite kdekoľvek.

:::note Dokumenty čestne
Každý feature dokumentácia je udržiavaná v lockstep s kódom — zmena správania, update doc, rovnaký
commit. Ak kedy vidíte drift, to je bug: prosím
[otvoriť issue](https://github.com/amusleh-spotware-com/cmind/issues/new/choose) alebo poslať PR. 🙏
:::
