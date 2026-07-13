---
description: "Fakta o každé kopírované exekuci — latence, realizovaný skluz, vyplnění versus selhání — zaznamenávaná při každém pokusu o kopírování, prezentovaná jako přehled transparentnosti na profil."
---

# Transparentnost kopírovací exekuce (Fáze 3)

Fakta o každé kopírované exekuci — latence, realizovaný skluz, vyplnění versus selhání — jsou zaznamenávána při každém pokusu o kopírování a prezentována jako přehled transparentnosti na profil. **Ve výchozím stavu vypnuto**; povolte pomocí `App:Copy:TransparencyEnabled=true`. Když je vypnuto, kopírovací engine zůstává beze změny: hostitel odesílá do prázdného sinku, nic se nezapisuje.

## Jak to funguje

```
CopyEngineHost ──Record(fact)──▶ ICopyEventSink
                                   │
             (transparentnost vypnuta) NullCopyEventSink   → zahazuje (výchozí; nulové náklady na hot-path)
             (transparentnost zapnuta)  ChannelCopyEventSink → vázaný in-memory kanál (DropOldest)
                                   │
                                   ▼
                          CopyExecutionDrainer (BackgroundService)
                                   │  dávkuje každý App interval vyprazdňování
                                   ▼
                          CopyExecution append-only tabulka  ◀── GET /api/copy/profiles/{id}/transparency
```

- **Hot path zůstává bez I/O.** Hostitel volá `ICopyEventSink.Record(...)` — neblokující,
  nikdy nevyhazuje výjimku při zařazení do fronty. Nikdy nečeká, nedotýká se DB, neblokuje exekuci obchodu.
- **Ztráta upřednostněna před back-pressure.** Kanál je vázaný (`CopyExecutionChannelCapacity`) s
  `DropOldest`: pokud vyprazdňování DB selže, *nejstarší* transparentnostní řádky jsou zahozeny místo zpoždění
  kopírování. Transparentnost = telemetry s nejlepší snahou, ne obchodní závislost.
- **Perzistence mimo pásmo.** `CopyExecutionDrainer` vyprazdňuje kanál v dávkách
  (`CopyExecutionDrainBatchSize`) v intervalu `CopyExecutionDrainInterval`, zapisuje řádky `CopyExecution` přes
  scoped `DataContext`. Finální flush při vypnutí.
- **Fakta, ne příkazy.** `CopyExecution` = append-only log (podobně jako `InstanceLog`/`AuditLog`), ne
  agregát. Read model se dotazuje přímo (CQRS-lite), agregáty v paměti.

## Co se zaznamenává

Jeden `CopyExecutionRecord` na pokus o kopírování na jednom cíli:

| Druh | Kdy | Nese |
|------|------|--------|
| `Opened` | kopírovací příkaz zadán | symbol, směr, wire volume, cena mastera, realizovaný skluz (body), latence (ms) |
| `Failed` | kopírovací otevření vyhodilo/rejektováno | symbol, směr, master volume/cena, latence, důvod selhání (typ výjimky) |

(`Closed`/`Skipped`/`Reconciled` existují v enum pro budoucí rozšíření.)

## Přehled

`GET /api/copy/profiles/{id}/transparency` (v rozsahu vlastníka) vrací, za posledních 500 faktů:

- **Souhrn** — celkem, otevřeno, selhalo, **míra vyplnění**, **průměrná latence (ms)**, **průměrný skluz (body)**.
- **Nedávné** — surová data (cíl, zdrojová pozice, symbol, směr, objem, cena mastera,
  skluz, latence, důvod, časové razítko).

## Konfigurace (`App:Copy`)

| Nastavení | Výchozí | Efekt |
|---------|---------|--------|
| `TransparencyEnabled` | `false` | Zapne zachycování faktů na úrovni kopírování + vyprazdňování pro uzel. |

Kapacita kanálu, velikost dávky vyprazdňování, interval vyprazdňování = `CopyDefaults` konstanty
(`CopyExecutionChannelCapacity` / `CopyExecutionDrainBatchSize` / `CopyExecutionDrainInterval`).

## Testy

- **Unit** (`CopyTransparencyTests`) — úspěšné otevření emituje fakt `Opened` se správným
  symbolem/směrem/objemem/latencí; odmítnuté otevření emituje fakt `Failed` s důvodem. Řízeno přes
  capturing sink.
- **Integration** (`CopyExecutionDrainerTests`, reálné Postgres) — vyprazdňovač perzistuje bufferovaná fakta do
  `CopyExecution` logu; prázdný sink nic nezapisuje.
- **DST** — změna hostitele fire-and-forget s výchozím no-op sink, takže deterministická copy stress
  sada zůstává zelená (23/23).
