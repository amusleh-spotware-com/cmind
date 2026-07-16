---
description: "cTrader cBotok építése, futtatása, backtesztje (C# és Python, mindkettő .NET) a böngészőben futó Monaco IDE-ből, futtatás a ghcr.io/spotware/ctrader-console képen."
---

# Build & backtest cBotok

cTrader cBotok (C# **és** Python, mindkettő .NET) építése, futtatása, backtesztje a böngészőben futó Monaco IDE-ből, futtatás a `ghcr.io/spotware/ctrader-console` képen.

## Építés

- **Builder** oldal Monaco szerkesztőt üzemeltet; `CBotBuilder` lefordítja a projektet
  `dotnet build` **egyszer használatos konténerben** (`AppOptions.BuildImage`, munkakönyvtár bind-mount
  `/work` alatt), hogy nem megbízható felhasználói MSBuild célok ne érjék el a gazdagépet. NuGet restore gyorsítótárazva
  a buildek között megosztott kötet révén. Web gazdagépnek Docker socket hozzáférésre van szüksége.
- C# + Python kezdő sablonok az `src/Nodes/Builder/Templates/` mappában találhatók.

## Futtatás & backtest

- **Instances** = TPH állapothierarchia (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Az átmenet helyettesít entitást (id változás),
  konténer id átkerül.
- `NodeScheduler` kiválasztja a legkevésbé terhelelt jogosult csomópontot; `ContainerDispatcherFactory` útvonal
  távoli csomópont HTTP ügynökhöz vagy helyi Docker dispatcher-hez.
- Befejezési poller-ek egyeztetik a kilépett konténereket (backtest konténerek magától kilépnek via
  `--exit-on-stop`); jelentés jelen → befejezett (tárolt `ReportJson`), hiányzó → sikertelen.
- Élő konténer naplók streamelnek a böngészőbe SignalR-on keresztül; backtest equity görbék elemezve a
  jelentésből + ábrázolva.

## Backtest piacadatok gyorsítótárazva fiók alapján

A cTrader Console letölti az előzményadatokat (tick/bar) az `--data-dir`-be. Ez a könyvtár egy
**stabil, tartós gyorsítótár, amely a kereskedési fiók alapján indexelt** (számlaszáma alapján) — bind-mounted
a csomópont lemezéről annak saját konténer elérési útjánál (`/mnt/data`), egy **külön, nem beágyazott mount**
a per-instance munkakönyvtáriból. Így ugyanazon a számlán minden backtest **újra felhasználja** a már letöltött adatokat
helyett minden futtatáskor újra letöltés helyett. (Korábban az
adatkönyvtár az per-instance munkakönyvtár alatt élt, amelynek id-je minden futtatásnál változik, ami friss letöltést kényszerített minden backtesztre.) Az efemer per-instance munkakönyvtár még mindig az algoritmust, paramétereket, jelszót
és jelentést tartalmazza; a megosztott adatgyorsítótár egy csomópont backtest-adatok használatában számít és a
csomópont-tisztító akcióval törlődik.

## Backtest beállítások

A **Backtest** párbeszédablak minden beállítást felfed, amelyet a cTrader Console backtest CLI elfogad, így soha
ne kelljen parancssor érintéséhez:

- **From / To** — a backtest ablak (`--start` / `--end`).
- **Data mode** — az egyik a három cTrader módból (`--data-mode`): **Tick data** (`tick`, pontos),
  **m1 bars** (`m1`, gyors), vagy **Open prices only** (`open`, leggyorsabb).
- **Starting balance** — alapértelmezés `10000` (`--balance`). A **0 egyenleg nem ad fel kereskedéseket és arra kényszeríti
  a cTrader-t, hogy kibocsásson egy üres jelentést, majd erre összeomlik** ("Message expected"), így nem nulla egyenleg
  mindig elküldésre kerül.
- **Commission** és **Spread** — `--commission` / `--spread` (spread pips-ben).
- **Data file** (opcionális) — csomópont-oldali útvonal egy történeti adatfájlhoz (`--data-file`); hagyja üresen a
  letöltött/gyorsítótárban tárolt adatok használatához.
- **Expose environment variables** — egy váltó, amely a gazdagép környezeti változóit átadja a cBot-nak
  (a `--environment-variables` zászló).

## Instance detail oldal

Egy instance megnyitásakor (`/instance/{id}`) az élő állapot, naplók és — backteszt esetén — az equity
görbe jelenik meg. A **böngészőlap címe** tükrözi az adott instance-ét (**cBot név · típus · szimbólum**, pl.
`TrendBot · Backtest · EURUSD`), így egy élő futási lap és egy backtest lap egyetlen pillantásra megkülönböztethetők.
A cBot futása és backtesztje állapotátmenetek közötti különálló **vonalak**-ként vannak követve (stabil lineage id),
így az oldal pontosan egy instance-t követi és soha nem keveri egy futás adatait egy backtest-ével.

## Instance lifecycle kontrollok

Minden instance sor (és annak detail oldala) állapot-helyes kontrollt tartalmaz. Egy **aktív** instance mutat
**Stop**; egy **terminális** (Stopped / Completed / Failed) mutat **Start (▶)** az eredeti cBot, fiók, szimbólum, timeframe, paraméter set és image-el való újraindítása (a futás futásként indul újra, a backtest backtesztként). Stop kattintásakor megjelenik egy "Stopping…" értesítés és letiltja az ikont míg nem oldódik meg, és egy újonnan létrehozott futás azonnal megjelenik a listában — oldal újratöltés nélkül.

A konzol naplók **egy instance terminálódásakor megmaradnak** — egy futásnál (Stop-on) és egy
**backtest** (befejezéskor) egyaránt — így az utolsó futás naplói megtekinthetők a detail oldalon és,
a napló eszköztáron keresztül, **másolva a vágólapra** (Copy logs ikon) vagy **letöltve** (Download logs
ikon) még akkor is, miután a konténer már nincs. Mindkettő az instance teljes konzol naplójára hat, nem csak a
képernyőn lévő végre.

Egy **feltöltött** `.algo` soha nem lett itt felépítve, így annak **Last Build** oszlopa a cBots oldalon üres
(csak azokhoz a cBotokhoz mutat buildidőt, amelyeket itt épít a böngészőben).

## Szerkesztés & megállt instance újraindítása

A **megállt** instance (futás vagy backtest) tartalmaz egy **Edit** kontroll — egy ikon annak során a listában **és**
a Start/Stop mellett a detail oldalon — amely megnyit egy párbeszédablakot **előre kitöltve** a jelenlegi konfigurációjával.
Megváltoztathatja a **kereskedési fiók, szimbólum, timeframe, paraméter set és image tag** (és backteszt esetén az
**ablak és az összes fenti backtest beállítás**), majd **Save & start** újraindítja azt az új beállításokkal (helyettesítve a megállt instance-t). A kontroll **le van tiltva, míg az instance aktív** —
csak egy megállt instance szerkeszthető.

## Futtatás a kódszerkesztőből

A kódszerkesztőben a **Run** kattintásra egy párbeszédablak nyílik meg vakon, nem pedig egy fix futás:

- **Trading account** (kötelező) — a cTrader fiók, amelyhez a cBot kapcsolódik.
- **Parameter set** (opcionális) — válasszon egy meglévő halmazt, vagy hagyja üresen az alapértelmezett futtatáshoz a cBot **alapértelmezett paraméter értékei**. Egy **+** gomb a választó mellett új paraméter halmazt hoz létre
  inline (lentebb) és kiválasztja azt.
- **Symbol / Timeframe** alapértelmezés `EURUSD` / `h1` és megváltoztatható; **Cancel** vagy **Run**.

A **Run** kattintásra a szerkesztő menti + felépíti az aktuális forráskódot, elindítja az instance-t a kiválasztott fiók
paraméterekkel, majd a élő konténer naplókat figyeli. (A napló stream továbbítja a bejelentkezett felhasználó auth cookie-ját a `/hubs/logs` SignalR hub-hoz, így csatlakozik ahelyett, hogy meghiúsulna az
`Invalid negotiation response received` hibával.)

## Paraméter halmazok

A **parameter set** egy elnevezett, újrafelhasználható cBot paraméter felülírások halmaza, amely egy sík JSON
objektumként van tárolva, amely minden paraméternevet leképez egy skaláris értékre, pl. `{"Period": 14, "Label": "trend"}`. Futtatás/backtest időnél a cTrader `params.cbotset` fájllá alakul
(`{ "Parameters": { … } }`). Nyers JSON-ből létrehozhat/szerkeszthet egy halmazt a cBot **Parameter
sets** párbeszédből vagy inline a Run párbeszédből.

Minden paraméter halmaz **egy cBot-hoz tartozik**: a New Parameter Set párbeszédablak az összes cBot-odat listázza és
**ki kell választania egyet** — a létrehozás addig letiltva van, amíg a cBot nincs kiválasztva. A halmaz **neve egyedi per cBot-ként**:
egy halmazt egy olyan névre létrehozni vagy átnevezni, amelyet ugyanaz a cBot már használ, elutasításra kerül (egyértelmű
hiba a párbeszédben, `409 Conflict` az API-ban). Ugyanez a név **más** cBot-on lehet újra felhasználni.

A JSON **ellenőrzött** mentésnél: egyetlen lapos objektumnak kell lennie, amelynek értékei mind skalár
(string / szám / bool). Nem-objektum gyökér, tömb, beágyazott objektum, `null` érték vagy rosszul formázott
JSON elutasításra kerül (egyértelmű hiba a párbeszédben, `400 Bad Request` az API-ban). Egy üres objektum `{}`
megengedett, és azt jelenti, hogy "nincs felülírás".

## cTrader Console CLI megjegyzések

A backteszt-ek szükséges `--data-mode` (alapértelmezés `m1`), dátumok mint `dd/MM/yyyy HH:mm`, és
`params.cbotset` JSON pozicionális argumentum; `run` elutasít `--data-dir` (csak backtest). Lásd
`ContainerCommandHelpers`.

## Csomópontok & méretezés

A végrehajtási kapacitás skálázódik csomópontügynökök hozzáadásával (önregisztráció + szívverés). Lásd
a [node discovery](../operations/node-discovery.md) és [scaling](../deployment/scaling.md) oldalt.

## Kereskedési fiók szükséges

cBot futtatása vagy backtesztje egy cTrader kereskedési fiók szükséges ahhoz, hogy csatlakozzék. Amíg nem adott hozzá egyet a
**Trading accounts** alatt, a **Run New cBot** / **Backtest New cBot** gombok letiltva vannak (egy tooltip-el) és az oldal egy promptot mutat, amely a fiók beállításához vezet — már nem ütközik egy nyers
`stream connect failed` hibára egy bot nélküli fiók nélkül.
