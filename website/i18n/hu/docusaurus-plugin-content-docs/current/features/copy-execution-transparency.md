---
description: "Per-copy veglegesitesi tenyek - latencia, realizalt csuszás, fill vs hiba - elkapva minden masolasi probalkozasnal, felszínre hozva per-profil atlasshato sági jelentest. Alapertelmezes szerint ki."
---

# Masolasi vegheritesi atlasshatóság (3. fazis)

Per-copy vegheritesi tenyek - latencia, realizalt csuszás, fill vs hiba - elkapva minden masolasi probalkozasnal, felszinre hozva per-profil atlasshato sagi jelentest. **Alapertelmezes szerint ki**; engedelyezd `App:Copy:TransparencyEnabled=true`-val. Ki kapcsolva, a masolo motor byte-Byte változatlan marad: a host no-op sink-be bocsát, semmi sin irva.

## Hogyan mukodik

```
CopyEngineHost ──Record(fact)──▶ ICopyEventSink
                                   │
             (transparency off) NullCopyEventSink   → eldob (alapertelmezes; zero hot-path koltseg)
             (transparency on)  ChannelCopyEventSink → korlatos in-memory csatorna (DropOldest)
                                   │
                                   ▼
                          CopyExecutionDrainer (BackgroundService)
                                   │  batch-eli minden App drain intervallumot
                                   ▼
                          CopyExecution append-only tabla  ◀── GET /api/copy/profiles/{id}/transparency
```

- **A hot path szabad I/O-tol.** A host meghivja `ICopyEventSink.Record(...)` - nem blokkoló, soha-nem-dobó enqueue. Sosem await-el, sosem érinti a DB-t, sosem blokkolja a megbízás végrehajtást.
- **A loss preferalando a back-pressure helyett.** A csatorna korlatos (`CopyExecutionChannelCapacity`) `DropOldest`-tel: ha a DB drainer akad, a *leggyobb* átláthatósági sorok eldobásra kerülnek, nehogy késleltessen egy másolást. Átláthatóság = best-effort telemetria, nem kereskedési függőség.
- **Out-of-band perzisztencia.** `CopyExecutionDrainer` batch-eli a csatornát (`CopyExecutionDrainBatchSize`) a `CopyExecutionDrainInterval`-en, ir `CopyExecution` sorokat a scoped `DataContext`-en keresztül. Végső flush leállításkor.
- **Tenyek, nem parancsok.** `CopyExecution` = append-only log (mint `InstanceLog`/`AuditLog`), nem agregatum. A read model közvetlenül kérdezi (CQRS-lite), agregatumok a memóriában.

## Mi van rögzítve

Egy `CopyExecutionRecord` per masolási probálkozás egy célponton:

| Fajta | Mikor | Hordozza |
|------|------|---------|
| `Opened` | masolasi megbizás elhelyezve | szimbulum, oldal, wire volumen, master ár, realizalt csuszás (pontokban), latencia (ms) |
| `Failed` | masolasi nyitás dob/ellened | szimbulum, oldal, master volume/ár, latencia, hiba oka (kivétel tipus) |

(`Closed`/`Skipped`/`Reconciled` létezik az enum-ban jövőbeli bővítésre.)

## A jelentés

`GET /api/copy/profiles/{id}/transparency` (tulajdonos-scoped) a legutóbbi 500 tényből adja vissza:

- **Összefoglaló** - osszes, opened, failed, **fill rate**, **atlagos latencia (ms)**, **atlagos csuszás (pontokban)**.
- **Legutóbbi** - nyers legutóbbi tenyek (cel, forras pozicio, szimbulum, oldal, volumen, master ár, csuszás, latencia, ok, idobelyeg).

## Konfiguracio (`App:Copy`)

| Beallitas | Alapertelmezes | Hatas |
|---------|---------|---------|
| `TransparencyEnabled` | `false` | Kapcsolja be per-copy teny elkapast + drainer-t a csomoponthoz. |

Csatorna kapacitás, drain batch meret, drain intervallum = `CopyDefaults` konstansok (`CopyExecutionChannelCapacity` / `CopyExecutionDrainBatchSize` / `CopyExecutionDrainInterval`).

## Tesztek

- **Unit** (`CopyTransparencyTests`) - sikeres nyitás kibocsát `Opened` tenyet a jo szimbulum/oldal/volume/latencia-val; elutasított nyitás kibocsát `Failed` tenyet a okkal. Meghajtva capturing sink-en at.
- **Integracio** (`CopyExecutionDrainerTests`, valódi Postgres) - drainer perzisztalja a pufferelt tenyeket a `CopyExecution` log-ba; ures sink nem ir semmit.
- **DST** - host change fire-and-forget no-op default sink-szel, igy a determinisztikus masolasi stress suite szinten zold (23/23).
