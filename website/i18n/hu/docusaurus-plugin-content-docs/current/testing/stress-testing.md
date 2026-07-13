---
description: "Stressz suite. Kalapálja az alkalmazás azon részeit, amelyeknek a meghibásodása felhasználók pénzébe kerül — főként a copy trading — ellenséges, randomizált, fault-injektált munkaterhelésekkel. Az invariánsok fennmaradását állítja."
---

# Stressz tesztelés

Stressz suite. Kalapálja az alkalmazás azon részeit, amelyeknek a meghibásodása felhasználók pénzébe kerül — főként a **copy trading** — ellenséges, randomizált, fault-injektált munkaterhelésekkel. Azt állítja, hogy a rendszer helyes marad. A `tests/StressTests`-ben él, a normál `dotnet test` green gate-ben fut.

## Megközelítés — Determinisztikus Szimulációs Tesztelés (DST)

A legjobb mód a persely pénzügyi rendszerek stresszelésére = **determinisztikus szimulációs tesztelés**, per TigerBeetle, FoundationDB, Antithesis: valódi logikát futtatni egy *szimulált* világ ellen, meghajtva **seedelt** random munkaterheléssel + injektált hibákkal, invariánsokat assert-elni quiescence-kor. Minden seedelt + determinisztikus → bármely hiba reprodukálható a seed-ből. Kombinálva:

- **Chaos-engineering fault injection** (Netflix Chaos Monkey stílus) — kapcsolat drop-ok, order elutasítások, token rotáció, node halál.
- **Property-based invariánsok** — nem exact hívási sorozatokat assert; olyan propertyket állít, amelyeknek fenn kell maradniuk, bármennyire is előre nem láthatóan interszekálódnak az események (konvergencia, nincs orphan, legfeljebb egy lease holder).

Az alkalmazás már tökéletes DST világmodellt szállít: `FakeTradingSession`, cTrader-hű in-memory Open API session. A stressz suite újrafelhasználja (linked, single source of truth) nem mock, így a szimulált broker úgy viselkedik, mint a valódi.

## Mit fedez fel

### Copy trading (elsődleges fókusz)

Meghajtva a `CopyDstWorld`-ön keresztül (`tests/StressTests/CopyTrading/`), valódi `CopyEngineHost`-ot futtat fake session ellen, konzisztens forrás munkaterhelést bocsát ki:

| Scenario | Stresses |
|---|---|
| `Mass_fan_out…` | 1 forrás → 80 cél, 150 nyitás majd zárás; teljes fan-out + drain |
| `High_frequency_open_close…` | 300 gyors interleaved open/close; nincs kiszivárgott pozíció |
| `Partial_close_and_scale_in_storm…` | partial-close + scale-in churn; label-set stabilitás |
| `Connection_flap_storm…` | ismételt socket disconnect/reconnect + mid-flight desync; resync konvergencia |
| `Order_rejection_cascade…` | egy részhalmaz minden ordert elutasít; az egészséges célok érintetlenek, majd öngyógyítanak resync-en át |
| `Token_rotation_storm…` | gyors in-place token swap-ek egy order storm alatt |
| `Randomized_chaos_workload…` (10 seeds) | **a DST mag** — minden esemény típus + minden fault előre nem láthatóan interleaving-elve |
| `CopyLeaseReclaimStressTests` | node halál + lease reclaim egy skálázott klaszteren (tiszta domain, `FakeTimeProvider`) |

**Konvergencia invariáns.** Nyugalomban minden egészséges cél pontosan tükrözi a még-nyitott forrás pozíciók halmazát — nincs orphan, semmi nem hiányzik. Assert-elve a label *halmazán* (a scale-in legitim módon megnyit egy második cél pozíciót ugyanazon forrás label alatt, így a duplikált labelek vártak). Az aktuálisan elutasító célok megengedhetik a lag-et, egyeztetve, amint meggyógyult.

**Lease invariáns.** Egy klaszterben, ahol node-ok meghalnak + felelevenednek egy seedelt ütemterven, legfeljebb egy node valaha birtokol érvényes lease-t egy profilon; a halott node lease-je pontosan a lejáratkor lakul el, reclaim-elt; az egészséges klaszter minden profil minden profilja pontosan egy node által birtokolt állapotban settles. Tükrözi a `CopyEngineSupervisor`'s claim predikátumát a `CopyProfile` domain lease metódusai ellen.

### A harness thread-safety-ja

`FakeTradingSession` single-threaded; a stressz munkaterhelés őt módosítja a teszt szálról, miközben a host olvas/ír a saját loop-jából. A `SyncTradingSession` wrap-eli, atomi működést végez minden session műveleten egy kapun (anélkül, hogy a kaput tartaná a reconnect callback-en át — invertálná a lock sorrendet a host `_stateGate`-jével és deadlock-olna). A szimulátor maga érintetlen marad.

## Megtalált hibák

- **`CopyEngineHost`-ban az indítási resync verseny.** Az `OnReconnected` az első referencia-betöltés + első resync előtt volt huzalozva, amely a `_stateGate` nélkül futott. Socket flap az indítás alatt második resync-et futtatott konkurrensen, korruptálva a host nem-konkurrens state dict-jeit (`_symbolDetails`, `_sourceVolumes`). Javítva: az indítási betöltés + első resync a kapun alatt fut. Produkciós versenyhelyzet, nem teszt artifakt — a DST chaos munkaterhelés a felszínre hozta.

## Futtatás

```bash
dotnet test tests/StressTests/StressTests.csproj
```

A suite **serializált** (`[assembly: CollectionBehavior(DisableTestParallelization = true)]`): minden teszt elforgat egy élő host background loop-ot, hajt a quiescence-ig wall clock alatt, így a párhuzamos futtatás megéhezteti a host task-okat és a konvergencia timeout-okat flaksyává teszi. A munkaterhelések másodpercek alatt befejeződnek, így a suite a default green gate-ben marad. A hiba kiprinteli a seed-jét; futtasd újra azt a seed-et a pontos interleaving reprodukálásához.

## Bővítés

- Új másolási viselkedés → add hozzá a forrás op-et a `CopyDstWorld`-höz (tartsd a forrás könyv tagságát konzisztensen az event stream-mel) + súlyozott eset a `CopyChaosDstTests`-ben. Ha létrehozhat vagy nyugdíjazhat egy cél pozíciót, győződj meg, hogy a konvergencia invariáns még mindig fennáll.
- Új hiba → add hozzá az injectort a `CopyDstWorld`-höz (delegate to `FakeTradingSession`'s control surface via `SyncTradingSession`) + gyakorold egy named scenario-ban plusz a chaos mix-ben.
- Tartsd a szimulátort cTrader-hűnek (lásd root `CLAUDE.md` mandátum); soha ne gyengítsd, hogy egy stressz teszt átmenjen.
