---
slug: /contributing
title: Beitragen
description: Wie du zu cMind beitragen kannst – von Menschen oder KI unterstützte PRs willkommen. Erster Beitrag in 10 Minuten.
sidebar_position: 5
---

# Zu cMind beitragen 🛠️

Danke, dass du hier bist. cMind wird bei jedem Beitrag besser – ob jemand ein Issue öffnet, präzises cTrader-Verhalten berichtet, einen Tippfehler in dieser Dokumentation behebt oder eine PR einreicht. **Du brauchst kein .NET-Magier zu sein** – Tester, Trader und Doc-Fixer sind genauso wertvoll wie diejenigen, die Aggregate schreiben.

:::tip Der kanonische Leitfaden lebt im Repository
Diese Seite ist die freundliche Einstiegshilfe. Der vollständige, immer aktuelle Prozess – Grundregeln, Coding-Konventionen, Review-Ablauf – findet sich in **[CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md)**.
:::

## Dein erster Beitrag in ~10 Minuten

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
dotnet restore
dotnet build          # 0 Warnungen, sonst wird CI dich höflich ablehnen
dotnet test           # Unit + Integration + E2E
```

Etwas zum Beheben gefunden? Verzweige den Code, änder ihn, füge einen Test hinzu und öffne eine PR. Das ist der ganze Ablauf.

## Wege, wie du helfen kannst (nicht alles ist Code)

| Beitrag | Aufwand | Wo |
|---|---|---|
| 🐛 Reproduzierbaren Bug melden | 10 Min | [Bug-Report](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) |
| 💡 Feature vorschlagen | 10 Min | [Feature-Request](https://github.com/amusleh-spotware-com/cmind/issues/new?template=feature_request.yml) |
| 📖 Diese Dokumente verbessern | 15 Min | Bearbeite unter `website/docs/` und erstelle eine PR |
| 🧪 Fehlende Tests hinzufügen | 30 Min | `tests/UnitTests` · `IntegrationTests` · `E2ETests` |
| 🧠 Exaktes cTrader-Verhalten melden | 10 Min | [Öffne eine Diskussion](https://github.com/amusleh-spotware-com/cmind/discussions) |

## Die Hausregeln (Kurzfassung)

cMind bewegt **echtes Geld**, daher sind einige Dinge nicht verhandelbar – und ehrlich gesagt machen sie die Codebase zu einem Freuden, darin zu arbeiten:

- **Striktes Domain-Driven Design.** Business-Logik lebt auf Aggregaten und Value Objects, niemals in Endpoints oder UI. (Es gibt einen freundlichen Playbook dafür im Repo.)
- **Drei Test-Ebenen, jede Änderung.** Unit + Integration + E2E, *einschließlich* Fehler-Pfade (Verbindungsabbrüche, abgelehnte Orders, tote Nodes). Grüne Tests sind der Preis der Zulassung.
- **Null Warnungen.** `TreatWarningsAsErrors=true`. Modernes C# 14.
- **Keine Secrets, keine Magic-Strings, niemals `DateTime.UtcNow`** (injiziere stattdessen `TimeProvider`).
- **Dokumentation im gleichen Commit.** Verhalten ändern → Dokumentation aktualisieren. Ja, das schließt diese Website ein.

Alle Details mit der *Begründung* hinter jeder Regel findest du in [CONTRIBUTING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/CONTRIBUTING.md) und [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md).

## Mit KI beitragen 🤖

Wir begrüßen **KI-unterstützte PRs** ausdrücklich – dieses Projekt ist auch für die Zusammenarbeit mit Agents gebaut. Wenn du Claude, Copilot oder ähnliches einsetzt: zeige es auf [AGENTS.md](https://github.com/amusleh-spotware-com/cmind/blob/main/AGENTS.md), lass es die verschachtelten `CLAUDE.md`-Dateien lesen und halte es zum gleichen Standard (Tests, null Warnungen, DDD). Eine gute KI-PR unterscheidet sich nicht von einer guten Human-PR – gleiche Review, gleiche Willkommenskultur.

## Seid zueinander freundlich

Wir haben einen [Verhaltenskodex](https://github.com/amusleh-spotware-com/cmind/blob/main/CODE_OF_CONDUCT.md). Kurz gesagt: sei nett, gehe von gutem Willen aus und denk daran, dass auf der anderen Seite eine Person (oder ihr Agent) sitzt. Stelle Fragen früh – das ist eine Stärke, keine Störung.

Willkommen an Bord. Wir freuen uns darauf zu sehen, was du baust. 🎉
