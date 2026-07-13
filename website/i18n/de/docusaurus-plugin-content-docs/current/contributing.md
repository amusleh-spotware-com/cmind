---
slug: /contributing
title: Beitragen
description: Wie man zu cMind beitragen kann — Mensch oder KI-unterstützt PRs willkommen. Erste Beitrag in 10 Minuten.
sidebar_position: 5
---

# Zu cMind beitragen

Danke, dass du hier bist. cMind wird besser jedes einzelne Mal wenn jemand öffnet ein Problem, berichtet Präzision cTrader Verhalten, behebt ein Tippfehler in diese sehr Dokumente, oder Schiffe ein PR. **Sie brauchen nicht zu sein ein .NET Zauberer** — Tester, Trader, und Doc-Fixer sind wie wertvoll wie die Leute schreiben Aggregate.

:::tip Die Kanonisch Leitfaden lebt im Repo

Diese Seite ist die Freundlich Einfahrt. Die Vollständig, immer-aktuell Prozess — Grund Regeln, Kodierung Konventionen, Überprüfung Fluss — ist in **[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md)**.

:::

## Ihr erste Beitrag in ~10 Minuten

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
dotnet restore
dotnet build          # 0 Warnungen, oder CI wird Sie höflich verweigern
dotnet test           # Unit + Integration + E2E
```

Etwas zu beheben gefunden? Branche es, ändere es, füge ein Test, und öffne ein PR. Das ist die ganze Schleife.

## Wege um zu helfen (nicht alle davon sind Code)

| Beitrag | Anstrengung | Wo |
|---|---|---|
| 🐛 Berichts ein wiederholbar Fehler | 10 min | [Fehler Bericht](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) |
| 💡 Schlag ein Feature vor | 10 min | [Feature Anfrage](https://github.com/amusleh-spotware-com/cmind/issues/new?template=feature_request.yml) |
| 📖 Verbessern diese Dokumente | 15 min | Bearbeiten unter `website/docs/` und PR |
| 🧪 Hinzufügung ein fehlend Test | 30 min | `tests/UnitTests` · `IntegrationTests` · `E2ETests` |
| 🧠 Berichts exakt cTrader Verhalten | 10 min | [Öffnen ein Diskussion](https://github.com/amusleh-spotware-com/cmind/discussions) |

## Die Haus-Regeln (kurz Version)

cMind bewegt **echten Geld**, daher ein paar Sachen sind nicht verhandelbar — und ehrlich, sie machen die Codebase ein Freude zu arbeiten in:

- **Streng Domain-Driven Design.** Geschäft-Logik lebt auf Aggregate und Wertobjekte, nein in Endpoint oder UI. (Es gibt ein Freundlich Playbook für es im Repo.)
- **Drei Test Tier, jede Änderung.** Unit + Integration + E2E, *einschließlich* Misserfolg Pfade (Aufgelöst Verbindung, Abgelöst Bestellung, Tote Knoten). Grün Test sind die Preis der Zulassung.
- **Null Warnungen.** `TreatWarningsAsErrors=true`. Moderne C# 14 Idiom.
- **Keine Geheimnisse, nein Magic-Zeichenfolgen, nein `DateTime.UtcNow`** (injizieren `TimeProvider` anstelle).
- **Dokumente in gleich Festschrift.** Änderung Verhalten → Update sein Dokument. Ja, dies enthält diese Seite.

Vollständig Detail, mit die *Warum* hinter jeder Regel, in [CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md) und [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md).

## Beitrag mit KI

Wir ehrlich willkommen **KI-unterstützt PRs** — diese Projekt ist gebaut zu sein funktioniert auf durch Agenten sowie Menschen. Wenn du fahren Claude, Copilot, oder ähnlich: Zeigen Sie es bei [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md), lasse es lesen die verschachtelt `CLAUDE.md` Dateien, und halte es zur gleich Bar (Test, Null Warnungen, DDD). Ein gut KI PR ist nicht zu unterscheiden von gut Mensch PR — gleich Überprüfung, gleich Willkommen.

## Seien Sie ausgezeichnet zu jedem andere

Wir haben ein [Verhaltenskodex](https://github.com/amusleh-spotware-com/cmind/blob/main/CODE_OF_CONDUCT.md). Die Gist: sei nett, gehe aus gut Glaube, und merke es gibt ein Person (oder ein Person Agent) auf die andere Ende. Fragen Sie früh — dies ein Stärke, nicht ein Bother.

Willkommen an Bord. Wir können nicht warten zu sehen was du Bau.
