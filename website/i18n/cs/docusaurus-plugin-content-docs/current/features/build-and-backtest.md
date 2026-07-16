---
description: "Stavěte, spouštějte a testujte cTrader cBoty (C# a Python, oba na .NET) z prohlížeče Monaco IDE, spouštějte na oficiální imagi ghcr.io/spotware/ctrader-console."
---

# Stavba a backtesting cBotů

Stavěte, spouštějte a testujte cTrader cBoty (C# **a** Python, oba na .NET) z prohlížeče Monaco IDE, spouštějte na oficiální imagi `ghcr.io/spotware/ctrader-console`.

## Stavba

- Stránka **Builder** hostuje editor Monaco; `CBotBuilder` kompiluje projekt pomocí `dotnet build` **v jednorázovém kontejneru** (`AppOptions.BuildImage`, adresář práce připojený jako bind-mount na `/work`), aby nedůvěryhodné uživatelské MSBuild cíle nedosáhly hostitele. Obnova NuGet je cachována v různých buildrech přes sdílený svazek. Webový hostitel potřebuje přístup k Docker socketu.
- Počáteční šablony pro C# i Python se nacházejí v `src/Nodes/Builder/Templates/`.

## Spouštění a backtesting

- **Instance** = hierarchie TPH stavů (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). Přechod nahrazuje entitu (změna id), id kontejneru se přenáší.
- `NodeScheduler` vybere nejméně zatížený způsobilý uzel; `ContainerDispatcherFactory` směruje na vzdálený uzel HTTP agenta nebo místní dispatcher Docker.
- Konečné pollery sjednocují ukončené kontejnery (backtestovací kontejnery se samy ukončí přes `--exit-on-stop`); zpráva přítomná → dokončená (uloží `ReportJson`), chybí → selhala.
- Živé protokoly kontejneru se streamují do prohlížeče přes SignalR; equity křivky z backtestů jsou parsovány z reportů a graficky znázorněny.

## Data pro backtesting jsou cachována na účet

cTrader Console si stáhne historické tick/bar data do svého adresáře `--data-dir`. Tento adresář je **stabilní, trvalá mezipaměť klíčovaná obchodním účtem** (podle jeho čísla účtu) — připojena jako bind-mount z disku uzlu do jeho vlastní cesty kontejneru (`/mnt/data`), **samostatný, ne-vnořený mount** z adresáře práce na instanci. Každý backtest na stejném účtu tedy **znovu používá** již stažená data místo jejich nového stahování při každém spuštění. (Dříve se adresář dat nacházel pod adresářem práce na instanci, jehož id se mění při každém spuštění, což vynutilo čerstvé stahování při každém backtestování.) Dočasný adresář práce na instanci stále obsahuje algo, parametry, heslo a report; sdílená mezipaměť dat se počítá do backtest-data využití uzlu a vyčistí se akcí node-clean.

## Nastavení backtestingu

Dialog **Backtest** vystavuje uživatelem nastavitelné nastavení backtestingu cTrader Console, takže se nikdy nemusíte dotýkat příkazového řádku:

- **Symbol / Timeframe** — timeframe je **rozevírací seznam každé cTrader periody** (`t1`…`t1000`, `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1` a Renko/Range/Heikin periody), v kánonu konzoly psané tak, aby jste vždy vybrali platný `--period`.
- **From / To** — okno backtestingu (`--start` / `--end`).
- **Data mode** — jeden ze tří cTrader režimů (`--data-mode`): **Tick data** (`tick`, přesné), **m1 bars** (`m1`, rychlé), nebo **Open prices only** (`open`, nejrychlejší).
- **Starting balance** — výchozí nastavení `10000` (`--balance`). **Zůstatek 0 neumožňuje žádné obchody a způsobí, že cTrader vydá prázdný report, na kterém se pak zhroutí** ("Message expected"), takže je vždy poslán nenulový zůstatek.
- **Commission** — `--commission`.
- **Spread** — `--spread`, **numerické pole v pipech, které nemůže jít pod 0**. Je **skryto v režimu Tick data**, kde cTrader odvozuje spread z dat ticků samotných (není poslán `--spread`).

Adresář dat (`--data-file` / `--data-dir`) je spravován samotnou aplikací (cache na účet, viz výše), není vystavena v dialogu.

:::note cTrader se zhroutí na prázdném backtestingu
Pokud backtest vyprodukuje **žádné výsledky** — žádné obchody nebo žádná tržní data pro zvolená data/symbol — writer reportu cTrader Console sám vyvolá `Message expected` a skončí bez reportu. Aplikace nemůže opravit tuto upstream chybu, ale detekuje ji a označí instanci jako **Failed** s vysvětlujícím důvodem ("no backtest results for the selected range…") místo surového stacktrace. Vyberte širší rozsah dat s dostupnými tržními daty a zkuste znovu.
:::

## Stránka detailu instance

Otevření instance (`/instance/{id}`) zobrazuje její aktuální stav, protokoly a — pro backtest — equity křivku. **Název karty prohlížeče** odráží konkrétní instanci (**název cBotu · druh · symbol**, např. `TrendBot · Backtest · EURUSD`), takže živé běhové kartě a backtestovací kartě se lze snadno rozlišit na první pohled. Běh a backtest stejného cBotu jsou sledovány jako samostatné **linie** (stabilní id linie přenesené přes přechody stavů), takže stránka následuje přesně jednu instanci a nikdy nemíchá data běhu s backtest daty.

## Ovládací prvky životního cyklu instance

Každý řádek instance (a její stránka detailu) má ovládací prvky správné pro daný stav. **Aktivní** instance ukazuje **Stop**; **terminální** (`Stopped` / `Completed` / `Failed`) ukazuje **Start (▶)**, aby ji znovu spustila se stejným cBotem, účtem, symbolem, timeframe, parametr set a imagí (běh se restartuje jako běh, backtest jako backtest). Kliknutí na Stop zobrazuje oznámení "Stopping…" a zakáže ikonu, dokud se nevyřeší, a nově vytvořený běh se okamžitě objeví v seznamu — bez znovunačtení stránky.

Protokoly konzoly se **uchují, když se instance ukončí** — pro běh (na Stop) i pro **backtest** (po dokončení) — takže logyy posledního běhu zůstanou viditelné na stránce detailu a prostřednictvím panelu nástrojů logu se dají **zkopírovat do schránky** (ikona Kopírovat protokoly) nebo **stáhnout** (ikona Stáhnout protokoly) i poté, co je kontejner pryč. Obě akce fungují na celém protokolu konzoly instance, ne jen na viditelném ocasu.

**Dokončený backtest** také zachovává svůj **cTrader report** v obou formátech — surový **JSON** (stejný, který čtou equity křivka a AI analýza) a úplný **HTML** report. Oba se dají stáhnout z řádku backtestingu **a** ze stránky detailu přes vyhrazené ikony. Uchovávají se pouze **logyy posledního běhu**, a ikony jsou **zakázány** pro jakýkoli backtest, který není spuštěný, běží nebo selhal (a nikdy se nezobrazují pro instanci běhu) — pouze dokončený backtest má report ke stažení.

Nahraný `.algo` nebyl nikdy postaven zde, takže jeho sloupec **Last Build** na stránce cBots je prázdný (zobrazuje čas buildu pouze pro cBoty, které tvoříte v prohlížeči).

## Úprava a opětovné spuštění zastavené instance

**Zastavená** instance (běh nebo backtest) má ovládací prvek **Edit** — ikona na jejím řádku v seznamu **a** vedle Start/Stop na její stránce detailu — která otevře dialog **předvyplněný** její aktuální konfigurací. Můžete změnit **obchodní účet, symbol, timeframe, parametr set a tag image** (a pro backtest také **okno a všechna nastavení backtestingu** výše), pak **Save & start** ji znovu spustí s novými nastavením (nahrazuje zastavenout instanci). Ovládací prvek je **zakázán, zatímco je instance aktivní** — pouze zastavená instance se dá upravit.

## Spuštění z editoru kódu

Kliknutí na **Run** v editoru kódu otevře dialog místo slepého, pevně zakódovaného spuštění:

- **Trading account** (povinné) — cTrader účet, ke kterému se cBot připojí.
- **Parameter set** (volitelné) — vyberte existující sadu nebo ji nechte prázdnou, aby se spustila s **výchozími hodnotami parametrů** cBotu. Tlačítko **+** vedle selektoru vytvoří novou sadu parametrů inline (viz níže) a vybere ji.
- **Symbol / Timeframe** default na `EURUSD` / `h1` a lze je změnit; **Cancel** nebo **Run**.

Při kliknutí na **Run** se editor uloží + postaví aktuální zdroj, spustí instanci na zvoleném účtu se zvolenými parametry a pak tailuje živé protokoly kontejneru. (Stream protokolu předá cookie autentifikace přihlášeného uživatele do centra SignalR `/hubs/logs`, takže se připojí místo selhání s `Invalid negotiation response received`.)

## Sady parametrů

**Sada parametrů** je pojmenovaná, opakovaně použitelná sada přepisů parametrů cBotu uložená jako plochý objekt JSON mapující každý název parametru na skalární hodnotu, např. `{"Period": 14, "Label": "trend"}`. V čase spuštění/backtestingu se změní na soubor cTrader `params.cbotset` (`{ "Parameters": { … } }`). Sadu můžete vytvořit/upravit jako surový JSON z dialogu **Parameter sets** cBotu nebo inline z dialogu Run.

Každá sada parametrů **patří do cBotu**: dialog New Parameter Set obsahuje seznam všech vašich cBotů a **musíte si jeden vybrat** — vytvoření je blokováno, dokud není vybrán cBot. **Název sady je jedinečný pro cBot**: vytvoření nebo přejmenování sady na název, který již používá jiná sada stejného cBotu, je odmítnuto (jasná chyba v dialogu, `409 Conflict` v API). Stejný název se dá znovu použít na **jiném** cBotu.

JSON je **ověřen** při uložení: musí být jeden plochý objekt, jehož hodnoty jsou všechny skaláry (řetězec / číslo / bool). Non-object root, pole, vnořený objekt, `null` hodnota nebo chybně formátovaný JSON je odmítnut (jasná chyba v dialogu, `400 Bad Request` v API). Prázdný objekt `{}` je povolen a znamená "žádné přepsání".

## Poznámky CLI cTrader Console

Backtesty potřebují `--data-mode` (výchozí `m1`), data jako `dd/MM/yyyy HH:mm` a `params.cbotset` JSON poziční arg; `run` odmítá `--data-dir` (pouze backtest). Podívejte se na `ContainerCommandHelpers`.

## Uzly a měřítko

Kapacita spuštění se zvyšuje přidáním agentů uzlů (automatické registrační a heartbeat). Podívejte se na [node discovery](../operations/node-discovery.md) a [scaling](../deployment/scaling.md).

## Obchodní účet je povinný

Spouštění nebo backtesting cBotu vyžaduje obchodní účet cTrader, aby se k němu připojil. Dokud nepřidáte jeden pod **Trading accounts**, jsou tlačítka **Run New cBot** / **Backtest New cBot** zakázána (s tooltipem) a stránka zobrazuje výzvu s odkazem na nastavení účtu — už nenarazíte na surový `stream connect failed` chyba od bota bez účtu.
