---
slug: /for-traders
title: cMind pre cTrader obchodníkov
description: Prečo by mal cTrader obchodník self-hostovať cMind — vlastniť váš stack a dáta, author, backtest, spúšťanie a monitorovanie cBots v jednej AI-powered konzole, na vašom notebooku, VPS alebo telefóne.
keywords:
  - cTrader
  - algoritmus trading
  - self-hosted trading platform
  - cBot backtesting
  - AI trading bots
  - open source trading software
sidebar_position: 5
---

# cMind pre cTrader obchodníkov 📈

Už obchodujete na cTrader. Už žongujete s code editorom, backtesterom, VPS a tromi
prehliadačovými záložkami. **cMind zrúta všetko to do jednej tmavej, keyboard-friendly konzoly, ktorú spúšťate
sami** — a je to open source, takže nič o vašej výhode, vašich stratégiách alebo vašich poverení
nikdy neopúšťa váš box.

:::tip[TL;DR]
Self-hostujte cMind na notebooku, lacnom VPS alebo home serveri. Author, backtest, spúšťanie a monitorovanie cBots
na jednom mieste, s AI jadrom robiace chores. → [Spustiť za 5 minút](./deployment/local.md)
:::

## Prečo self-hostovať namiesto hosted service?

- **Vlastniť váš stack a vaše dáta.** Vaše cBots, poverení, tokeny a equity história žijú na
  **vašej** infraštruktúre — žiadny tretí subjekt, žiadny lock-in, žiadny "ukončujeme tento produkt" email.
- **Je to skutočne vašu zmenu.** C# 14 / .NET 10, striktný DDD, EF Core + PostgreSQL, MCP
  server — všetko open source a hackable. Fork, rozšírte, pošlite PR.
- **Žiadny per-feature paywall.** Priveďte si vlastný AI kľúč pre každého poskytovateľa; každá AI funkcia je zapnutá.

Nechcete sami spúšťať servery? Hosting spoločnosť môže spustiť managed cMind pre vás —
pozrite [Pre cloud & VPS poskytovateľ](./for-cloud-providers.md).

## Jedna konzola, žiadne tab-juggling

- **Author** v skutočnom Monaco IDE (VS Code editor), s C# **a** Python šablón a
  sandboxed `dotnet build` v jednorazových kontajneroch. → [Build & backtest](./features/build-and-backtest.md)
- **Backtest** cez flotilu uzlov a pozrite equity curves stream back live.
- **Spustite** stratégie live a **monitorujte** ich z jedného dashboard. → [Dashboard](./features/dashboard.md)
- **Kopírovať** master účet na viac účtov cez brokerov a cTrader ID, s zosúladením
  ktoré prežije dropped connections a rotating tokeny. → [Copy trading](./features/copy-trading.md)

## AI že robí chores, nie small talk

Priveďte si vlastný API kľúč (akýkoľvek podporovaný poskytovateľ — cloud alebo local model) a získajte plain-English → skutočný kompilujúci cBot s self-repair slučkou, parameter tuning, backtest post-mortems a risk
guard, ktorá môže auto-stop misbehaving bot. → [Stretnutie AI jadro](./features/ai.md)

## Institucjonálny-grade tooling, pre jedného

Rovnaký rigor, ktorý stôl platí, na vašom vlastnom boxe:

- [Backtest integrity](./features/backtest-integrity.md) · [Position sizing](./features/position-sizing.md)
- [Strategy health](./features/strategy-health.md) · [Regime lab](./features/regime-lab.md)
- [Execution TCA](./features/execution-tca.md) · [Trading journal](./features/trading-journal.md)
- [Agent Studio](./features/agent-studio.md) · [Contrarian positioning](./features/contrarian-positioning.md)

## Bežíš tam, kde si ty

Začnite na notebooku s `docker compose up`, absolvent lacného VPS alebo home servera, keď ste
pripravení a skontrolujte si vašich botov z telefónu — cMind je installable, mobile-first
[PWA](./features/pwa.md). → [Spustite lokálne](./deployment/local.md)

Chcete, aby váš AI klient ho riadil? Tu je zabudovaný [MCP server](./features/mcp.md).

## Pomôžte to vylepšiť

cMind je open source a MIT-licensed — roadmap je community-shaped:

- Podajte problémy a feature requests a hlasujte čo záleží.
- Pridajte cBot šablóny, AI provider adaptéry alebo UI preklady.
- Pošlite PRs — tri test tiers (unit + integration + E2E) a striktný DDD držia bar vysoký a
  [Contributing guide](./contributing.md) vás povedie cez to.

Pripravení? → [Čítajte intro](./intro.md) potom [spustiť lokálne](./deployment/local.md).
