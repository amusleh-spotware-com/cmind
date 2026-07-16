---
description: "cTrader cBots (C# és Python, mindkettő .NET) felépítése, futtatása, backtestje böngészőbeli Monaco IDE-ből, futtatás a hivatalos ghcr.io/spotware/ctrader-console képen."
---

# cBots felépítése és backtestje

cTrader cBots (C# **és** Python, mindkettő .NET) felépítése, futtatása, backtestje böngészőbeli Monaco
IDE-ből, futtatás a hivatalos `ghcr.io/spotware/ctrader-console` képen.

## Felépítés

- A **Builder** oldal Monaco szerkesztőt biztosít; a `CBotBuilder` a projektet `dotnet build` -del fordítja le **eldobható konténerben** (`AppOptions.BuildImage`, munkakönyvtár bind-mount az `/work` szinten), így az nem megbízható felhasználó MSBuild-céljai nem érik el a gazdagépet. A NuGet helyreállítás gyorsítótárazódik a buildek között egy megosztott kötet segítségével. A web host Docker socket-hozzáférést igényel.
- A C# + Python kezdősablonok a `src/Nodes/Builder/Templates/` helyen vannak.

## Futtatás és backtest

- Az **Instances** = TPH állapot-hierarchia (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Az átmenet helyettesítve az entitást (id-je megváltozik),
  a konténer id-je átkerül.
- A `NodeScheduler` kiválasztja a legkevésbé terheltnél jogosult csomópontot; a `ContainerDispatcherFactory` útvonalat küld a
  távoli csomópont HTTP ügynökhöz vagy a helyi Docker diszpécserhez.
- A befejezési poller-ek egyeztetik a kilépett konténereket (a backtest konténerei önmagukban kilépnek a `--exit-on-stop` segítségével); a jelentés jelen van → befejezve (tárolás `ReportJson`), hiányzik → sikertelen.
- Az élő konténer naplói az SignalR-en keresztül áramlanak a böngészőbe; a backtest equity görbék az jelentésből elemzésre kerülnek és diagramozódnak.

## A backtest piaci adatai gyorsítótárazódnak számlánként

A cTrader Console a történeti tick/bar adatokat az `--data-dir` címre tölti le. Ez a könyvtár egy
**stabil, állandó gyorsítótár, amely a kereskedési számlán van kulcsozva** (annak számlaszámán) — kötet-mount a csomópont lemezéről annak saját konténer útvonalán (`/mnt/data`), egy **külön, nem beágyazott csatolás** a
per-instance munkakönyvtárból. Így minden backtest ugyanazon a számlán **újrafelhasználja** az már letöltött adatokat, ahelyett, hogy minden futáson frissen letöltené azt. (Korábban az adatkönyvtár a per-instance munkakönyvtárban volt, amelynek id-je minden futáson megváltozik, ami minden backtestre friss letöltésre kényszerített.) Az efemer per-instance munkakönyvtár még tartalmazza az algo-t, paramétereket, jelszót és jelentést; a megosztott adatgyorsítótár a csomópont backtest-data használatában számítódik és a node-clean akció által törlődik.

## Backtest beállítások

A **Backtest** párbeszédablak felhasználó-hangolható cTrader Console backtest beállításokat tesz elérhetővé, így soha nem kell parancssorhoz nyúlnia:

- **Symbol / Timeframe** — az időkeret egy **legördülő lista minden cTrader periódussal** (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1` és a Renko/Range/Heikin periódusok), a
  konzol kanonikus alakjában, így mindig érvényes `--period`-ot választ.
- **From / To** — a backtest ablak (`--start` / `--end`).
- **Data mode** — a három cTrader mód egyike (`--data-mode`): **Tick adatok** (`tick`, pontos),
  **m1 sávok** (`m1`, gyors), vagy **Nyitási árak csak** (`open`, leggyorsabb).
- **Starting balance** — alapértelmezés `10000` (`--balance`). Egy **0 egyenleg nem végez kereskedelmet, és a cTrader üres jelentést bocsát ki, amely később összeomlik** ("Message expected"), így egy nem nulla egyenleg mindig elküldésre kerül.
- **Commission** — `--commission`.
- **Spread** — `--spread`, egy **numerikus mező pipben, amely nem mehet 0 alá**. **Tick adatok módban rejtve van**, ahol a cTrader az spread-et magából a tick adatokból levezetik (nincs `--spread` elküldve).

Az adatkönyvtár (`--data-file` / `--data-dir`) az alkalmazás által felügyelt (per-account gyorsítótár, lásd fent), nem jelenik meg a párbeszédablakban.

:::note A cTrader összeomlik egy üres backtesten
Ha egy backtest **nincs eredményt** — nincs kereskedelem, vagy nincs piaci adat a kiválasztott dátumokhoz/szimbólumhoz — a cTrader Console saját jelentés-írója dobja a `Message expected` hibát és kilép jelentés nélkül. Az alkalmazás nem tudja ezt felfelé javítani, de észleli és az instancet **Failed** (Sikertelen) jelöléssel látja el egy gyakorlatias indoklással ("no backtest results for the selected range…") ahelyett, hogy nyers stack trace-t adna. Válasszon egy szélesebb dátumtartományt, amelyhez elérhető piaci adatok vannak, és próbálja újra.
:::

## Instance detail oldal

Egy instance megnyitása (`/instance/{id}`) megjeleníti az élő állapotát, naplóit és — egy backtest esetén — az equity görbét. A **böngésző fül címe** az adott instancet tükrözi (**cBot neve · típusa · szimbóluma**, pl. `TrendBot · Backtest · EURUSD`), így az élő futtatás füle és a backtest füle egy pillantásra megkülönböztethető. Egy cBot futtatása és backtestje külön **lineage-ként** követkedik (egy stabil lineage id az állapot-átmeneteken keresztül), így az oldal pontosan egy instancet követ, és soha nem keveri össze a futtatás adatait egy backtest adataival.

## Instance életciklus-vezérlők

Minden instance sor (és annak detail oldala) állapot-helyes vezérlőkkel rendelkezik. Egy **aktív** instance mutatja a **Stop** gombot; egy **terminális** (Stopped / Completed / Failed) pedig mutatja a **Start (▶)** gombot az újraindításhoz ugyanazon cBot, fiók, szimbólum, időkeret, paraméter készlet és kép segítségével (egy futtatás futtatásként indul újra, egy backtest backtestként). A Stop kattintáskor egy "Stopping…" értesítés jelenik meg és az ikon le van tiltva, amíg az nem rendezõdik, és egy újonnan létrehozott futtatás azonnal megjelenik a listában — nincs oldal újratöltés.

A konzol naplói **megmaradnak, amikor egy instance végpontot ér** — futtatás (Stop-on) és **backtest** (befejezéskor) esetén — így az utolsó futtatás naplói megtekinthetõek maradnak a detail oldalon és, a napló eszköztáron keresztül, **vágólapra másolva** (Naplók másolása ikon) vagy **letöltve** (Naplók letöltése ikon), még azután is, hogy a konténer már eltûnt. Mindkettõ az instance teljes konzol-naplóján mûködik, nem csak a képernyõn megjelenõ végén.

Az **feltöltött** `.algo` sohasem lett felépítve itt, ezért az **Last Build** oszlopa a cBots oldalon üres marad (csak az itt böngészõben felépített cBots mutatnak összeállítási idõt).

## Leállított instance szerkesztése és újbóli futtatása

A **leállított** instance (futtatás vagy backtest) rendelkezik egy **Edit** vezérlõvel — egy ikon az sor listáján **és** Start/Stop mellett a detail oldalán — amely egy párbeszédablakot nyit meg, amely az **elõre kitöltött** az aktuális konfigurációval. Módosíthatja a **kereskedési fiókot, szimbólumot, időkeret, paraméter készletet és kép címkét** (és backtest esetén az **ablakot és az összes fenti backtest-beállítást**), majd a **Save & start** az újraindítja az új beállításokkal (helyettesítve a leállított instancet). A vezérlõ **le van tiltva, amíg az instance aktív** — csak egy leállított instance szerkeszthetõ.

## Futtatás a kódszerkesztõbõl

A kódszerkesztõben a **Run** kattintáskor egy párbeszédablak nyílik meg a vakon, merevített futtatás helyett:

- **Trading account** (kötelezõ) — a cTrader fiók, amelyhez a cBot csatlakozik.
- **Parameter set** (opcionális) — válasszon egy meglévõ készletet, vagy hagyja üresen, hogy a cBot **alapértelmezett paraméter értékeivel** fusson. A szelektort szomszédos **+** gomb egy új paraméter készletet hoz létre beágyazottan (lásd alább) és kiválasztja azt.
- **Symbol / Timeframe** alapértelmezés `EURUSD` / `h1`, és módosítható; **Cancel** vagy **Run**.

A **Run** kattintáskor a szerkesztõ menti + felépíti az aktuális forrást, elindítja az instancet a kiválasztott fiókon a kiválasztott paraméterekkel, majd az élõ konténer naplóit követi. (A napló stream az aláírt felhasználó hitelesítési sütijét továbbítja a `/hubs/logs` SignalR hubra, így csatlakozik az `Invalid negotiation response received` helyett.)

## Paraméter készletek

A **parameter set** egy elnevezett, újrafelhasználható cBot paraméter felülírások készlete, amely egy sík JSON objektumként tárolódik, amely minden paraméter nevét egy skaláris értékre leképezi, pl. `{"Period": 14, "Label": "trend"}`. A futtatás/backtest idõben a cTrader `params.cbotset` fájlvá alakul (`{ "Parameters": { … } }`). Létrehozhat/szerkeszthet egy készletet nyers JSON-ként a cBot **Parameter sets** párbeszédablakjából vagy beágyazottan a Run párbeszédablakból.

Minden paraméter készlet **egy cBothoz tartozik**: az Új paraméter készlet párbeszédablak az összes cBot-ját listázza, és **ki kell választanod egyet** — a létrehozás le van tiltva, amíg egy cBot nincs kiválasztva. A készlet **neve egyedi cBotonként**: egy készlet átnevezése ugyanarra a cBotra egy olyan névre, amelyet egy másik, ugyanazon cBot más készletének már használ, elvetítésre kerül (egy világos hiba a párbeszédablakban, `409 Conflict` az API-ban). Ugyanez a név újrafelhasználható egy **másik** cBoten.

A JSON **érvényesítésre kerül** mentéskor: egyetlen sík objektumnak kell lennie, amelynek értékei mind skalárok (string / szám / logikai). Egy nem objektum gyökér, egy tömb, egy beágyazott objektum, egy `null` érték, vagy hibás JSON elvetítésre kerül (egy világos hiba a párbeszédablakban, `400 Bad Request` az API-ban). Egy üres objektum `{}` megengedett, és azt jelenti, hogy "nincs felülírás".

## cTrader Console CLI megjegyzések

A backtestekhez `--data-mode` szükséges (alapértelmezés `m1`), dátumok as `dd/MM/yyyy HH:mm`, és `params.cbotset` JSON pozicionális arg; a `run` elutasítja a `--data-dir` (csak backtest). Lásd a `ContainerCommandHelpers` helyet.

## Csomópontok és skálázás

A végrehajtási kapacitás csomópont ügynökök hozzáadásával növekszik (önregisztráció + szívverés). Lásd a [node discovery](../operations/node-discovery.md) és [scaling](../deployment/scaling.md) oldalakat.

## Kereskedési fiók szükséges

A cBot futtatásához vagy backtestjéhez szükséges egy cTrader kereskedési fiók, hogy csatlakozzon. Amíg nem adja hozzá az egyik a **Trading accounts** alatt, a **Run New cBot** / **Backtest New cBot** gombokat le vannak tiltva (eszköztippel), és az oldal egy prompt-ot mutat, amely a fiók beállítására hivatkozik — már nem találkozhat egy nyers `stream connect failed` hibával egy fiók nélküli botról.
