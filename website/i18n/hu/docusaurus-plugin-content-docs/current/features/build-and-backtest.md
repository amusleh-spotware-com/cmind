---
description: "cTrader cBotok létrehozása, futtatása, backtestje (C# és Python, mindkettő .NET) böngészőben futó Monaco IDE-ből, futtatás a hivatalos ghcr.io/spotware/ctrader-console képen."
---

# cBotok létrehozása és backtestje

Hozzon létre, futtasson és backteszteljen cTrader cBotokat (C# **és** Python, mindkettő .NET) a böngészőben futó Monaco IDE-ből, futtatás a hivatalos `ghcr.io/spotware/ctrader-console` képen.

## Létrehozás

- **Builder** oldal tartalmazza a Monaco szerkesztőt; `CBotBuilder` lefordítja a projektet a `dotnet build` paranccsal **egy ideiglenes konténerben** (`AppOptions.BuildImage`, munkakönyvtár bind-mount a `/work` útvonalon), így a nem megbízható felhasználó MSBuild céljainak nincs hozzáférése a gazdagéphez. A NuGet restore gyorsítótárazott a buildek között egy megosztott kötet segítségével. A webes gazdagépnek Docker socket hozzáférésre van szüksége.
- A C# + Python kezdősablonok a `src/Nodes/Builder/Templates/` könyvtárban vannak.

## Futtatás és backtest

- **Instances** = TPH állapot-hierarchia (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). Az átmenet helyettesíti az entitást (ID módosul), a konténer ID átkerül.
- `NodeScheduler` kiválasztja a legkevésbé terhelelt jogosult csomópontot; `ContainerDispatcherFactory` irányítja a távoli csomópont HTTP ügynökéhez vagy a helyi Docker diszpécseréhez.
- Az befejezési pollerek egyeztetik a kilépett konténereket (a backtest konténerek a `--exit-on-stop` segítségével automatikusan kilépnek); jelent lévő jelentés → befejeződött (tárolt `ReportJson`), hiányzó → sikertelen.
- Az élő konténer naplói SignalR-en keresztül streamelődnek a böngészőhöz; a backtest saját tőke görbéit a jelentésből elemezve, majd ábrázolva.

## A backtest piaci adatai konténként gyorsítótárazottak

A cTrader Console letölti az elmúlt tick/bar adatokat a `--data-dir` könyvtárba. Ez a könyvtár egy **stabil, állandó gyorsítótár, amely a kereskedési számlára van indexelve** (annak számlaszámára) — bind-mountolt a csomópont lemezéről annak saját konténer útvonalánál (`/mnt/data`), egy **külön, nem beágyazott mount** az instance-onkénti munkakönyvtárból. Így az ugyanazon a számlán végzett minden backtest **újra felhasználja** az már letöltött adatokat ahelyett, hogy újra letöltené azokat minden futtatáskor. (Korábban az adatkönyvtár az instance-onkénti munkakönyvtár alatt volt, amelynek ID-je minden futtatáskor megváltozik, ami minden backteszt újra letöltésére kényszerített.) Az ideiglenes instance-onkénti munkakönyvtár továbbra is az algoritmus, paraméterek, jelszó és jelentés tárolására szolgál; a megosztott adatgyorsítótár a csomópont backtest-adat felhasználásában számítódik, és a csomópont tisztítási művelete által törlödik.

## Backtest beállítások

A **Backtest** párbeszédpanel minden beállítást közzétesz, amelyet a cTrader Console backtest CLI elfogad, így soha nem kell hozzáérnie a parancssorhoz:

- **From / To** — a backtest ablak (`--start` / `--end`).
- **Data mode** — `m1` (1 perces rudak) vagy `tick` (`--data-mode`).
- **Starting balance** — alapértelmezett `10000` (`--balance`). A **0 egyenlege nem hajt végre kereskedéseket, és a cTrader üres jelentést bocsát ki, amely aztán összeomlik** ("Message expected"), így egy nem nulla egyenlege mindig elküldödik.
- **Commission** és **Spread** (`--commission` / `--spread`, spread in pips).
- **Advanced options** — egy szabad formátumú `name=value` per sor mező bármilyen egyéb backtest opcióhoz, amelyet a cTrader támogat (pl. `applyCommissionAutomatically=true`); minden sor egy `--name value` CLI argumentummá válik.

## Instance részletoldal

Egy instance megnyitásakor (`/instance/{id}`) megjeleníti az élő állapotot, naplókat, és — egy backtest esetén — a saját tőke görbét. A **böngésző lap címe** az adott instancere utal (**cBot név · típus · szimbólum**, pl. `TrendBot · Backtest · EURUSD`), így az élő futtatási lap és a backtest lap egy pillantásra megkülönböztethető. Az ugyanazon cBot egy futtatása és egy backtestje különálló **leszármazásként** (egy stabil leszármazási ID, amely az állapot-átmenetek során átvitt) követkedtetik, így az oldal pontosan egy instancet követ, és soha nem keveri össze egy futtatás adatait egy backtest adataival.

## Instance életciklus-vezérlők

Minden instance sor (és annak részletoldala) állapot-helyes vezérlőkkel rendelkezik. Egy **aktív** instance megjeleníti a **Stop**-ot; egy **terminál** (Stopped / Completed / Failed) a **Start (▶)** gombot mutatja az újraindítás érdekében ugyanazzal a cBottal, fiókkal, szimbólummal, időkeret és képpel (egy futtatás futtatásként indul újra, egy backtest backtestként). A Stop megnyomása egy "Stopping…" értesítést mutat, és letiltja az ikont, amíg az megoldódik, és egy újonnan létrehozott futtatás azonnal megjelenik a listában — nincs oldal-frissítésre szükség.

A konzol naplói **megmaradnak, amikor egy instance befejeződik** — egy futtatásra (Stop-on) és egy **backtest-re** (befejezéskor) egyaránt — így az utolsó futtatás naplói megtekinthetők maradnak a részletoldalon, és a naplósáv segítségével, **másolt a vágólapra** (Copy logs ikon) vagy **letöltve** (Download logs ikon), még akkor is, ha a konténer már nincs jelen. Mindkettő az instance teljes konzol naplójára hat, nem csak a képernyőn látható végre.

Egy **feltöltött** `.algo` soha nem lett felépítve itt, így az **Last Build** oszlopa a cBots oldalon üres marad (csak a böngészőben felépített cBotok esetén mutat felépítési időt).

## Leállított instance szerkesztése és újrafuttatása

Egy **leállított** instance (futtatás vagy backtest) rendelkezik egy **Edit** vezérlővel — egy ikon a lista során **és** a Start/Stop mellett a részletoldalon — amely egy párbeszédpanelt nyit meg az **előkitöltött** jelenlegi konfigurációjával. Módosíthatja a **kereskedési fiókot, szimbólumot, időkeretet, paraméter-készletet és kép-taget** (és egy backtest esetén az **ablakot és az összes fenti backtest beállítást**), majd a **Save & start** újra elindítja az új beállításokkal (helyettesítve a leállított instancet). A vezérlő **letiltott az instance aktív állapotában** — csak egy leállított instance szerkeszthető.

## Futtatás a kódszerkesztőből

A kódszerkesztőben a **Run** gombra kattintva egy párbeszédpanel nyílik meg ahelyett, hogy egy nyitott, kemény futtatást indítana:

- **Trading account** (szükséges) — a cTrader fiók, amelyhez a cBot csatlakozik.
- **Parameter set** (opcionális) — válasszon egy meglévő készletet, vagy hagyja üresen a cBot **alapértelmezett paraméter-értékeivel** való futtatáshoz. A választó mellett egy **+** gomb lehetővé teszi egy új paraméter-készlet belső létrehozását (lásd alább) és annak kiválasztását.
- **Symbol / Timeframe** alapértelmezés szerint `EURUSD` / `h1`, és módosítható; **Cancel** vagy **Run**.

A **Run** megnyomásakor a szerkesztő elmenti és felépíti az aktuális forrást, elindítja az instancet a választott fiókon a kiválasztott paraméterekkel, majd rákötődik az élő konténer naplóira. (A napló stream a bejelentkezett felhasználó auth cookie-ját a `/hubs/logs` SignalR hubhoz továbbítja, így csatlakozik ahelyett, hogy az `Invalid negotiation response received` hibával kudarcot vallana.)

## Paraméter-készletek

Egy **parameter set** egy elnevezett, újrafelhasználható cBot paraméter-felülbírálatok készlete, amely egy sima JSON objektumként tárolódik, amely az egyes paraméter neveket egy skaláris értékre leképezi, pl. `{"Period": 14, "Label": "trend"}`. A futtatás/backtest időben átalakítódik a cTrader `params.cbotset` fájlba (`{ "Parameters": { … } }`). Egy készletet bruttó JSON-ként hozhat létre/szerkeszthet a cBot **Parameter sets** párbeszédpaneljéből vagy a Run párbeszédpanelből belül.

Minden paraméter-készlet **egy cBothoz tartozik**: az New Parameter Set párbeszédpanel felsorolja az összes cBotot, és **ki kell választania egyet** — az létrehozás blokkolódik, amíg egy cBot ki nem választódik. Egy készlet **neve egyedi egy cBotnként**: egy készlet egy olyan névre való létrehozása vagy átnevezése, amelyet az ugyanazon cBot egy másik készlete már használ, elutasítódik (világos hiba a párbeszédpanelben, `409 Conflict` az API-ban). Ugyanaz a név újrafelhasználható egy **eltérő** cBoton.

A JSON **validálva** van a mentéskor: egy egyetlen, sima objektumnak kell lennie, amelynek értékei mindegyike skaláris (string / number / bool). Egy nem objektum gyökerezet, egy tömb, egy beágyazott objektum, egy `null` érték vagy rossz JSON elutasítódik (világos hiba a párbeszédpanelben, `400 Bad Request` az API-ban). Egy üres objektum `{}` megengedett, és azt jelenti, hogy "nincs felülbírálat".

## cTrader Console CLI megjegyzések

A backtestek igényelnek `--data-mode` (alapértelmezett `m1`), dátumokat `dd/MM/yyyy HH:mm` formátumban, és a `params.cbotset` JSON pozicionális argumentumot; a `run` elutasítja a `--data-dir` (csak backtest-hez). Lásd: `ContainerCommandHelpers`.

## Csomópontok és skálázás

A végrehajtási kapacitás csomópont-ügynökök hozzáadásával skálázódik (önálló regisztráció + szívverés). Lásd: [node discovery](../operations/node-discovery.md) és [scaling](../deployment/scaling.md).

## Szükséges egy kereskedési fiók

A cBot futtatása vagy backtestje szükséges egy cTrader kereskedési számlához való csatlakozáshoz. Amíg nem ad hozzá egyet a **Trading accounts** alatt, a **Run New cBot** / **Backtest New cBot** gombok le vannak tiltva (egy tooltip-pel), és az oldal egy üzenetet mutat, amely a fiók beállításához vezet — többé nem kap nyers `stream connect failed` hibát egy fiók nélküli botból.
