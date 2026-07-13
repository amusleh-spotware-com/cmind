---
slug: /for-cloud-providers
title: cMind felhő és VPS-szolgáltatók számára
description: Miért felhő vagy VPS-szolgáltatónak kell felügyelt cMind-üzemeltetést kínálni — egy kész, megkülönböztetett termék az algo-kereskedőknek, brókereknek és prop-cégeknek, egyértelmű módokkal a számítás, fehér címke újra-értékesítés és felügyelt AI pénzszerzésére.
keywords:
  - felügyelt üzemeltetés
  - VPS-szolgáltató
  - felhő-szolgáltató
  - kereskedelmi platform üzemeltetése
  - fehér címke újra-értékesítő
  - felügyelt AI-üzemeltetés
sidebar_position: 7
---

# cMind felhő és VPS-szolgáltatók számára 🖥️

Ön már számítást bérel. A cMind egy kész, nyílt forráskódú termék, amelyet az adott számítás körül csomagolhat: **felügyelt cMind-üzemeltetést kínálnak**, és egy magas értékű, ragasztós, számítás-éhes terhelés hozzák — algoritmikus kereskedők, brókerek, prop-cégek és kereskedelmi közösségek, akik a platform futtatása nélkül az operációs csapatnak nem akarnak lenni.

:::tip TL;DR
Futtassa az állapot nélküli szintet + Postgres + egy node-flottát; adjon ügyfeleknek egy márkanem URL-t. A szinlét, a számítást, a fehér címkét és az AI-t pénzszerezheti. → [Telepítés a felhőbe](./deployment/cloud.md)
:::

## Miért kínálják a felügyelt cMind-et

- **Nincs építési költség.** Ez nyílt forráskódú, MIT-licencelt, és már dokumentálva, tesztelt és konténer-ezett. Csomagold meg és működtetd — nem kell építened.
- **Egy megkülönböztetett termék egy lucratív rés.** Az algo-kereskedés számítás-éhes: a backtestjek és az élő node-ok éget a CPU-hoz, amely *számlázható használat*, amit már értékesítesz.
- **Ragasztós ügyfelek.** A kereskedők, akik stratégiákat építenek és futtatnak a platformon belül, nem ingadoznak casually.
- **Fordítson egy óvatosságot egy felfelé-eladásra.** A cMind tervből önmeghatározott — az olyan ügyfeleknek, akik "nem akarnak az operációs csapat lenni," *te* vagy a válasz.

## Ki vesz fel felügyelt cMind-et tőled

- **Egyéni quant-ok & kereskedők**, akik azt üzemeltetni szeretnék. → [Kereskedőknek](./for-traders.md)
- **cTrader-brókerek** az ügyfeleikhez fehér címke futtatnak. → [Brókereknek](./for-brokers.md)
- **Prop-cégek & másolási kereskedés vállalkozások**, akiknek márkanem, auditálható infrastruktúra szükséges.

## Mit jelent a "felügyelt cMind" futtatása

Ön három szintet működtet; az ügyfél egy márkanem webes URL-t kap:

| Szint | Mi ez | Hol fut |
|---|---|---|
| Állapot nélküli (Web + MCP) | Az alkalmazás + API + MCP szerver | Bármilyen konténer-platform, autoscaled |
| Adatbázis | PostgreSQL | Felügyelt Postgres (RDS / Rugalmas szerver / saját) |
| Node-flotta | cTrader-konténereket építenek és futtat | **VM-ek vagy Kubernetes — szükséges jogosult Docker** |

:::warning Egy dolog az cím elöl
A Node-ügynökök cTrader-konténereket építenek és futtatnak, így **jogosult Docker**-hez szükségük van. Ez kiszűri a kiszolgáló nélküli konténer-futási megoldások (Azure Container Apps, AWS Fargate) *az ügynökök számára* — futtassuk az [Kubernetes](./deployment/kubernetes.md), VM vagy EC2-n. Az állapot nélküli szint mindenhol futhat.
:::

Valódi, másolás-beillesztés telepítési útmutatók ezt konkrét: [felhő áttekintés](./deployment/cloud.md) · [Azure](./deployment/cloud-azure.md) · [AWS](./deployment/cloud-aws.md) · [Kubernetes](./deployment/kubernetes.md) · [Méretezés](./deployment/scaling.md).

