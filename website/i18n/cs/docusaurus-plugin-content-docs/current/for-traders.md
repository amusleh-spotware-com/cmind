---
slug: /for-traders
title: cMind pro cTrader tradery
description: Proč by měl cTrader trader self-hostovat cMind — vlastněte svůj stack a data, autorujte, backtestujte, spouštějte a monitorujte cBots v jedné AI-powered konzoli, na notebooku, VPS nebo telefonu.
keywords:
  - cTrader
  - algorithmic trading
  - self-hosted trading platform
  - cBot backtesting
  - AI trading bots
  - open source trading software
sidebar_position: 5
---

# cMind pro cTrader tradery

Již obchodujete na cTrader. Již manuálně přepínáte mezi editorem kódu, backtesterem, VPS a třemi záložkami prohlížeče. **cMind to vše shrnuje do jedné tmavé, klávesnici-přívětivé konzole, kterou si sami provozujete** — a je open source, takže nic z vaší výhody, vašich strategií nebo vašich pověření nikdy neopustí váš počítač.

:::tip TL;DR
Self-hostujte cMind na notebooku, levném VPS nebo domácím serveru. Autorujte, backtestujte, spouštějte a monitorujte cBots na jednom místě, s AI jádrem dělajícím práci. → [Spusťte to za 5 minut](./deployment/local.md)
:::

## Proč self-hostovat místo hostované služby?

- **Vlastněte svůj stack a svá data.** Vaše cBots, pověření, tokeny a historie equity žijí na **vaší** infrastruktuře — žádná třetí strana, žádné lock-in, žádný email "tento produkt ukončujeme".
- **Je to skutečně vaše k změně.** C# 14 / .NET 10, strict DDD, EF Core + PostgreSQL, MCP server — vše open source a hackable. Forkněte to, rozšiřte to, pošlete PR.
- **Žádný paywall per-funkce.** Přineste si vlastní AI klíč pro jakéhokoliv poskytovatele; každá AI funkce je zapnutá.

Neché se vám nelíbí provozovat servery samy? Hostingová společnost může pro vás provozovat spravovaného cMind — viz [Pro cloud & VPS poskytovatele](./for-cloud-providers.md).

## Jedna konzole, žádné přepínání záložek

- **Autorujte** v skutečném Monaco IDE (editor VS Code), s šablonami **C#** i **Python** a sandboxovaným `dotnet build` v jednorázových kontejnerech. → [Sestavení a backtest](./features/build-and-backtest.md)
- **Backtestujte** napříč flotilou uzlů a sledujte equity křivky streamovat zpět živě.
- **Spouštějte** strategie live a **monitorujte** je z jednoho dashboardu. → [Dashboard](./features/dashboard.md)
- **Kopírujte** master účet na mnoho účtů napříč brokery a cTrader ID, s rekonciliací, která přežije přerušená spojení a rotující tokeny. → [Kopírování obchodů](./features/copy-trading.md)

## AI, která dělá práci, ne small talk

Přineste si vlastní API klíč (jakéhokoliv podporovaného poskytovatele — cloud nebo lokální model) a získejte plain-English → skutečný kompilující se cBot se smyčkou samoopravy, laděním parametrů, post-mortemy backtestů a rizikovým strážcem, který umí automaticky zastavit neposlušného bota. → [Poznámte AI jádro](./features/ai.md)

## Institucionální nástroje, pro jednoho

Stejná přísnost, kterou si desk pays for, na vašem vlastním počítači:

- [Backtest integrity](./features/backtest-integrity.md) · [Position sizing](./features/position-sizing.md)
- [Strategy health](./features/strategy-health.md) · [Regime lab](./features/regime-lab.md)
- [Execution TCA](./features/execution-tca.md) · [Trading journal](./features/trading-journal.md)
- [Agent Studio](./features/agent-studio.md) · [Kontrariánské pozicování](./features/contrarian-positioning.md)

## Běží tam, kde vy

Začněte na notebooku s `docker compose up`, přejděte na levné VPS nebo domácí server, až budete připraveni, a kontrolujte své boty z telefonu — cMind je instalovatelná, mobilně prioritní [PWA](./features/pwa.md). → [Spusťte to lokálně](./deployment/local.md)

Chcete, aby to váš AI klient řídil? Existuje vestavěný [MCP server](./features/mcp.md).

## Pomozte to zlepšit

cMind je open source a MIT-licencovaný — roadmap je tvarován komunitou:

- Nahlasujte issues a požadavky na funkce a hlasujte o tom, na čem záleží.
- Přidejte šablony cBot, adaptéry poskytovatele AI nebo UI překlady.
- Pošlete PR — tři testovací úrovně (unit + integration + E2E) a strict DDD udržují laťku vysoko a [Contributing guide](./contributing.md) vás provede.

Připraveni? → [Přečtěte si úvod](./intro.md) pak [spusťte to lokálně](./deployment/local.md).
