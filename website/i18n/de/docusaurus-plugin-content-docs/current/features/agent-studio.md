---
description: "Agent Studio – persona-getriebene, No-Code Trading-Agents mit Charakter und Archetyp erstellen, die Konten im Rahmen der Autonomy & Safety Kernel (Risikohülle, Circuit Breaker, Kill Switch, versionierter Haftungsausschluss-Consent) verwalten."
---

# Agent Studio

Agent Studio ermöglicht es, einen **Trading-Agent mit Charakter** zu erstellen — ohne Code — und ihm die
Verwaltung Ihrer Konten hin zu messbaren Zielen zu übertragen. Ein Agent ist wie ein
personengesteuerter cBot: Sie wählen einen Archetyp und eine Haltung, setzen die Schranken, und er läuft
unter der **Autonomy & Safety Kernel**.

Öffnen Sie **AI → Agent Studio** (`/agent-studio`).

## Einen Agenten erstellen

Der **Neuer Agent**-Dialog sammelt, ohne Code:

- **Name** und **Archetyp** — Scalper, Day Trader, Swing Trader, Position Trader, News Trader,
  Contrarian, Mean Reversion oder Breakout/Momentum. Jedes Preset legt eine sinnvolle Kadenz und Haltung fest.
- **Haltung** — Aggressivitäts-, Geduld- und Trendfolgen-Slider.
- **Verwaltete Konto(s)** — **mindestens ein ist erforderlich, um den Agenten zu erstellen** (ein Agent ohne Konto könnte nie starten, daher bleibt *Erstellen* deaktiviert, bis du einen auswählst). Wenn du noch kein Handelskonto verknüpft hast, teilt dir der Dialog dies mit und weist dich an, zuerst eines zu verknüpfen.
- **Autonomie-Stufe** — **Advisory** (schlägt nur vor) oder **Approval-gated** (handelt erst nach
  Ihrer Genehmigung pro Aktion). **Full Auto** (keine per-Trade-Genehmigung) erfordert zusätzlich eine
  **Risikohülle** und die Annahme des Risiko-Haftungsausschlusses, bevor es scharf geschaltet werden kann.

Die Persona wird **deterministisch** in den System-Prompt des Agenten kompiliert (kein LLM verfasst ihn),
sodass dieselbe Konfiguration immer dieselben Anweisungen erzeugt — reproduzierbar und prüfbar.

## Die Übersicht

Jeder Agent wird in einer Kontrollraum-Tabelle angezeigt: **welcher Agent, sein Typ, wie viele Konten er
verwaltet, seine Ziele, sein Run-Status und seine letzte Aktion**, mit **Start / Stop / Kill**-Steuerungen.
Der Kill-Switch stoppt einen laufenden Agenten sofort.

## Sicherheit ist eine Domain-Invariante, keine Einstellung

Alles, was Geld berührt, läuft durch die **Autonomy & Safety Kernel**:

- **Risikohülle** — harte per-Order-Limits (max. täglicher Verlust, offene Exposition, Positionsgröße,
  Hebel, aufeinanderfolgende Verluste, Orders/Stunde, erlaubte Symbole). Jede Order wird vor dem
  Dispatch dagegen validiert; ein Verstoß wird abgelehnt, nicht begrenzt. Erforderlich, bevor ein Agent
  Full Auto erreichen kann.
- **Circuit Breaker** — hält deterministisch neue Risiken an bei einer Verluststrähne, einem
  Tagesverlust-Verstoß, einem **harten Performance-Ziel-Verstoß** oder **AI-Provider-Unverfügbarkeit**
  (ein ausgefallenes oder halluzinierendes Modell eröffnet niemals neue Positionen).
- **Versionierter Haftungsausschluss-Consent** — eine einmalige, versionierte Annahme ist erforderlich,
  um Full Auto scharf zu schalten (rechtlich erforderlicher Consent, keine per-Trade-Genehmigung); eine
  Änderung des Haftungsausschlusses erzwingt erneutes Consent.
- **Kill Switch** — ein idempotenter Notfall-Stopp auf jedem laufenden Agenten.

## Ziele

Geben Sie einem Agenten **messbare Ziele** — z.B. *halte maximalen Drawdown unter 4%*, *Profit Factor
mindestens 1,5*, *Win-Rate ≥ 55%*. Jedes Ziel ist **Hard** (eine Schranke — ein Verstoß löst den
Circuit Breaker aus) oder **Soft** (beeinflusst nur die Argumentation), bewertet als On-track /
At-risk / Breached.

