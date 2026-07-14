---
slug: /for-traders
title: cMind für cTrader-Trader
description: Warum ein cTrader-Trader cMind selbst hosten sollte – besitze deinen Stack und deine Daten, erstelle, teste, führe und beobachte cBots in einer KI-gestützten Konsole auf deinem Laptop, VPS oder Telefon.
keywords:
  - cTrader
  - algorithmic trading
  - self-hosted trading platform
  - cBot backtesting
  - AI trading bots
  - open source trading software
sidebar_position: 5
---

# cMind für cTrader-Trader 📈

Du handelst bereits auf cTrader. Du jonglierst bereits mit einem Code-Editor, einem Backtester, einem VPS und drei Browser-Tabs. **cMind kollabiert alles das in eine dunkle, tastaturfreundliche Konsole, die du selbst betreibst** – und sie ist Open Source, daher verlassen deine Edge, deine Strategien oder deine Anmeldedaten niemals deine Box.

:::tip[TL;DR]
Hoste cMind selbst auf einem Laptop, einem billigen VPS oder einem Home-Server. Erstelle, teste, führe aus und beobachte cBots an einem Ort, mit einem KI-Kern, der die Hausarbeit erledigt. → [Führe es in 5 Minuten aus](./deployment/local.md)
:::

## Warum selbst hosten statt einen gehosteten Service?

- **Besitze deinen Stack und deine Daten.** Deine cBots, Anmeldedaten, Tokens und Eigenkapital-History leben auf **deiner** Infrastruktur – kein dritter Anbieter, keine Lock-in, keine E-Mail "wir stellen dieses Produkt ein".
- **Es ist wirklich dein zum Ändern.** C# 14 / .NET 10, striktes DDD, EF Core + PostgreSQL, ein MCP-Server – alles Open Source und hackbar. Forke es, erweitere es, sende eine PR.
- **Keine Pro-Feature-Paywall.** Bring deine eigenen KI-Keys für jeden Provider; jedes KI-Feature ist dabei.

Bevorzugst du es nicht, Server selbst zu betreiben? Ein Hosting-Unternehmen kann verwaltetes cMind für dich laufen lassen – siehe [Für Cloud- und VPS-Anbieter](./for-cloud-providers.md).

## Eine Konsole, kein Tab-Jonglieren

- **Erstelle** in einer echten Monaco IDE (dem VS Code Editor), mit C# **und** Python-Templates und sandboxy `dotnet build` in einmaligen Containern. → [Build & Backtest](./features/build-and-backtest.md)
- **Teste rückwärts** über eine Flotte von Nodes und beobachte Equity-Kurven, die live zurückströmen.
- **Führe** Strategien live aus und **überwache** sie von einem Dashboard. → [Dashboard](./features/dashboard.md)
- **Kopiere** ein Master-Konto auf viele Konten über Broker und cTrader-IDs, mit Abstimmung, die Verbindungsabbrüche und rotierende Tokens übersteht. → [Copy Trading](./features/copy-trading.md)

## KI, die Hausarbeit erledigt, keine Smalltalk

Bring deine eigene API-Key (jeden unterstützten Provider – Cloud oder ein lokales Modell) und erhalte Englisch in Klartext → einen echten kompilierenden cBot mit einer Self-Repair-Loop, Parameter-Tuning, Backtest-Obduktionen und einer Risiko-Guard, die einen fehlerhaften Bot auto-stoppen kann. → [Treffe den KI-Kern](./features/ai.md)

## Institutional-Grade-Tooling, für eine Person

Die gleiche Strenge, die eine Desk zahlt, auf deiner Box:

- [Backtest-Integrität](./features/backtest-integrity.md) · [Position-Sizing](./features/position-sizing.md)
- [Strategie-Gesundheit](./features/strategy-health.md) · [Regime Lab](./features/regime-lab.md)
- [Ausführungs-TCA](./features/execution-tca.md) · [Handels-Journal](./features/trading-journal.md)
- [Agent Studio](./features/agent-studio.md) · [Gegensätzliche Positionierung](./features/contrarian-positioning.md)

## Läuft wo du bist

Beginne auf deinem Laptop mit `docker compose up`, wechsle zu einem billigen VPS oder einem Home-Server, wenn du bereit bist, und überprüfe deine Bots von deinem Telefon – cMind ist eine installierbare, mobile-first [PWA](./features/pwa.md). → [Führe es lokal aus](./deployment/local.md)

Möchtest du, dass dein KI-Client es fahrt? Es gibt einen eingebauten [MCP-Server](./features/mcp.md).

## Hilf, es besser zu machen

cMind ist Open Source und MIT-lizenziert – die Roadmap wird von der Gemeinde geprägt:

- Reiche Issues und Feature-Requests ein und stimme ab, was zählt.
- Füge cBot-Templates, KI-Provider-Adapter oder UI-Übersetzungen hinzu.
- Sende PRs – drei Test-Ebenen (Unit + Integration + E2E) und striktes DDD halten die Messlatte hoch, und der [Beitragsleitfaden](./contributing.md) führt dich durch.

Bereit? → [Lies die Einführung](./intro.md) und [führe es lokal aus](./deployment/local.md).
