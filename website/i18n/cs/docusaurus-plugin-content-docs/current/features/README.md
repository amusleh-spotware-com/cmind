---
slug: /features
title: Vlastnosti — plná prohlídka
description: Všechno co cMind umí — copy trading, AI, build & backtest, prop-firm guards, white-label, PWA, MCP, a více.
sidebar_label: Přehled
---

# Vlastnosti — plná prohlídka

Vítejte na velké prohlídce. cMind balí *hodně* do jedné aplikace, takže zde je mapa. Každá schopnost má svůj vlastní deep-dive doc — kliknutí přes na cokoliv co škrábete.

## Copy trading

Korunní klenot. Zrcadlí master účet na mnoho, a udržovat je v sync i když internet se chová špatně.

- **[Copy trading](./copy-trading.md)** — jádro: zrcadlení, order typy, SL/TP, slippage, desync/resync.
- **[Execution transparentnost](./copy-execution-transparency.md)** — viz přesně co bylo zkopírováno, kdy, a proč.
- **[Výkonnostní poplatky](./copy-performance-fees.md)** — účet pro váš signál, high-water-mark styl.
- **[Provider marketplace](./copy-provider-marketplace.md)** — nechaj tradery objevit a sledovat poskytovatele.
- **[Upozornění](./copy-notifications.md)** — dostat řečeno když něco potřebuje vás.
- **[AI copy doporučovatel](./ai-copy-recommender.md)** — nechaj AI navrhnout koho kopírovat.
- **[Open API token lifecycle](./token-lifecycle.md)** — jak cMind udržuje přesně jeden platný token per cID.

## Vaše domovská základna

- **[Dashboard](./dashboard.md)** — live, mobile-first velké velitele: KPIs s sparklines, activity chart, status ring, live feed, a (pro adminy) cluster health. Obnovuje se sama.

## AI jádro

Ne chatbox sešroubován na stranu — AI to opravdu *dělá práci*.

- **[AI asistent, agent, risk guard & alerts](./ai.md)** — strategie generování, self-repairing builds, background risk guard, který může auto-stop boty, a smart alerts.

## Staví & Běžet

- **[Staví & backtest cBots](./build-and-backtest.md)** — in-browser Monaco IDE, C#/Python templates, sandboxed builds, a live equity křivky.
- **[MCP server](./mcp.md)** — exponuje cMind's nástrojů přes HTTP + SSE takže AI klienti mohou to řídit.

## Spustit to jako obchod

- **[White-label / branding](./white-label.md)** — přebranduj každý povrch přes config.
- **[Prop-firm challenge simulace](./prop-firm.md)** — vynuť daily-loss, drawdown, a target pravidla s live equity.
- **[Feature toggles](./feature-toggles.md)** — rozhoduji co každé nasazení/tenant vidí.
- **[Compliance / právní](./compliance.md)** — audit trail a právní povrch.

## Zážitek

- **[Instalovatelná aplikace (PWA)](./pwa.md)** — mobile-first, offline shell, add-to-home-screen.
- **[UI design systém & mobile-first](../ui-guidelines.md)** — design tokens a pravidla za vzhled.

## Pod kapotou

Operační bity, které to všechno běží:

- **[Node fleet & discovery](../operations/node-discovery.md)** — jak uzly self-register a léčí.
- **[Horizontální scaling](../deployment/scaling.md)** — přidat repliky, bez externí koordinátor potřeba.
- **[Logging & audit](../operations/logging.md)** — strukturované logy + OpenTelemetry.
- **[Deployment](../deployment/local.md)** — dostat běžící kdekoli.

:::note Udržování dokumentů čestné
Každý feature doc je udržován v lockstep s kódem — změna chování, aktualizace doc, stejný commit. Pokud kdy vidíte drift, to je bug: prosím [otevřít issue](https://github.com/amusleh-spotware-com/cmind/issues/new/choose) nebo poslat PR.
:::
