---
description: "cTrader cBotok fordítása, futtatása, backtestje (C# és Python, mindkettő .NET) böngészőbeli Monaco IDE-ből, futtatás a hivatalos ghcr.io/spotware/ctrader-console képen."
---

# cBotok fordítása és backtestje

cTrader cBotok fordítása, futtatása, backtestje (C# **és** Python, mindkettő .NET) böngészőbeli Monaco
IDE-ből, futtatás a hivatalos `ghcr.io/spotware/ctrader-console` képen.

## Fordítás

- A **Builder** oldal Monaco szerkesztőt üzemeltet; a `CBotBuilder` ezután fordítja a projektet
  `dotnet build` **eldobható konténerben** (`AppOptions.BuildImage`, munkakönyvtár bind-mount
  a `/work` helyen), így a nem megbízható felhasználó MSBuild céljai nem érhetik el a gazdagépet. A NuGet helyreállítás gyorsítótárazott
  az összeállítások során egy megosztott kötet segítségével. A webes gazdagépnek Docker szoftvercsatorna-hozzáférésre van szüksége.
- A C# + Python kezdősablonok a `src/Nodes/Builder/Templates/` helyen találhatók.

## Futtatás és backtest

- Az **Instances** = TPH állapothierarchia (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Az átmenet helyettesíti az entitást (az id megváltozik),
  a konténer id átvitelre kerül.
- A `NodeScheduler` a legkevésbé terhelést viselő jogosult csomópontot választja; a `ContainerDispatcherFactory` irányít
  a távoli csomópont HTTP ügynökhöz vagy a helyi Docker diszpécserre.
- A befejezési poller-ek egyeztetik a kilépett konténereket (a backtest konténerek
  `--exit-on-stop` segítségével); a jelenlétből meglévő jelentés → befejezett (a `ReportJson` tárolása), hiányzó → sikertelen.
- Az élő konténer naplói az SignalR-n keresztül a böngészőre kerülnek; a backtest tőkeés görbe-je elemezve
  a jelentésből + diagramozva.

## A backtest piaci adatai gyorsítótárazódnak fiókonként

A cTrader Console letölti az előzményi tick/bar adatokat a `--data-dir` könyvtárba. Ez a könyvtár egy
**stabil, állandó gyorsítótár, amely a kereskedési fiók alapján van kulcsozva** (annak fiók száma) — bind-mount a
csomópont lemezéről a saját konténer útvonalán (`/mnt/data`), egy **külön, nem-fészek csatolás** a
felenként-instance munkakönyvtárból. Így minden ugyanazon a fiókon végzett backtest **újrahasznosítja** a már letöltött adatokat
ahelyett, hogy újra letöltené minden futtatásnál. (Korábban az
adatkönyvtár a perinstance munkakönyvtárban lett, amelynek id-je minden futtatásnál megváltozik, ami friss
letöltést kényszerített minden backtestnél.) Az efemer perinstance munkakönyvtár továbbra is tartalmazza az algoritmust, paramétereket, jelszót
és jelentést; a megosztott adatgyorsítótár a csomópont backtest-data használatában számít és a
csomópont-clean művelettel törölhető.

## Backtest beállítások

A **Backtest** párbeszédablak felhasználó által hangolható cTrader Console backtest beállításokat tesz elérhetővé, így soha nem kell
parancssor érinteni:

- **Symbol / Timeframe** — az idősáv a **cTrader minden periódusának legördülő listája** (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1` és a Renko/Range/Heikin időszak), a
  konzol kanonikus esésében, így mindig érvényes `--period` választ.
- **From / To** — a backtest ablak (`--start` / `--end`).
- **Data mode** — a három cTrader mód egyike (`--data-mode`): **Tick adatok** (`tick`, pontos),
  **m1 sávok** (`m1`, gyors), vagy **Nyitási árak csak** (`open`, leggyorsabb).
- **Starting balance** — alapértelmezett `10000` (`--balance`). Egy **0 egyenlege nem helyez el kereskedéseket és a cTrader üres jelentést
  generál, majd erre összeomlik** ("Message expected"), így egy non-zero egyenleg mindig elküldésre kerül.
- **Commission** és **Spread** — `--commission` / `--spread` (spread pontban).

Az adatkönyvtár (`--data-file` / `--data-dir`) az alkalmazás által felügyelt (perfiók gyorsítótár, fentebb lásd),
nem jelenik meg a párbeszédablakban.

## Instance detail oldal

Egy instance megnyitása (`/instance/{id}`) megjeleníti az élő állapotot, naplókat és — egy backtest esetén — az equity
görbét. A **böngésző lap címe** az adott instance-t tükrözi (**cBot neve · faja · szimbóluma**, pl.
`TrendBot · Backtest · EURUSD`), így egy élő futtatás fül és egy backtest fül egyszerre megkülönböztethetők. Egy és ugyanaz
cBot futtatása és backtestje **lineages** néven nyomon követett (egy stabil lineage id az állapotátmenetek között),
így az oldal pontosan egy instance-t követ és soha nem keveri össze egy futtatás adatait egy backtest adataival.

## Instance életciklus vezérlők

Minden instance sor (és annak detail oldala) állapot-helyes vezérlőket tartalmaz. Egy **aktív** instance
**Stop** gombot mutat; egy **terminal** (Stopped / Completed / Failed) **Start (▶)** gombot mutat az újraindítása céljából
ugyanazon cBot, fiók, szimbólum, timeframe, paraméterkészlet és kép felhasználásával (egy futtatás futtatásként indul újra, egy
backtest backtestként). A Stop gomb kattintása „Stopping…" figyelmeztetést mutat és letiltja az ikont,
amíg feloldódik, és egy újonnan létrehozott futtatás azonnal megjelenik a listában — oldal újrabetöltés nélkül.

A konzol naplói **megmaradnak, amikor egy instance leállt** — egy futtatásnál (Stop-nál) és egy
**backtest** (befejezésnél) — így az utolsó futtatás naplói megtekinthetők maradnak a detail oldalon és
a naplósáv segítségével **vágólapra másolva** (Naplók másolása ikon) vagy **letöltve** (Naplók letöltése
ikon), még miután a konténer eltűnt. Mindkettő a teljes konzol naplóra működik, nem csak a
képernyő alján látható végre.

Az **feltöltött** `.algo` sohasem lett fordítva itt, így az **Last Build** oszlopa a cBots oldalon
üres marad (csak az itt böngészőben fordított cBotokhoz mutat fordítási időpontot).

## Leállított instance szerkesztése és újrafuttatása

Egy **leállított** instance (futtatás vagy backtest) tartalmaz egy **Edit** vezérlőt — egy ikon a során a listában **és**
a Start/Stop mellett a detail oldalon — amely egy párbeszédablakot nyit **előre kitöltve** az aktuális konfigurációval.
Módosíthatja a **kereskedési fiók, szimbólum, timeframe, paraméterkészlet és kép címkét** (és backtest-nél a
**ablak és összes backtest beállítást** fentebb), majd **Save & start** újraindítja a
új beállításokkal (az leállított instance helyettesítésével). A vezérlő **le van tiltva, amíg az instance aktív** —
csak egy leállított instance szerkeszthető.

## Futtatás a kódszerkesztőből

A kódszerkesztőben a **Run** gomb kattintása egy párbeszédablakot nyit egy vakon, kemény futtatás helyett:

- **Kereskedési fiók** (szükséges) — a cTrader fiók, amelyhez a cBot csatlakozik.
- **Paraméterkészlet** (opcionális) — válasszon egy meglévő készletet, vagy hagyja üresen, ha futtatni szeretne a cBot-tal
  **alapértelmezett paramétereivel**. Egy **+** gomb a szelector mellett egy új paraméterkészletet hoz létre
  beágyazva (lásd alább) és kiválasztja azt.
- **Symbol / Timeframe** alapértékei `EURUSD` / `h1`, módosíthatók; **Cancel** vagy **Run**.

A **Run** gombra az szerkesztő menti + fordítja az aktuális forráskódot, elindítja az instance-t a választott fiókon
a választott paraméterekkel, majd az élő konténer naplókat követi. (A napló az signalR hub-hoz (`/hubs/logs`) továbbítja a
bejelentkezésben résztvevő felhasználó auth cookie-jét, így csatlakozik az `Invalid negotiation response received` helyett.)

## Paraméterkészletek

A **paraméterkészlet** egy megnevezett, újrahasználható cBot paraméter felülbírálatok halmaza, amely egy
lapos JSON objektumként van tárolva, amely minden paraméter nevet skaláris értékhez rendel, pl. `{"Period": 14, "Label": "trend"}`. A
futtatás/backtest időpontban a cTrader `params.cbotset` fájl (`{ "Parameters": { … } }`) válik. Létrehozhat/szerkeszthet egy
készletet nyers JSON formában a cBot **Parameter sets** párbeszédablakból vagy beágyazva a Run párbeszédablakból.

Minden paraméterkészlet **egy cBothoz tartozik**: a New Parameter Set párbeszédablak felsorolja az összes cBot-ját és
**ki kell választania egyet** — a létrehozás le van tiltva, amíg egy cBot ki nem választódik. Egy készlet **neve egyedi egy cBot-on**:
egy készlet átnevezése vagy átnevezése egy névvel, amelyet ugyanazon cBot egy másik készlete már használ, elutasítják (egyértelmű
hiba a párbeszédablakban, `409 Conflict` az API-nál). Ugyanez a név **eltérő** cBot-on újrahasználható.

A JSON a **mentéskor ellenőrizve** van: lapos objektumnak kell lennie, amelynek értékei mind skalárok
(string / number / bool). Egy nem-objektum gyökér, egy tömb, egy beágyazott objektum, egy `null` érték vagy
hibás JSON elutasítják (egyértelmű hiba a párbeszédablakban, `400 Bad Request` az API-nál). Egy üres objektum `{}`
engedélyezett és azt jelenti, hogy "nincs felülbírálat".

## cTrader Console CLI megjegyzések

A backtestek `--data-mode` (alapértelmezett `m1`) szükséges, dátumok `dd/MM/yyyy HH:mm` formátumban, és
`params.cbotset` JSON pozicionális argumentum; a `run` elutasítja a `--data-dir`-t (csak backtest). Lásd
`ContainerCommandHelpers`.

## Csomópontok és skálázás

Végrehajtási kapacitás bővíthető csomópont ügynökök hozzáadásával (önregisztráció + szívverés). Lásd
a [node discovery](../operations/node-discovery.md) és a [scaling](../deployment/scaling.md) oldalakat.

## Kereskedési fiók szükséges

A cBot futtatásához vagy backtestjéhez szükséges egy cTrader kereskedési fiók a csatlakozáshoz. Amíg nem ad hozzá egyet az
**Trading accounts** alatt, a **Run New cBot** / **Backtest New cBot** gombok le vannak tiltva (tipp-mellett), és az oldal egy
kérdést mutat a fiók beállításához — már nem találja meg a nyers
`stream connect failed` hibát egy fiók nélküli bot-ból.
