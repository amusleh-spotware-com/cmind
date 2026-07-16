---
description: "cTrader cBotok fordítása, futtatása, backtesztelése (C# és Python, mindkettő .NET) böngészőben lévő Monaco IDE-ből, futtatás az official ghcr.io/spotware/ctrader-console képen."
---

# cBotok fordítása és backtesztelése

cTrader cBotok fordítása, futtatása, backtesztelése (C# **és** Python, mindkettő .NET) böngészőben lévő Monaco IDE-ből, futtatás az official `ghcr.io/spotware/ctrader-console` képen.

## Fordítás

- A **Builder** oldal Monaco szerkesztőt üzemeltet; a `CBotBuilder` a projektet `dotnet build` paranccsal fordítja le egy **eldobható konténerben** (`AppOptions.BuildImage`, munkakönyvtár bind-mount `/work` helyen), így a nem megbízható felhasználó MSBuild célok nem érik el a gazdagépet. A NuGet helyreállítás gyorsítótárazott az összeállítások között egy megosztott kötet segítségével. A webes host Docker szoftvercsatorna-hozzáférésre van szükség.
- A C# és Python indító sablonok a `src/Nodes/Builder/Templates/` mappában találhatók.

## Futtatás és backtesztelés

- Az **Instances** (Példányok) = TPH állapothierarchia (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). Az átmenet helyettesíti az entitást (azonosító változás), a konténer azonosítót átviszik.
- A `NodeScheduler` a legkevésbé terhelten lévő jogosult csomópontot választja ki; a `ContainerDispatcherFactory` egy távoli csomópont HTTP ügynökre vagy a helyi Docker diszpécserhez irányítja az útvonalat.
- A befejezési poller-ek kiegyenlítik a kilépett konténereket (a backteszt konténerek önként kilépnek a `--exit-on-stop` segítségével); a jelentés jelen van → befejezve (tárolás `ReportJson`), hiányzik → sikertelen.
- Az élő konténer naplók SignalR-en keresztül folynak a böngészőre; a backteszt részvénygörbék az objektumból elemzett és diagrammázott.

## A backteszt piaci adatai konténkeyntonként vannak gyorsítótárazva

A cTrader Console a `--data-dir` adatkönyvtárba tölti le a történeti tick/bar adatokat. Ez a könyvtár egy **stabil, perzisztens gyorsítótár, amely a kereskedési számlára** (annak számlaszámára) van kulcsozva — a csomópont lemezéről bind-mount-olva a saját konténer útvonalán (`/mnt/data`), a **per-instance munkakönyvtár egy külön, nem egymásba ágyazott csatlakoztatása**. Így minden backteszt ugyanazon a számlán **újrahasznosítja** az már letöltött adatokat az egyes futások során történő újbóli letöltés helyett. (Korábban az adatkönyvtár az per-instance munkakönyvtár alatt volt, amelynek azonosítója minden futás során megváltozik, ami minden backtesztet friss letöltésre kényszerített.) Az ephemeral per-instance munkakönyvtár még mindig az algoritmust, paramétereket, jelszót és jelentést tartalmazza; a megosztott adatok gyorsítótára a csomópont backteszt-adatok használatában számít, és a csomópont tiszta művelete törli.

## Backteszt beállítások

A **Backtest** (Backtesztelés) párbeszédpanel a felhasználó által beállítható cTrader Console backteszt beállítások közé fejleszt, így soha nem kell megérintenie a parancssor:

- **Symbol / Timeframe** — az időkeret az **összes cTrader periódus legördülő menüje** (`t1`…`t1000`, `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1` és a Renko/Range/Heikin időszakok), a konzol kanonikus dobozaiban, így mindig érvényes `--period`-ot választhat.
- **From / To** — a backteszt ablak (`--start` / `--end`).
- **Data mode** — a cTrader három módja közül egy (`--data-mode`): **Tick data** (`tick`, pontos), **m1 bars** (`m1`, gyors), vagy **Open prices only** (`open`, leggyorsabb).
- **Starting balance** — alapértelmezés szerint `10000` (`--balance`). A **0 egyenleg nem hajtja végre a kereskedéseket, és a cTrader üres jelentés kibocsátásra kényszerít, majd összeomlanak ("Message expected"), így mindig nem nulla egyenleget küldenek el.
- **Commission** — `--commission`.
- **Spread** — `--spread`, egy **numerikus mező pipekben, amely nem mehet 0 alá**. **A Tick adatok módban rejtett**, ahol a cTrader a spread-et a tick adatokból vezeti le (nincs `--spread` elküldve).

Az adatkönyvtár (`--data-file` / `--data-dir`) az alkalmazás által felügyelt (per-account gyorsítótár, lásd fent), nem jelenik meg a párbeszédpanelen.

:::note cTrader összeomlása egy üres backteszten
Ha a backteszt **nem ad eredményt** — nincs kereskedés, vagy nincs piaci adat a választott dátumokhoz/szimbólumhoz — a cTrader Console saját jelentés írója `Message expected`-ot dob, és kilép, szinte jelentés nélkül. Az alkalmazás nem tudja az upstream hibát kijavítani, de észleli és megjegyzi az instance-t **Failed** (Sikertelen) állapottal, egy működésre képes oka miatt ("no backtest results for the selected range…") egy nyers stack trace helyett. Válasszon egy szélesebb dátumtartományt, amely rendelkezik elérhető piaci adatokkal, és próbálkozzon újra.
:::

## Instance részletoldala

Az instance megnyitása (`/instance/{id}`) az élő állapotot, naplókat és — backteszt esetén — a részvénygörbét mutatja. A **böngésző lapjának címe** az adott instancet tükrözi (**cBot név · típus · szimbólum**, pl. `TrendBot · Backtest · EURUSD`), így egy élő futtatási fül és egy backteszt fül azonnal megkülönböztethető. Az ugyanazon cBot futtatása és backtesztelése az **vonalak** között követett (egy stabil vonalazonosító az állapotátmeneteken keresztül), így az oldal pontosan egy instancet követi, és soha nem keveri a futtatás adatait a backteszt adataihoz.

## Instance életciklus kezelőelemek

Az egyes instance sorok (és a részletoldal) állapothoz helyes vezérlésekkel rendelkeznek. Egy **aktív** instance **Stop** (Leállítás) gombot mutat; egy **terminal** (Stopped / Completed / Failed) pedig **Start (▶)** gombot mutat az újraindításához, ugyanazon cBot, fiók, szimbólum, időkeret, paraméter készlet és kép (egy futtatás futtatásként újraindul, egy backteszt backtesztként). A Stop kattintásra a "Stopping…" (Leállítás alatt) hirdetmény és letiltott ikon kerül megjelenítésre, amíg feloldódik, és az újonnan létrehozott futtatás azonnal megjelenik a listában — oldalfrissítés nélkül.

Az konzol naplók **megmaradnak, ha az instance leáll** — egy futtatásra (Leállításkor) és egy **backtesztra** (befejezéskor) egyaránt — így az utolsó futtatás naplói a részletoldalon tekinthetők meg, és a naplók eszköztár segítségével **a vágólapra másolva** (Naplók másolása ikon) vagy **letöltve** (Naplók letöltése ikon) még az után is, hogy a konténer már nem létezik. Mindkét műveletnél az instance teljes konzol naplójára működik, nem csak a képernyőn látható farok.

Az **elkészített backteszt** az **cTrader jelentést** mindkét formátumban is megtartja — az nyers **JSON** (ugyanaz, amelyet a részvénygörbe és a mesterséges intelligencia elemzés olvas) és a teljes **HTML** jelentés. Mindkettő letölthető a backteszt sorból **és** a részletoldalról dedikált ikonok segítségével. Csak az **utolsó futtatás** jelentéseit tartják meg, és az ikonok **le vannak tiltva** minden backtesztnél, amely nincs elindítva, fut vagy sikertelen (és soha nem jelennek meg a futtatási instancenél) — csak egy befejezett backteszten van letölthető jelentés.

Az **feltöltött** `.algo` soha nem lett itt fordítva le, így az **Last Build** (Utolsó összeállítás) oszlopa a cBots oldalon üres (ez csak azokhoz a cBotokhoz mutat összeállítási időt, amelyeket a böngészőben fordít le).

## Leállított instance szerkesztése és újrafuttatása

Egy **leállított** instance (futtatás vagy backteszt) egy **Edit** (Szerkesztés) vezérléshez tartozik — egy ikon a lista sorában **és** a Start/Stop mellett a részletoldalon — amely egy párbeszédpanelt nyit **az aktuális konfigurációval előre feltöltve**. Megváltoztathatja a **kereskedési számlát, szimbólumot, időkeretet, paraméter készletet és képcímkét** (és backteszt esetén az **ablakot és az összes fenti backteszt beállítást**), majd a **Save & start** (Mentés és indítás) gomb az új beállítások közé újraindítja (helyettesítve a leállított instancet). A vezérlés **letiltott, amíg az instance aktív** — csak egy leállított instance szerkeszthető.

## Futtatás a kódszerkesztőből

A kódszerkesztőben a **Run** (Futtatás) gombra kattintva egy párbeszédpanel jelenik meg a vak, kemény kódú futtatás helyett:

- **Trading account** (Kereskedési fiók) (szükséges) — a cTrader fiók, amelyhez a cBot csatlakozik.
- **Parameter set** (Paraméter készlet) (opcionális) — válasszon egy meglévő készletet, vagy hagyja üresen a cBot **alapértelmezett paraméter értékeivel** való futtatáshoz. A kiválasztó melletti **+** gomb létrehoz egy új paraméter készletet inline (lásd alább) és kijelöli.
- A **Symbol / Timeframe** alapértelmezés szerint `EURUSD` / `h1` értékre állnak, és módosíthatók; **Cancel** (Mégse) vagy **Run** (Futtatás).

A **Run** (Futtatás) gombra kattintva a szerkesztő menti + összeállítja az aktuális forrást, az instance indítása a kiválasztott számlán a választott paraméterekkel, majd az élő konténer naplókat továbbítja. (A naplófolyam a bejelentkezett felhasználó hitelesítési sütijét a `/hubs/logs` SignalR hubra továbbítja, így csatlakozik az `Invalid negotiation response received` helyett.)

## Paraméter készletek

Az **paraméter készlet** egy elnevezett, újrafelhasználható cBot paraméter felülbírálási készlet, amely sima JSON objektumként tárolódik, amely minden paraméter nevet a skalár értékre leképez, pl. `{"Period": 14, "Label": "trend"}`. Futtatás/backteszt idején a cTrader `params.cbotset` fájlra konvertálódik (`{ "Parameters": { … } }`). A cBot **Parameter sets** (Paraméter készletek) párbeszédpaneléből vagy a Run párbeszédpanelből inline hozhat létre/szerkeszthet készletet.

Minden paraméter készlet **egy cBothoz tartozik**: az New Parameter Set (Új paraméter készlet) párbeszédpanel felsorolja az összes cBotot, és **kötelező kijelölni egyet** — a létrehozás blokkolt, amíg a cBot nincs kijelölve. Egy készlet **neve egyedi cBotonként**: egy készletet egy olyan névre létrehozni vagy átnevezni, amelyet az ugyanazon cBot másik készlete már használ, elutasított (egyértelmű hiba a párbeszédpanelen, `409 Conflict` az API-ban). Ugyanez a név újrahasznosítható egy **másik** cBoten.

A JSON **validálva** mentéskor: ez egy olyan egyedi sima objektumnak kell lennie, amelynek értékei skalár (string / szám / bool). A nem objektum gyökér, egy tömb, egy beágyazott objektum, egy `null` érték vagy rosszul formázott JSON elutasított (egyértelmű hiba a párbeszédpanelen, `400 Bad Request` az API-ban). Az üres objektum `{}` megengedett és azt jelenti, hogy "nincs felülbírálás".

## cTrader Console CLI megjegyzések

A backtesztekhez `--data-mode` (alapértelmezés `m1`), dátumok `dd/MM/yyyy HH:mm` formátumban és `params.cbotset` JSON pozicionális argumentum; a `run` elutasítja a `--data-dir` (csak backteszt). Lásd a `ContainerCommandHelpers` szakaszt.

## Csomópontok és skálázás

A végrehajtás kapacitása csomópont ügynökök hozzáadásával skálázható (önregesztráció + szívverés). Lásd a [node discovery](../operations/node-discovery.md) és a [scaling](../deployment/scaling.md) szakaszokat.

## Kereskedési fiók szükséges

A cBot futtatásához vagy backteszteléséhez szükséges egy cTrader kereskedési fiók a csatlakozásához. Amíg nem ad hozzá egyet a **Trading accounts** (Kereskedési fiókok) alatt, a **Run New cBot** / **Backtest New cBot** (Új cBot futtatása / Új cBot backtesztelése) gombok le vannak tiltva (egy tippel), az oldal pedig egy felhívást mutat a fiók beállítási linkjével — már nem találkozik nyers `stream connect failed` hibával egy fiók nélküli bothoz.
