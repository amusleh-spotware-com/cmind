---
description: "cMind se skalira z minimalnim naporom operatorja. Dve stateful delovni obremenitvi — izvajanje teka/backtesta, kopiranje — oba uporabljata bazo podatkov kot točko usklajevanja, zato…"
---

# Vodoravna lestvica

cMind se skalira z minimalnim naporom operatorja. Dve stateful delovni obremenitvi — izvajanje teka/backtesta,
kopiranje — oba uporabljata bazo podatkov kot točko usklajevanja, zato dodajanje replik potrebuje
nobenega zunanjega usklajevalca (nobenega ZooKeeper, nobenega vodje volilnega).

## Kopiranje (samo-popravljanje zakupa)

Vsako vozlišče teče `CopyEngineSupervisor` (zaklenjen na `App:Copy:Enabled`). Vsak usklajeni cikel,
nadzornik:

1. **Zahteva** vsak tekoči profil neprideljeni *ali* zakupa-poteklo, v eni atomski `UPDATE` —
   dve tekmovalki nadzorniku nikoli ne zahtevata isti profil, zato profil kopiran s točno enim
   vozliščem (nobenega dvojnega naročila).
2. **Obnovljena** zakupa na profilih, ki jih gostuje.
3. Gostje dodeljeni profili, potisnejo dostop-žeton rotacije na tekoči gostitelj na mestu (nobenega
   pretoka dogodkov-padec).

Vozlišče sesutje → zaustavi obnavljanje; enkrat `App:Copy:LeaseTtl` prosledi, vsako preživelo vozlišče zahteva
svoje profile naslednji cikel, ponovno zasnovana stanja iz uskladitve brez podvajanja trgovanja. **Razširite
ven** = dodajte replike; nedodeljeni/prosti profili izbrani avtomatično.

**Graciozno lestvica-vmes / valjalnik posodobitve (S1)** = na `SIGTERM`, `CopyEngineSupervisor.StopAsync`
**sprosti to vozlišče zakup** (`AssignedNode`/`LeaseExpiresAt` → null) zato preživelec zahteva jih
svoje *zelo naslednje* usklajeni cikel — **ne** po polni `LeaseTtl`. Samo trda sesutje čaka TTL.
Agent kopije `terminationGracePeriodSeconds` (privzeto 30) daje čas sproščanju konča pred
pod ubit.

### Gumbi (`App:Copy`)

| Nastavitev | Privzeto | Opombe |
|---------|---------|-------|
| `Enabled` | `false` | Vklopite gostovanje kopije za vozlišče. |
| `ReconcileInterval` | `30s` | Kako pogosto vozlišče zahteva/obnovlena/usklajevanje. |
| `LeaseTtl` | `120s` | Milost pred tihim vozliščem profile zahtevane. Obdržite nekaj usklajeni intervali torej počas cikel ne povzroči neutemeljeno prenos. |
| `NodeName` | ime stroja | Nastavite jasno, ko dve nadzorniku delita gostitelja. |

Na Kubernetes kopijo nadzorniku teči kot Deployment; nastavite `replicas` na želeno paralelizem. Vsak
pod dobi stabilnega `NodeName` (privzeto: pod ime gostitelja), zato zakupe pripisani na pod. Baza
je edini vir resnice — nobenega lepljive seje, nobenega na-pod stanja za selitev.

**Uravnotežena distribucija (S4):** nastavite `App:Copy:MaxProfilesPerNode` > 0 za omejevanje koliko tečejo
profile vozlišče gostuje. Vsak nadzornik nato zahteva **največ** njegov preostali prostor preko atomske
`FOR UPDATE SKIP LOCKED` omejena zahteva, tako da profili **razširi** čez replike namesto prvi
nadzornik grabbing vsi — nobenega samotnega vroč pod / SPOF. Preskoči-zaklenjen zahteva obdrži "točno eno vozlišče
na profil" garancija (nobeno dvojno-gostovanje) tudi pod hkratnimi zahtevami. `0` (privzeto) =
omejeno (eno vozlišče gostuje vsi, nespremenjeno).

**Pri lestvici (S7/S8):** vsak pod trese usklajeni do 20% `ReconcileInterval`
(`CopyEngineSupervisor.JitteredInterval`) torej N replike ne ogenj zahteva/obnovlena `UPDATE`
sočasno (Postgres grozing-čred). Ko `copyAgent.replicas > 1` grafikon tudi razširi
replike čez vozlišče (`topologySpreadConstraints`) in doda `PodDisruptionBudget` (`minAvailable: 1`)
zato drenaž/nadgradnja nikoli vzame kopijo zmogljivosti na nič.

## Izvajanje teka/backtesta

`NodeScheduler` izbira najmanj-napolnjeno upravičeno vozlišče časti `MaxInstances`; vozlišče agenti daljava
samogostovajo se in udarci srca (`App:Discovery`), `NodeHeartbeatMonitor` označi vozlišče nedosegljiv
ko udarec srca preseže `Discovery:HeartbeatTtl`. Dodajte vozlišče agenti za dodajanje izvedbe zmogljivosti;
mrtva agent usmerita okoli avtomatično.

## Migracije na lestvici-ven / valjajoči vzorec

Vsak Web/MCP replika teči `OwnerSeeder` pri zagonu, ki uporablja EF migracije in seme lastnikom.
Da bi to storiti varno ko N replike začnejo ob hkrati, migracija + seme teči znotraj **Postgres seje
svetovalec zakup** (`MigrationLock.RunExclusiveAsync`, ključ `DatabaseDefaults.MigrationAdvisoryLockKey`):
prvi replika, ki jo pridobi migracije in semena; preostanek blok na zakupi, nato poiščejo migracije
že uporabljeno (brez-op) in lastnik že prisoten. Nobenega ločenega pristopa migracije ali vodje izbora
potrebno. Če dodate prvo-tek seeding, ga postavite **znotraj** isti varovan blok zato je edini-pisec.

## Vozlišče-agent HTTP obstojnost

Glavni vozlišče govori s vsak `CtraderCliNode` agent čez HTTP preko tri namen-razdeliti kliente zato a
flaky vozlišče ali omrežje nikoli korumpirane stanja:

- **preberi** (`status` / `report` / `stats`) — idempotent GETs, ponovno poskušali na prehodni nepravilnosti
