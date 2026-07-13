---
description: "Stress suite. Valja dele aplikacije katerih napaka stane uporabnikom denarja — predvsem copy trading — s sovražnimi, randomiziranimi, fault-injektiranimi delovnimi obremenitvami. Trdi da sistem ostane correct."
---

# Stress testiranje

Stress suite. Valja dele aplikacije katerih napaka stane uporabnikom denarja — predvsem **copy trading** — s sovražnimi, randomiziranimi, fault-injektiranimi delovnimi obremenitvami. Trdi da sistem ostane correct. Živi v `tests/StressTests`, teče v normalnem `dotnet test` zelenem vratih.

## Pristop — Deterministic Simulation Testing (DST)

Najboljši način stress distributed financial systems = **deterministic simulation testing**, po TigerBeetle, FoundationDB, Antithesis: zaženi realno logiko proti *simuliranemu* svetu, poganja z **sejanim** naključnim delom + vbrizganimi napakami, trdi invariante na tišini. Vse sejano + deterministično → katera koli napaka reproducira natančno iz semena. V kombinaciji z:

- **Chaos-inženiring vbrizgavanje napak** (Netflix Chaos Monkey stil) — padci povezav, zavrnitve naročil, rotacija žetonov, smrt vozlišča.
- **Lastnostno-bazirane invariante** — ne trdi natančno zaporedij klicev; trdi lastnosti ki morajo veljati ne glede na to kako se dogodki prepletajo (konvergenca, brez sirot, največ en holder lease).

Aplikacija že ladi popoln DST model sveta: `FakeTradingSession`, cTrader-veren in-memory Open API seja. Stress suite ga znova uporablja (linked, en sam vir resnice) ne mock, torej simuliran broker obnaša se kot realen.

## Kaj pokriva

### Copy trading (primarni fokus)

Poganja prek `CopyDstWorld` (`tests/StressTests/CopyTrading/`), zažene live `CopyEngineHost` proti fake seji, izda membership-konsistent source workload:

| Scenarij | Stresa |
|---|---|
| `Mass_fan_out…` | 1 vir → 80 ciljev, 150 odprtij nato zaprtij; polni fan-out + izprazni |
| `High_frequency_open_close…` | 300 hitri prepleteni open/close; brez puščanja pozicij |
| `Partial_close_and_scale_in_storm…` | delno-zaprtje + scale-in šarža; stabilnost nabora oznak |
| `Connection_flap_storm…` | ponavljajoči se socket disconnect/reconnect + sred-leta desync; resync konvergenca |
| `Order_rejection_cascade…` | podnabor zavrne vsako naročilo; zdravi cilji neprizadeti, nato samozdravijo prek resync |
| `Token_rotation_storm…` | hitre in-place zamenjave žetonov med šaržo naročil |
| `Randomized_chaos_workload…` (10 seeds) | **DST jedro** — vsak tip dogodka + vsaka napaka prepleteni nepredvidljivo |
| `CopyLeaseReclaimStressTests` | smrt vozlišča + lease reclaim čez skalirano gručo (čista domena, `FakeTimeProvider`) |

**Konvergenčna invarianta.** V mirovanju, vsak zdrav cilj zrcali natančno nabor še odprtih izvornih pozicij — brez sirot, nobene manjkajoče. Trjeno na naboru oznak (scale-in legitimno odpre drugo ciljevo pozicijo pod isto izvorno oznako, torej podvojene oznake pričakovane). Cilj ki trenutno zavrača naročila lahko zaostaja, uskladi se ko ozdravi.

**Lease invarianta.** V gruči kjer vozlišča odmrejo + poživijo po sejanem razporedu, največ en vozlišče kadarkoli drži veljaven lease na profil; mrtvo vozliščevo lease mine natančno ob poteku, prevzeto; zdrava gruča se umiri z vsakim profilom ki ga drži natančno eno vozlišče. Zrcali `CopyEngineSupervisor`'s claim predikat proti `CopyProfile` domeni lease metode.

## Varnost niti nadzorne naprave

`FakeTradingSession` enovijačna; stress delovna obremenitev jo mutira iz testne niti medtem ko host bere/pisje iz svoje zanke. `SyncTradingSession` jo ovije, naredi vsako operacijo seje atomsko na enem zaklepu (brez držanja zaklepa čez reconnect callback — obrnilo bi vrstni red zaklepa glede na hostov `_stateGate` in povzročilo mrtv zaklep). Simulator sam ostane nedotaknjeno.

## Najdene hrošče

- **Start-up resync race v `CopyEngineHost`.** `OnReconnected` napeljan preden initial reference-load + prvi resync, ki je tekel brez `_stateGate`. Socket flap med start-up je lahko tekel drugi resync sočasno in pokvaril hostove ne-concurrent state rečnike (`_symbolDetails`, `_sourceVolumes`). Popravljeno: start-up load + prvi resync zdaj tečeta pod zaklepom. Produkcijska race, ne test artifakti — DST chaos workload jo je razkril.

## Zagon

```bash
dotnet test tests/StressTests/StressTests.csproj
```

Suite je **serijaliziran** (`[assembly: CollectionBehavior(DisableTestParallelization = true)]`): vsak test zavrti live host background zanko, poganja do tišine pod realno uro, torej paralelen zagon strada host opravil in naredi konvergencočasovne preskoke flaky. Delovne obremenitve dimenzionirane da končajo v sekundah torej suite ostane v privzetem zelenem vratih. Neuspeh natisne njegovo seme; znova zaženi to seme da reproduciraš natančno prepletanje.

## Razširjanje

- Novo copy vedenje → dodaj source op v `CopyDstWorld` (ohrani source book membership konsistentno z event stream) + utežen primer v `CopyChaosDstTests`. Če lahko ustvari ali upokoji ciljevo pozicijo, poskrbi da konvergenčna invarianta še vedno drži.
- Nova napaka → dodaj injector v `CopyDstWorld` (delegiraj na `FakeTradingSession`'s control surface prek `SyncTradingSession`) + vadi v poimenovanem scenariju plus chaos mešanici.
- Ohrani simulator cTrader-veren (glej koren `CLAUDE.md` mandat); nikoli ne oslabi da bi stress test potekel.
