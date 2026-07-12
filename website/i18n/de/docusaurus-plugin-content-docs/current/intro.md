---
slug: /intro
title: Willkommen bei cMind
description: Eine freundliche Einführung in cMind — die quelloffene, selbst hostbare Trading-Operations-Plattform für cTrader.
sidebar_position: 1
---

# Willkommen bei cMind 👋

Sie möchten also Trading-Bots bauen, sie backtesten, ohne Ihren Laptop zum Schmelzen zu bringen, sie
auf mehreren Rechnern laufen lassen, Trades auf ein Dutzend Konten spiegeln und eine KI das Risiko im
Auge behalten lassen, während Sie schlafen. **Sie sind genau richtig hier.**

cMind ist eine **quelloffene, selbst hostbare Trading-Operations-Plattform für cTrader**. Stellen Sie
es sich als Ihren gesamten Trading-Desk vor — Erstellung, Ausführung, eine Compute-Flotte, Copy-Trading
und einen KI-Kern — verpackt in einer ruhigen, dunklen, mobiltauglichen App, die Ihnen von A bis Z gehört.

:::tip In einem Satz
Bauen → backtesten → ausführen → kopieren Sie Ihre cTrader-Strategien im großen Maßstab, mit
eingebauter KI, auf Ihren eigenen Servern, unter Ihrer eigenen Marke.
:::

## Was kann es tatsächlich?

| Sie möchten … | cMind erledigt es | Mehr lesen |
|---|---|---|
| Einen cBot im Browser schreiben | Monaco-IDE + C#/Python-Vorlagen, sandboxed Builds | [Bauen & backtesten](./features/build-and-backtest.md) |
| Über mehrere Rechner backtesten | Eine selbstheilende Node-Flotte wählt die am wenigsten ausgelastete Maschine | [Skalierung](./deployment/scaling.md) |
| Ein Konto auf viele kopieren | Robustes Spiegeln mit Resync, keine Doppel-Trades | [Copy-Trading](./features/copy-trading.md) |
| KI die Fleißarbeit machen lassen | Strategie-Generierung, Selbstreparatur, Risk Guard, Post-mortems | [KI-Kern](./features/ai.md) |
| Innerhalb der Prop-Firm-Regeln bleiben | Live-Equity-Tracking + Simulation der Challenge-Regeln | [Prop-Firm](./features/prop-firm.md) |
| Es als *Ihr* Produkt ausliefern | Vollständiges White-Label: Name, Farben, Logo, Favicon | [White-Label](./features/white-label.md) |
| Es auf Ihrem Handy betreiben | Installierbare, mobile-first PWA | [PWA](./features/pwa.md) |
| Es von einem KI-Client aus steuern | Eingebauter MCP-Server (HTTP + SSE) | [MCP](./features/mcp.md) |

## Der 5-Minuten-Weg ⏱️

Wenn Sie Docker und fünf Minuten haben, können Sie gerade jetzt an einer echten cMind-Instanz herumprobieren:

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
cp .env.example .env        # set OWNER_EMAIL + OWNER_PASSWORD
docker compose up --build
```

Öffnen Sie dann **<http://localhost:8080>**, melden Sie sich an, und schon geht's los. Die vollständige
Anleitung (samt Fehlerbehebung für den Fall, dass Docker unweigerlich eigene Ansichten hat) finden Sie
unter **[Lokal ausführen](./deployment/local.md)**.

## Neu hier? Folgen Sie dem gelben Ziegelsteinweg 🟡

1. **[Für wen ist das?](./audience.md)** — vergewissern Sie sich, dass Sie unsere Art von Ärger sind.
2. **[Lokal ausführen](./deployment/local.md)** — bringen Sie eine echte Instanz zum Laufen.
3. **[Funktionen](./features/README.md)** — die vollständige Tour durch alles, was drinsteckt.
4. **[Richtig deployen](./deployment/cloud.md)** — Docker, Kubernetes, Azure, AWS.
5. **[Machen Sie es zu Ihrem](./white-label-for-business.md)** — versehen Sie es mit Ihrem White-Label.
6. **[Mitwirken](./contributing.md)** — PRs (menschlich *und* KI-unterstützt) sehr willkommen.

## Ein kurzes Wort zum Geld 💸

cMind bewegt **echtes Kapital**. Wir nehmen das ernst — jede Änderung wird mit Unit-, Integrations- und
End-to-End-Tests ausgeliefert, Fehlerpfade inklusive (abgebrochene Verbindungen, abgelehnte Orders, tote
Nodes). Sie sollten es auch ernst nehmen: **testen Sie zuerst auf einem Demokonto**, und lesen Sie die
[Compliance-Hinweise](./features/compliance.md), bevor Sie es auf etwas Echtes richten. Trading ist
riskant; diese Software ist ein Werkzeug, keine Finanzberatung.

Gut — genug der Vorrede. Lassen Sie uns etwas bauen. →