## Hogyan pénzszerez

- **Felügyelt üzemeltetési szinlét.** Havi indító / csapat / üzlet terv node-flotta és backtest egyidejűségről. 
- **Használat & számítás métering.** Számlázza a backtest-órát, az élő-node-órát és a tárolást — természetesen a konténer-flotta mérési által, amit már futtat.
- **Fehér címke újra-értékesítő szintek.** Töltsenek többet egy teljes átmárkaépítésért (logó, színek, PWA, `ShowSiteLink=false`) és a prémium képességek engedélyezésére a [funkcióváltógombokkal](./features/feature-toggles.md). → [Fehér címke](./features/white-label.md)
- **Felügyelt AI.** Csomagoljon egy alapértelmezett AI-szolgáltatói kulcsot, így minden ügyfél felhasználói AI-t kapnak beállítás nélkül, és jelöljük fel a használatot — vagy kínáljon hozd-a-saját-kulcsot. → [AI funkció](./features/ai.md)
- **Prop-firm & másolási kereskedés bevételi megosztás.** Üzemi cégeket futtasson kihívások és teljesítmény-díjak és veszenek egy platform-vágást. → [Prop-firm](./features/prop-firm.md) · [Teljesítmény-díjak](./features/copy-performance-fees.md) · [Szolgáltatói piac](./features/copy-provider-marketplace.md)
- **Beállítás, onboarding & SLA.** Csatoljon a szakmai szolgáltatások és prémium támogat.

## Multi-bérlő minták

- **Telepítés-per-bérlő (ajánlott).** Egy márkanem instancia ügyfélenkénti — erős izolálás, per-bérlő márkaépítés és adatbázis, egy megkülönböztetett node-csatlakozási token per bérlő. A márkaépítés az `IOptionsMonitor`-ből olvasódik, így az egyes instancia-szállító a saját identitásának.
  → [Multi-bérlő márkaépítés](./white-label-for-business.md#multi-tenant-per-customer-branding) · [Node-felderítés](./operations/node-discovery.md)
- **Megosztott kontrolláló sík (fejlett).** Sok instanciát vezet az Ön saját kiépítési rétegéről, márkaépítés és funkciók bérlőenkénti programozottan vetésével.

## Mérje a használatot a számlázáshoz

Az egyéni/admin-csak **`GET /api/usage`** végpont a egy olvasható összefoglalót ad vissza, amelyet a szolgáltató szavazhat és számlázhat — anélkül, hogy egy új tartomány vagy kitartás nélkül vetítse a meglévő állapot:

```json
{
  "users": { "total": 42 },
  "nodes": { "total": 6, "online": 5 },
  "instances": { "total": 1280, "backtestsRunning": 3, "runsRunning": 11 },
  "cbots": { "total": 210 },
  "tradingAccounts": { "total": 88 }
}
```

Szavazza meg az per-bérlő-telepítést az ülés-alapú, flotta-alapú vagy terhelés-alapú árazás meghajtásához. Párosítsd a [naplózás & megfigyelhető](./operations/logging.md) finomabb számítás-méréséhez.

## A szélességek megjósolhatóságának megtartása

A node-okat a kereslet felé skálázza, a Postgres-szinteket megosztja, és az állapot nélküli szintet autoscale-be. Az operációs felületek, amelyekre szüksége van:

- [Méretezés & öngyógyulás](./deployment/scaling.md)
- [Naplózás & megfigyelhető](./operations/logging.md)
- [Biztonsági mentés & helyreállítás](./operations/backup-recovery.md)

## Kezdjük el

1. Az [felhő útmutatók](./deployment/cloud.md) alapján egy hivatkozási telepítést állítson fel.
2. Sablonozza meg bérlőenkénti (márkaépítés + csatlakozási token + DB) és drótja a számlázást a számítás-használatra.
3. Soroljon fel — most egy felügyelt algo-kereskedelmi platformot lehet eladni.

## Járuljon vissza

A cMind-et nagy léptékben futtatott szolgáltatók az éles éleket először találják meg. Az operációs javítások és az IaC-fejlesztések felsőbb adatfolyama az Ön flottyáját olcsóan tartja karbantartva — kezdje az [Hozzájárulás útmutatóval](./contributing.md).
