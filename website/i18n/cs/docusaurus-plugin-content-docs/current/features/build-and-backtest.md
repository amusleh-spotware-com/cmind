---
description: "Vytvářejte, spouštějte a testujte cTrader cBoty (C# a Python, oba na .NET) z prohlížeče Monaco IDE, spouštějte na oficiální imagi ghcr.io/spotware/ctrader-console."
---

# Výstavba a backtest cBotů

Vytvářejte, spouštějte a testujte cTrader cBoty (C# **a** Python, oba na .NET) z prohlížeče Monaco
IDE a spouštějte je na oficiální imagi `ghcr.io/spotware/ctrader-console`.

## Výstavba

- Stránka **Builder** hostuje editor Monaco; `CBotBuilder` kompiluje projekt s
  `dotnet build` **v dočasném kontejneru** (`AppOptions.BuildImage`, pracovní adresář připojen
  v `/work`), takže nedůvěryhodné MSBuild cíle uživatele nemohou dosáhnout na hostitele. NuGet restore je cache-ován
  mezi sestaveními přes sdílený svazek. Webový hostitel potřebuje přístup k Docker socketu.
- Šablony C# a Python startovacích projektů se nacházejí v `src/Nodes/Builder/Templates/`.

## Spuštění a backtest

- **Instances** = TPH stavová hierarchie (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Přechod nahrazuje entitu (změna id),
  id kontejneru je přenesen.
- `NodeScheduler` vybere nejméně zatížený vhodný uzel; `ContainerDispatcherFactory` směruje na
  vzdálený uzel HTTP agent nebo lokální Docker dispatcher.
- Completion pollery sjednocují ukončené kontejnery (backtest kontejnery se samy ukončují přes
  `--exit-on-stop`); zpráva přítomna → dokončena (uloží `ReportJson`), chybí → selhala.
- Živé logy kontejneru se streamují do prohlížeče přes SignalR; equity křivky backtestu jsou analyzovány ze
  zprávy a znázorněny.

## Backtest tržní data jsou cache-ována na účet

Console cTrader stahuje historická tick/bar data do svého `--data-dir`. Tento adresář je
**stabilní, trvalá cache klíčovaná na obchodním účtu** (její číslo účtu) — připojena z
disku uzlu na své vlastní cestě kontejneru (`/mnt/data`), **samostatné, vnořené pripojení** z
adresáře práce na jednotlivou instanci. Takže každý backtest na stejném účtu **znovu využívá** již
stažená data místo jejich opětovného stažení každého spuštění. (Dříve byl
data adresář umístěn pod adresářem práce na jednotlivou instanci, jehož id se mění při každém spuštění, což vynucovalo nové
stažení při každém backtestu.) Ephemeralní adresář práce na jednotlivou instanci stále obsahuje algo, parametry, heslo
a zprávu; sdílená data cache se počítají v backtest-data použití uzlu a jsou vymazány
akcí node-clean.

## Nastavení backtestu

Dialog **Backtest** odhaluje uživatelem laditelné nastavení backtestu cTrader Console, takže nikdy nebudete muset
dotýkat se příkazové řádky:

- **Symbol / Timeframe** — timeframe je **seznam všech cTrader období** (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1` a období Renko/Range/Heikin), v
  kanonickém psaní konzoly, takže vždy vybíráte platný `--period`.
- **From / To** — backtest okno (`--start` / `--end`).
- **Data mode** — jeden ze tří režimů cTrader (`--data-mode`): **Tick data** (`tick`, přesný),
  **m1 bars** (`m1`, rychlý), nebo **Open prices only** (`open`, nejrychlejší).
- **Starting balance** — výchozí je `10000` (`--balance`). **0 bilance neumožňuje žádné obchody a způsobí, že
  cTrader vydá prázdnou zprávu, na které se pak zhroutí** ("Message expected"), takže se vždy odesílá nenulová bilance.
- **Commission** a **Spread** — `--commission` / `--spread` (spread v pipech).

Adresář dat (`--data-file` / `--data-dir`) je spravován samotnou aplikací (cache na účet, viz
výše), není objeven v dialogu.

## Stránka detailů instance

Otevření instance (`/instance/{id}`) zobrazuje její aktuální stav, logy a — pro backtest — equity
křivku. **Název karty prohlížeče** odráží konkrétní instanci (**jméno cBotu · typ · symbol**, např.
`TrendBot · Backtest · EURUSD`), takže kartu live-run a kartu backtestu lze rozlišit na první pohled.
Run a backtest stejného cBotu jsou sledovány jako odlišné **lineáže** (stabilní id lineáže přeneseno
v průběhu přechodů stavů), takže stránka sleduje přesně jednu instanci a nikdy neměší data z běhu s
backtest daty.

## Ovládací prvky životního cyklu instance

Každý řádek instance (a jeho stránka detailů) má správné ovládací prvky stavu. **Aktivní** instance zobrazuje
**Stop**; **terminální** (Stopped / Completed / Failed) zobrazuje **Start (▶)** pro opětovné spuštění se
stejným cBotem, účtem, symbolem, timeframe, sadou parametrů a imagí (běh se restartuje jako běh, backtest jako backtest). Kliknutí Stop zobrazuje "Stopping…" oznamem a zakáže ikonu, dokud se to nevyřeší, a nově vytvořený běh se
okamžitě objeví v seznamu — bez obnovení stránky.

Logy konzoly jsou **trvale uloženy, když se instance ukončí** — pro běh (na Stop) a pro
**backtest** (po dokončení) — takže logy posledního běhu zůstávají viditelné na stránce detailů a,
přes panel nástrojů logu, **kopírovány do schránky** (ikona Kopírovat logy) nebo **staženy** (ikona Stáhnout logy)
i po vymazání kontejneru. Obě fungují na úplném logu konzoly instance, ne jen na zobrazeném chvostě.

Nahraný `.algo` zde nikdy nebyl sestaven, takže jeho sloupec **Last Build** na stránce cBotů zůstane
prázdný (zobrazuje čas sestaven pouze pro cBoty, které zde vytváříte v prohlížeči).

## Úprava a opětovné spuštění zastavené instance

**Zastavená** instance (běh nebo backtest) má ovládací prvek **Edit** — ikona na jejím řádku v seznamu **a**
vedle Start/Stop na jeho stránce detailů — která otevírá dialog **předvyplněný** s její aktuální konfigurací.
Můžete změnit **obchodní účet, symbol, timeframe, sadu parametrů a značku image** (a pro
backtest, **okno a všechna nastavení backtestu** výše), pak **Save & start** ji restartuje s
novými nastaveními (nahrazuje zastavenému instanci). Ovládací prvek je **zakázán, zatímco je instance aktivní** —
pouze zastavená instance se dá upravit.

## Spuštění z editoru kódu

Kliknutí na **Run** v editoru kódu otevírá dialog místo spuštění slepého, hardkódovaného běhu:

- **Trading account** (povinné) — obchodní účet cTrader, ke kterému se cBot připojuje.
- **Parameter set** (volitelné) — vyberte existující sadu, nebo ji ponechte prázdnou pro spuštění s cBotem
  **výchozí hodnoty parametrů**. Tlačítko **+** vedle selektoru vytvoří novou sadu parametrů
  inline (viz níže) a vybere ji.
- **Symbol / Timeframe** se defaultují na `EURUSD` / `h1` a lze je změnit; **Cancel** nebo **Run**.

Na **Run** editor uloží + sestaví aktuální zdroj, spustí instanci na zvoleném účtu
s vybranými parametry a pak sleduje živé logy kontejneru. (Stream logu předá
ověřený cookie auth uživatele do `/hubs/logs` SignalR hubu, takže se připojí místo selhání s
`Invalid negotiation response received`.)

## Sady parametrů

**Sada parametrů** je pojmenovaná, znovu použitelná sada přepsaných parametrů cBotu uložená jako plochý JSON
objekt mapující každý název parametru na skalární hodnotu, např. `{"Period": 14, "Label": "trend"}`. V
čase spuštění/backtestu se změní na cTrader soubor `params.cbotset`
(`{ "Parameters": { … } }`). Můžete vytvářet/upravovat sadu jako surový JSON z dialogu **Parameter
sets** cBotu nebo inline z dialogu Run.

Každá sada parametrů **patří cBotu**: dialog New Parameter Set vypíše všechny vaše cBoty a vy
**musíte vybrat jeden** — vytvoření je blokováno, dokud není cBot vybrán. **Jméno sady je jedinečné pro cBot**:
vytvoření nebo přejmenování sady na název, který již používá jiná sada stejného cBotu, je odmítnuto (jasná
chyba v dialogu, `409 Conflict` v API). Stejné jméno může být znovu použito na **jiný** cBot.

JSON je **ověřen** při uložení: musí to být jediný plochý objekt, jehož hodnoty jsou všechny skalární
(řetězec / číslo / bool). Non-objekt root, pole, vnořený objekt, `null` hodnota, nebo chybný
JSON je odmítnut (jasná chyba v dialogu, `400 Bad Request` v API). Prázdný objekt `{}`
je povolen a znamená "bez přepsání".

## Poznámky k CLI cTrader Console

Backtest vyžaduje `--data-mode` (výchozí `m1`), data jako `dd/MM/yyyy HH:mm` a
`params.cbotset` JSON poziční argument; `run` odmítá `--data-dir` (pouze backtest). Viz
`ContainerCommandHelpers`.

## Uzly a škálování

Kapacita spuštění se zvyšuje přidáváním agentů uzlů (vlastní registrace + heartbeat). Viz
[node discovery](../operations/node-discovery.md) a [scaling](../deployment/scaling.md).

## Obchodní účet je vyžadován

Spuštění nebo backtest cBotu vyžaduje obchodní účet cTrader, aby se k němu připojil. Dokud
nepřidáte nějaký pod **Trading accounts**, tlačítka **Run New cBot** / **Backtest New cBot** jsou
zakázána (s tipu) a stránka zobrazuje výzvu odkazující na nastavení účtu — již nenarazíte na syrový
`stream connect failed` error od bota bez účtu.
