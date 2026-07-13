---
description: "Činjenice po izvršenju kopiranja — latencija, ostvareno proklizanje, popunjavanje naspram neuspeha — hvataju se pri svakom pokušaju kopiranja, prikazuju se kao izveštaj o transparentnosti po profilu. Podrazumevano isključeno…"
---

# Transparentnost izvršenja kopiranja (Faza 3)

Činjenice po izvršenju kopiranja — latencija, ostvareno proklizanje, popunjavanje naspram neuspeha — hvataju se pri svakom pokušaju kopiranja,
prikazuju se kao izveštaj o transparentnosti po profilu. **Podrazumevano isključeno**; omogućite sa
`App:Copy:TransparencyEnabled=true`. Kada je isključeno, copy engine doslovno nepromenjen: host emituje
na no-op sink, ništa ne piše.

## Kako funkcioniše

```
CopyEngineHost ──Record(fact)──▶ ICopyEventSink
                                   │
             (transparentnost isključena) NullCopyEventSink   → odbacuje (podrazumevano; nula hot-path troška)
             (transparentnost uključena)  ChannelCopyEventSink → ograničen in-memory kanal (DropOldest)
                                   │
                                   ▼
                          CopyExecutionDrainer (BackgroundService)
                                   │  batch-uje svaki App interval pražnjenja
                                   ▼
                          CopyExecution append-only tabela  ◀── GET /api/copy/profiles/{id}/transparency
```

- **Hot path ostaje bez I/O.** Host poziva `ICopyEventSink.Record(...)` — ne-blokirajuće,
  nikad-ne-baca enqueue. Nikad ne čeka, ne dodiruje DB, ne blokira izvršenje narudžbine.
- **Gubitak preferiran nad back-pressure.** Kanal je ograničen (`CopyExecutionChannelCapacity`) sa
  `DropOldest`: ako DB drainer stane, *najstariji* redovi transparentnosti se odbacuju pre nego što se
  kopiranje uspori. Transparentnost = best-effort telemetrija, ne zavisnost trgovanja.
- **Out-of-band perzistencija.** `CopyExecutionDrainer` prazni kanal u batch-ovima
  (`CopyExecutionDrainBatchSize`) na `CopyExecutionDrainInterval`, piše `CopyExecution` redove kroz
  scoped `DataContext`. Final flush na gašenju.
- **Činjenice, ne komande.** `CopyExecution` = append-only log (kao `InstanceLog`/`AuditLog`), ne
  agregat. Read model direktno pita (CQRS-lite), agregati u memoriji.

## Šta se beleži

Jedan `CopyExecutionRecord` po pokušaju kopiranja na jednoj destinaciji:

| Vrsta | Kada | Nosi |
|------|------|---------|
| `Opened` | copy narudžbina postavljena | simbol, strana, wire volume, master cena, ostvareno proklizanje (poeni), latencija (ms) |
| `Failed` | copy open bacio/odbio | simbol, strana, master volume/cena, latencija, razlog neuspeha (tip izuzetka) |

(`Closed`/`Skipped`/`Reconciled` postoje u enum-u za buduću ekspanziju.)

## Izveštaj

`GET /api/copy/profiles/{id}/transparency` (owner-scoped) vraća, preko poslednjih 500 činjenica:

- **Rezime** — ukupno, otvoreno, neuspelo, **stopa popunjavanja**, **prosečna latencija (ms)**, **prosečno proklizanje (poeni)**.
- **Skorašnje** — sirove skorašnje činjenice (destinacija, izvorna pozicija, simbol, strana, volume, master cena,
  proklizanje, latencija, razlog, timestamp).

## Konfiguracija (`App:Copy`)

| Postavka | Podrazumevano | Efekat |
|---------|---------|--------|
| `TransparencyEnabled` | `false` | Uključi činjenice po-copy + drainer za čvor. |

Kapacitet kanala, veličina batch-a pražnjenja, interval pražnjenja = `CopyDefaults` konstante
(`CopyExecutionChannelCapacity` / `CopyExecutionDrainBatchSize` / `CopyExecutionDrainInterval`).

## Testovi

- **Unit** (`CopyTransparencyTests`) — uspešan open emituje `Opened` činjenicu sa pravim
  simbolom/stranom/volume/latencijom; odbijen open emituje `Failed` činjenicu sa razlogom. Vođeno kroz
  capturing sink.
- **Integration** (`CopyExecutionDrainerTests`, real Postgres) — drainer perzistira baferovane činjenice u
  `CopyExecution` log; prazan sink ne piše ništa.
- **DST** — host change fire-and-forget sa no-op podrazumevanim sink-om, tako da deterministic copy stress
  suite ostaje zelena (23/23).