## Die Entscheidungspipeline

Sobald gestartet, läuft ein Agent eine **24/7 überwachte Schleife** (`AgentRuntimeService`). Bei jedem
Tick, für jedes verwaltete Konto: liest den **deterministischen Kontostand** (Ground Truth, nie die
Erinnerung des Modells); fragt die Entscheidungsmaschine nach einem Zug; leitet ihn durch das
**Safety Gate** (`AgentDecisionProcessor`) — Autonomie-Stufe → Circuit Breaker → Risikohülle; schreibt
einen append-only **`AgentDecisionRecord`**; und hält an oder führt aus, wie das Gate vorgibt. Die
Schleife ist **fehlerisoliert** (das Scheitern eines Agenten berührt nie einen anderen oder den Host) und
**standardmäßig sicher**: Sie ist träge, es sei denn AI ist konfiguriert *und*
`App:Ai:AgentRuntimeEnabled` ist gesetzt, und sie eröffnet niemals neue Risiken, während der AI
Provider nicht verfügbar ist.

- **Approval Gate** — eine vorgeschlagene Order eines **Approval-gated** Agenten wird als **Pending**
  aufgezeichnet und tut nichts, bis der Eigentümer sie genehmigt
  (`POST /api/agent-studio/{id}/decisions/{seq}/approve` oder `/reject`);
  **Full Auto** geht ohne per-Trade-Genehmigung durch die Hülle; **Advisory** macht nur Vorschläge.
- **Audit-Ledger** — jede Entscheidung ist wiedergebbar: Begründung (XAI), die zitierten Belege, das
  Gate-Urteil, die Orderabsicht und ob sie ausgeführt wurde, unter
  `GET /api/agent-studio/{id}/decisions`.
- **Research Desk** — ein bedarfsgesteuerter Multi-Agenten-Debatte: Alpha-/Sentiment-/Technische-/Risikoanalysten
  geben jeweils eine Einschätzung und ein Reviewer synthetisiert einen Vorschlag
  (`POST /api/agent-studio/{id}/debate`).
- **Memory** — der Agent erinnert sich an jede Entscheidung und ruft kürzliche Erinnerungen in seinen
  nächsten Prompt für Kontinuität ab (`GET /api/agent-studio/{id}/memory`).

Jede Zeile der Übersicht öffnet den Entscheidungs-Feed des Agenten (mit Genehmigen/Ablehnen bei
ausstehenden Orders), sein Memory und einen Run-Debatte-Tab.

## Umfang

Versendet: der vollständige Agenten-Lebenszyklus, das deterministische Safety Gate, die 24/7-Runtime,
das Human-in-the-Loop-Genehmigungsgate, den Audit-Ledger und die **Live-cTrader-Open-API-Integration**
— den Kontostand-Speicher (liest echtes Guthaben, Positionen und offene Exposition in Lots) und den
Order-Ausführer (platziert echte Market-Orders, Lots→Volumen über die Lot-Größe des Symbols), beide
auflösend die OAuth-Anmeldedaten jedes verwalteten Kontos und sicher degradierend, wenn ein Konto nicht
verknüpft ist. **Erfordert den Anthropic-API-Schlüssel**, damit das Modell Orders generiert (bis
dahin hält die Engine); noch ausstehend sind Multi-Agenten-Debatte-Rollen und Schichtgedächtnis/Reflexion.
Die Runtime ist aus, solange nicht `App:Ai:AgentRuntimeEnabled` gesetzt ist, sodass Live-Trading nur auf
einer expliziten, vollständig konsentierten Opt-in-Basis geschieht.

## Verwaltete Konten und Bearbeitung

Bei der Erstellung eines Agenten wählst du die Handelskonto(s), die er verwaltet – **mindestens eines ist bei der Erstellung erforderlich** (die *Erstellen*-Schaltfläche ist deaktiviert, bis eines ausgewählt ist, und der create-Endpunkt lehnt eine leere Auswahl ab). Jeder Agent kann später **bearbeitet** werden (Name, Temperament, Autonomie und verwaltete Konten) vom Stiftsymbol auf seiner Rosterzeile. Lifecycle-Steuerungen (Details, Bearbeitung, Start, Stop, Kill) sind Icon-Schaltflächen, jede in Zuständen deaktiviert, in denen die Aktion nicht anwendbar ist.
