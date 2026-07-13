---
description: "Trading-Journal & Coach — analysiert Ihre eigenen Läufe und Backtests auf behaviorliche Lecks (Über-Konzentration, wiederholte Ausfälle, eine verlierende Neigung) und coacht Sie auf die Strategie, die Sie bereits haben. Deterministisch, mit optionaler KI-Narrative."
---

# Trading-Journal & Coach

Die neueste wirklich nützliche Kategorie von KI-für-Trading ist nicht die Markt-Vorhersage — es ist die Analyse von *Ihrer eigenen* Verhalten. Das Trading-Journal verwandelt Ihre Geschichte von Läufen und Backtests in ehrliches Feedback, sodass Sie die Strategie verbessern können, die Sie bereits haben.

Öffnen Sie **AI → Trading Journal** (`/journal`).

## Was es zeigt

Aus Ihren Instances (Läufe und Backtests) berechnet es deterministisch:

- **Gewinn / Verlust / Fehler-Zähler und Gewinnquote** über Ihre Backtests;
- **Behaviorale Einsichten** — die Lecks, die Retail-Trader leise kosten:
  - **Über-Konzentration** — die meisten Ihrer Aktivitäten sind in einem Symbol;
  - **Wiederholte Ausfälle** — ein hoher Anteil der Läufe schaffte es nicht zu bauen oder zu konfigurieren;
  - **Verlierende Neigung** — mehr verlierende als gewinnende Backtests (mit einem Nudge, um das Integrity Lab zu laufen und zu überprüfen, dass die Edge wirklich ist);
  - eine saubere Gesundheits-Bescheinigung wenn nichts der obigen Punkte gilt.

```http
GET /api/journal
```

## Warum es zuverlässig ist

Die behaviorale Analyse ist reiner, deterministischer Domänen-Code (`Core.Journal`) mit keiner Infrastruktur-Abhängigkeit — Unit-getestet für Über-Konzentration, wiederholte Ausfälle, verlierende Neigung, die ausgewogene Case und das leere Konto. Die Fakten kommen zuerst; der KI-Coach (Portfolio-Zusammenfassung) ist eine optionale Narrative-Schicht oben, gated auf dem Anthropic-API-Schlüssel, sodass das Journal vollständig ohne konfiguriert KI funktioniert.
