---
description: "Per-copy execution facts — latencia, realizovaný slippage, fill vs zlyhanie — zachytené pri každom pokuse o kopírovanie, surfacované ako per-profile transparency report. Vypnuté predvolene…"
---

# Copy execution transparency (Fáza 3)

Per-copy execution facts — latencia, realizovaný slippage, fill vs zlyhanie — zachytené pri každom
pokuse o kopírovanie, surfacované ako per-profile transparency report. **Vypnuté predvolene**; povolte s
`App:Copy:TransparencyEnabled=true`. Keď vypnuté, copy engine bajt-po-bajte nezmenený: host emituje
do no-op sink, nič napísané.

## Ako to funguje

```
CopyEngineHost ──Record(fact)──▶ ICopyEventSink
                                   │
             (transparency off) NullCopyEventSink   → zahadzuje (predvolené; nulové hot-path náklady)
             (transparency on)  ChannelCopyEventSink → bounded in-memory channel (DropOldest)
                                   │
                                   ▼
                          CopyExecutionDrainer (BackgroundService)
                                   │  batchuje každý App drain interval
                                   ▼
                          CopyExecution append-only table  ◀── GET /api/copy/profiles/{id}/transparency
```

- **Hot path zostáva bez I/O.** Host volá `ICopyEventSink.Record(...)` — non-blocking,
  nikdy-nehodí enqueue. Nikdy nečaká, nikdy sa nedotýka DB, nikdy neblokuje order execution.
- **Strata preferovaná pred back-pressure.** Channel bounded (`CopyExecutionChannelCapacity`) s
  `DropOldest`: ak DB drainer stojí, *najstaršie* transparency riadky dropnuté namiesto oneskorenia
  copy. Transparency = best-effort telemetria, nie trading dependency.
- **Out-of-band perzistencia.** `CopyExecutionDrainer` drainuje channel v batchoch
  (`CopyExecutionDrainBatchSize`) na `CopyExecutionDrainInterval`, píše `CopyExecution` riadky cez
  scoped `DataContext`. Finálny flush pri shutdown.
- **Fakty, nie príkazy.** `CopyExecution` = append-only log (ako `InstanceLog`/`AuditLog`), nie
  aggregate. Read model sa pýta priamo (CQRS-lite), aggregates in-memory.

## Čo sa zaznamenáva

Jeden `CopyExecutionRecord` na pokus o kopírovanie na jednej destinácii:

| Kind | Kedy | Nesie |
|------|------|-------|
| `Opened` | copy order umiestnená | symbol, side, wire volume, master price, realizovaný slippage (points), latencia (ms) |
| `Failed` | copy open hodila/odmietnutá | symbol, side, master volume/price, latencia, dôvod zlyhania (exception type) |

(`Closed`/`Skipped`/`Reconciled` existujú v enum pre budúcu expanziu.)

## Report

`GET /api/copy/profiles/{id}/transparency` (owner-scoped) vráti, za najnovších 500 faktov:

- **Summary** — total, opened, failed, **fill rate**, **priemerná latencia (ms)**, **priemerný slippage (points)**.
- **Recent** — raw recent facts (destination, source position, symbol, side, volume, master price,
  slippage, latencia, dôvod, timestamp).

## Konfigurácia (`App:Copy`)

| Nastavenie | Predvolené | Efekt |
|---------|---------|--------|
| `TransparencyEnabled` | `false` | Zapne per-copy fact capture + drainer pre node. |

Channel capacity, drain batch size, drain interval = `CopyDefaults` konštanty
(`CopyExecutionChannelCapacity` / `CopyExecutionDrainBatchSize` / `CopyExecutionDrainInterval`).

## Testy

- **Jednotka** (`CopyTransparencyTests`) — úspešný open emituje `Opened` fact so správnym
  symbol/side/volume/latencia; odmietnutý open emituje `Failed` fact s dôvodom. Cez capturing sink.
- **Integrácia** (`CopyExecutionDrainerTests`, reálny Postgres) — drainer perzistuje buffered facts do
  `CopyExecution` log; prázdny sink nič nepíše.
- **DST** — host fire-and-forget s no-op default sink, takže deterministic copy stress
  suite zostáva zelená (23/23).
