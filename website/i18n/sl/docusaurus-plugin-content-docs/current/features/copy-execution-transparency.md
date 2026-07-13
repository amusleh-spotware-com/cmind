---
description: "Na-kopiranje izvedbe dejstva — zakasnitev, realizacija zdrsa, polni vs napaka — zajeto vsak kopiranje poskuša, izpostavljen kot na-profil prosojnosti poročilo. Izključeno po…"
---

# Kopiranje izvedbe prosojnosti (Faza 3)

Na-kopiranje izvedbe dejstva — zakasnitev, realizacija zdrsa, polni vs napaka — zajeto vsak kopiranje poskuša,
izpostavljen kot na-profil prosojnosti poročilo. **Izključeno privzeto**; omogočiti z
`App:Copy:TransparencyEnabled=true`. Ko je izključeno, kopiranje motor bajt-za-bajt nespremenjeno: gostitelj oddaja
na brez-op umivalnik, nič napisano.

## Kako deluje

```
CopyEngineHost ──Record(fact)──▶ ICopyEventSink
                                   │
             (prosojnosti izključeno) NullCopyEventSink   → zavrženi (privzeto; nič vroči-pot strošek)
             (prosojnosti na)  ChannelCopyEventSink → omejena v-spomin kanal (DropOldest)
                                   │
                                   ▼
                          CopyExecutionDrainer (BackgroundService)
                                   │  serije vsak App odtok interval
                                   ▼
                          CopyExecution dodajanje-samo tabela  ◀── GET /api/copy/profiles/{id}/transparency
```

- **Vroči pot ostane prosti od I/O.** Gostitelj klici `ICopyEventSink.Record(...)` — ne-blokiranje,
  nikoli-metanje enqueue. Nikoli čaka, nikoli dotakne DB, nikoli bloki reda izvedbe.
- **Izguba izbirna preko nazaj-pritiska.** Kanal omejena (`CopyExecutionChannelCapacity`) z
  `DropOldest`: če DB odtok stallis, *najstarejši* prosojnosti vrstice padla precej kot zakasnitev a
  kopiranje. Prosojnost = najboljši-trud telemetrija, ne trgovanja odvisnosti.
- **Izven-pasu trajnost.** `CopyExecutionDrainer` odtoka kanal v serije
  (`CopyExecutionDrainBatchSize`) na `CopyExecutionDrainInterval`, piše `CopyExecution` vrstice skozi
  obsežen `DataContext`. Finalni odtok na zaustavitvi.
- **Dejstva, ne ukazi.** `CopyExecution` = dodajanje-samo dnevnik (kot `InstanceLog`/`AuditLog`), ne
  agregat. Branja model poizvedbe ga neposredno (CQRS-lite), agregati v spomin.

## Kaj je zabeleženo

Ena `CopyExecutionRecord` na kopiranje poskuša na eno odredišče:

| Vrsta | Ko | Nosi |
|------|------|---------|
| `Opened` | kopiranje reda postavljen | simbol, stran, žičana glasnost, glavni cena, realizacija zdrsa (točke), zakasnitev (ms) |
| `Failed` | kopiranje odprto vrzel/zavrnjena | simbol, stran, glavni glasnost/cena, zakasnitev, napaka razlog (izjeme tip) |

(`Closed`/`Skipped`/`Reconciled` obstoj v enum za prihodnosti razširitve.)

## Poročilo
