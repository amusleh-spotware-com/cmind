---
description: "Pro Copy-Ausführungsfakten — Latenz, realisierter Slippage, Füllung vs. Misserfolg — erfasst bei jedem Copy-Versuch, dargestellt als Pro-Profil-Transparenzbericht. Ausgeschaltet von…"
---

# Copy-Ausführungstransparenz (Phase 3)

Pro-Copy-Ausführungsfakten — Latenz, realisierter Slippage, Füllung vs. Misserfolg — erfasst bei jedem Copy-Versuch, dargestellt als Pro-Profil-Transparenzbericht. **Standard aus**; aktivieren mit `App:Copy:TransparencyEnabled=true`. Wenn aus, Copy-Engine Byte-für-Byte unverändert: Host emittiert zu no-op Sink, nichts geschrieben.

## Wie es funktioniert

```
CopyEngineHost ──Record(fact)──▶ ICopyEventSink
                                   │
             (transparency off) NullCopyEventSink   → verworfen (Standard; Nullkosten für heißer Pfad)
             (transparency on)  ChannelCopyEventSink → gebundener In-Memory-Kanal (DropOldest)
                                   │
                                   ▼
                          CopyExecutionDrainer (BackgroundService)
                                   │  batches jedem App drain interval
                                   ▼
                          CopyExecution append-only Tabelle  ◀── GET /api/copy/profiles/{id}/transparency
```

- **Heißer Pfad bleibt frei von I/O.** Host ruft `ICopyEventSink.Record(...)` auf — nicht-blockierend, niemals-wurf Enqueue. Nie wartet, berührt nie DB, blockiert nie Bestellausführung.
- **Verlust bevorzugt über Gegendruckl.** Kanal gebunden (`CopyExecutionChannelCapacity`) mit `DropOldest`: wenn DB-Drainer steckenbleibt, *älteste* Transparenzreihen gelöscht anstatt Copy zu verzögern. Transparenz = Best-Effort-Telemetrie, nicht Trading-Abhängigkeit.
- **Out-of-Band-Persistenz.** `CopyExecutionDrainer` entleert Kanal in Batches (`CopyExecutionDrainBatchSize`) auf `CopyExecutionDrainInterval`, schreibt `CopyExecution`-Reihen über Scope `DataContext`. Endleerung beim Herunterfahren.
- **Fakten, nicht Befehle.** `CopyExecution` = append-only Log (wie `InstanceLog`/`AuditLog`), nicht Aggregate. Lese-Modell-Anfragen direkt (CQRS-lite), Aggregate im Speicher.

## Was wird aufgezeichnet

Ein `CopyExecutionRecord` pro Copy-Versuch auf einem Ziel:

| Art | Wann | Trägt |
|------|------|---------|
| `Opened` | Copy-Bestellung platziert | Symbol, Seite, Draht-Volumen, Master-Preis, realisierter Slippage (Pips), Latenz (ms) |
| `Failed` | Copy-Open warf/lehnte ab | Symbol, Seite, Master-Volumen/Preis, Latenz, Fehlergrund (Ausnahmengtyp) |

(`Closed`/`Skipped`/`Reconciled` existieren in Enum für zukünftige Erweiterung.)

## Der Bericht

`GET /api/copy/profiles/{id}/transparency` (Owner-Umfang) gibt über die meisten letzten 500 Fakten zurück:

- **Zusammenfassung** — gesamt, geöffnet, fehlgeschlagen, **Füllrate**, **durchschnittliche Latenz (ms)**, **durchschnittlicher Slippage (Pips)**.
- **Aktuell** — unbearbeitete aktuelle Fakten (Ziel, Quellposition, Symbol, Seite, Volumen, Master-Preis, Slippage, Latenz, Grund, Zeitstempel).

## Konfiguration (`App:Copy`)

| Einstellung | Standard | Effekt |
|---------|---------|--------|
| `TransparencyEnabled` | `false` | Aktivieren Sie Pro-Copy-Fakten-Erfassung + Drainer für Knoten. |

Kanal-Kapazität, Drain-Batch-Größe, Drain-Intervall = `CopyDefaults` Konstanten (`CopyExecutionChannelCapacity` / `CopyExecutionDrainBatchSize` / `CopyExecutionDrainInterval`).

## Tests

- **Unit** (`CopyTransparencyTests`) — erfolgreiche Open emittiert `Opened` Fakt mit richtigem Symbol/Seite/Volumen/Latenz; abgelehnte Open emittiert `Failed` Fakt mit Grund. Getrieben durch erfassenden Sink.
- **Integration** (`CopyExecutionDrainerTests`, real Postgres) — Drainer persistiert gepufferte Fakten zu `CopyExecution` Log; leerer Sink schreibt nichts.
- **DST** — Host-Änderung Fire-and-Forget mit no-op Standard-Sink, daher bleiben deterministische Copy-Stress-Suite grün (23/23).
