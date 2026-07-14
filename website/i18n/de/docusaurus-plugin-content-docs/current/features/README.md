---
slug: /features
title: Features – die volle Tour
description: Alles, was cMind kann – Copy-Trading, KI, Build & Backtest, Prop-Firm-Guards, White-Label, PWA, MCP und mehr.
sidebar_label: Übersicht
---

# Features – die volle Tour 🧭

Willkommen zur großen Tour. cMind packt *viel* in eine App, daher hier ist die Karte. Jede Fähigkeit hat ihre eigene Deep-Dive-Doc – klicke durch zu allem, was dich kratzt.

## 🔁 Copy-Trading

Die Kronjuwel. Spiegele ein Master-Konto auf viele, und halte sie synchron, sogar wenn das Internet sich misbehaves.

- **[Copy-Trading](./copy-trading.md)** – der Kern: Spiegelung, Order-Typen, SL/TP, Slippage, Desync/Resync.
- **[Ausführungs-Transparenz](./copy-execution-transparency.md)** – siehe genau was kopiert wurde, wann und warum.
- **[Performance Fees](./copy-performance-fees.md)** – berechne für dein Signal, High-Water-Mark-Stil.
- **[Provider Marketplace](./copy-provider-marketplace.md)** – lass Trader Anbieter entdecken und folgen.
- **[Benachrichtigungen](./copy-notifications.md)** – werde benachrichtigt, wenn etwas dich braucht.
- **[KI Copy-Recommender](./ai-copy-recommender.md)** – lass die KI vorschlagen, wem zu folgen.
- **[Open API Token-Lebenszyklus](./token-lifecycle.md)** – wie cMind genau einen gültigen Token pro cID behält.

## 📊 Deine Heimatbasis

- **[Dashboard](./dashboard.md)** – die Live, Mobile-First Command Center: KPIs mit Sparklines, ein Aktivitäts-Chart, ein Status-Ring, ein Live-Feed und (für Admins) Cluster-Gesundheit. Es aktualisiert sich selbst.

## 🧠 KI-Kern

Kein Chat-Box, die auf die Seite geschraubt ist – KI, die tatsächlich *die Arbeit macht*.

- **[KI-Assistent, Agent, Risk Guard & Alerts](./ai.md)** – Strategy-Generierung, Selbst-Reparatur-Builds, ein Hintergrund-Risk-Guard, der Bots auto-stoppen kann, und Smart-Alerts.

## 🛠️ Build & Run

- **[Build & Backtest cBots](./build-and-backtest.md)** – die In-Browser Monaco IDE, C#/Python-Templates, Sandboxed-Builds und Live-Equity-Kurven.
- **[MCP-Server](./mcp.md)** – exposiere cMind's Tools über HTTP + SSE, daher KI-Clients können es fahren.

## 🏢 Führe es als Business

- **[White-Label / Branding](./white-label.md)** – rebrand jede Oberfläche über Config.
- **[Prop-Firm-Challenge-Simulation](./prop-firm.md)** – setze Daily-Loss, Drawdown und Target-Regeln mit Live-Eigenkapital durch.
- **[Feature-Toggles](./feature-toggles.md)** – entscheide, was jede Bereitstellung/Tenant sieht.
- **[Compliance / Rechtliches](./compliance.md)** – die Audit-Spur und Rechts-Oberfläche.

## 📱 Das Erlebnis

- **[Installierbare App (PWA)](./pwa.md)** – Mobile-First, Offline-Shell, Add-to-Home-Screen.
- **[UI-Design-System & Mobile-First](../ui-guidelines.md)** – die Design-Tokens und Regeln hinter dem Look.

## ⚙️ Unter der Haube

Die operativen Bits, die alles am Laufen halten:

- **[Node-Flotte & Erkennung](../operations/node-discovery.md)** – wie Nodes sich selbst registrieren und heilen.
- **[Horizontale Skalierung](../deployment/scaling.md)** – Replicas hinzufügen, kein externer Koordinator nötig.
- **[Logging & Audit](../operations/logging.md)** – strukturierte Logs + OpenTelemetry.
- **[Bereitstellung](../deployment/local.md)** – get it running anywhere.

:::note[Docs ehrlich halten]
Jede Feature-Doc wird im Lockstep mit dem Code gehalten – ändere das Verhalten, aktualisiere die Doc, gleicher Commit. Wenn du jemals Drift bemerkst, das ist ein Bug: bitte [öffne ein Issue](https://github.com/amusleh-spotware-com/cmind/issues/new/choose) oder sende eine PR. 🙏
:::
