---
description: "Postavte, spusťte a otestujte cTrader cBoty (C# a Python, oba .NET) z integrovaného editoru Monaco v prohlížeči, spouštějte na oficiální imagi ghcr.io/spotware/ctrader-console."
---

# Build & backtest cBots

Postavte, spusťte a otestujte cTrader cBoty (C# **a** Python, oba .NET) z integrovaného editoru
Monaco v prohlížeči, spouštějte na oficiální imagi `ghcr.io/spotware/ctrader-console`.

## Build

- **Builder** stránka hostuje editor Monaco; `CBotBuilder` kompiluje projekt pomocí
  `dotnet build` **v jednorázovém kontejneru** (`AppOptions.BuildImage`, pracovní adresář bind-mountem
  na `/work`), aby nedůvěryhodné MSBuild cíle uživatele nedosáhly hostitele. Obnovení NuGet je cachováno
  mezi sestaveními prostřednictvím sdíleného svazku. Webový hostitel potřebuje přístup k Docker soketu.
- Startovací šablony C# a Python se nacházejí v `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = TPH hierarchie stavů (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Přechod nahradí entitu (změna id),
  id kontejneru se zachovává.
- `NodeScheduler` vybere nejméně zatížený oprávněný uzel; `ContainerDispatcherFactory` směruje na
  vzdálený HTTP agent uzlu nebo místní Docker dispatcher.
- Pollers dokončení sladí ukončené kontejnery (kontejnery backtestů se sami ukončují přes
  `--exit-on-stop`); zpráva přítomna → dokončeno (uložit `ReportJson`), chybí → selhalo.
- Živé protokoly kontejneru se streamují do prohlížeče přes SignalR; křivky vlastního kapitálu backtestů se analyzují ze
  zprávy a vykreslují.

## Backtest market data is cached per account

Konzola cTrader stahuje historická data tick/bar do svého `--data-dir`. Tento adresář je
**stabilní, perzistentní mezipaměť klíčovaná obchodním účtem** (jeho číslo účtu) — bind-mountem z disku
uzlu na jeho vlastní cestu kontejneru (`/mnt/data`), **samostatný, vnořený mount** od
adresáře práce pro jednotlivé instance. Takže každý backtest na stejném účtu **znovu používá** již stažená data
místo jejich opětovného stahování při každém běhu. (Dříve byl
adresář dat umístěn v adresáři práce pro jednotlivé instance, jehož id se mění při každém běhu, což nuceně způsobilo
stažení při každém backtestování.) Efemérní pracovní adresář pro jednotlivé instance stále obsahuje algoritmus, parametry, heslo
a zprávu; sdílená mezipaměť dat se počítá do využití backtestovacích dat uzlu a vymazává se akcí čištění uzlu.

## Backtest settings

Dialog **Backtest** zpřístupňuje každé nastavení, které konzola cTrader backtest CLI přijímá, takže nikdy
nemusíte dotýkat příkazního řádku:

- **From / To** — okno backtestování (`--start` / `--end`).
- **Data mode** — jeden ze tří režimů cTrader (`--data-mode`): **Tick data** (`tick`, přesné),
  **m1 bars** (`m1`, rychlé), nebo **Open prices only** (`open`, nejrychlejší).
- **Starting balance** — výchozí `10000` (`--balance`). **Zůstatek 0 umístí bez obchodů a způsobí, že
  cTrader vygeneruje prázdnou zprávu, na které pak havaruje** ("Message expected"), takže je vždy poslán
  nenulový zůstatek.
- **Commission** a **Spread** — `--commission` / `--spread` (rozpětí v pipech).
- **Data file** (volitelně) — cesta na straně uzlu k historickému datovému souboru (`--data-file`); nechte prázdné pro
  použití stažených/cachovaných dat.
- **Expose environment variables** — přepínač, který předá proměnné prostředí hostitele cBotu
  (příznak `--environment-variables`).

## Instance detail page

Otevření instance (`/instance/{id}`) zobrazuje její živý stav, protokoly a — pro backtest — křivku
vlastního kapitálu. **Název tabulátoru prohlížeče** odráží konkrétní instanci (**jméno cBotu · typ · symbol**, např.
`TrendBot · Backtest · EURUSD`), takže záložka živého běhu a záložka backtestování jsou rozlišitelné na první pohled.
Běh a backtest stejného cBotu se sledují jako odlišné **linie** (stabilní id linie přenesené
přes přechody stavů), takže stránka sleduje přesně jednu instanci a nikdy nesměšuje data běhu s
backtestováním.

## Instance lifecycle controls

Každý řádek instance (a jeho stránka podrobností) má ovládací prvky správné pro svůj stav. **Aktivní** instance zobrazuje
**Stop**; **terminální** (Stopped / Completed / Failed) zobrazuje **Start (▶)** k opětovnému spuštění stejného cBotu, účtu, symbolu, timeframu, sady parametrů a image (běh se restartuje jako běh, backtest jako backtest). Kliknutí na Stop zobrazuje "Stopping…" upozornění a zakáže ikonu, dokud se nerozřeší, a nově vytvořený běh se okamžitě objeví v seznamu — bez obnovení stránky.

Protokoly konzoly jsou **trvalé, když se instance ukončí** — pro běh (při zastavení) a pro
**backtest** (po dokončení) — takže protokoly posledního běhu zůstávají viditelné na stránce podrobností a,
přes panel nástrojů protokolu, **zkopírovány do schránky** (ikona Kopírovat protokoly) nebo **staženy** (ikona Stáhnout protokoly)
i poté, co je kontejner pryč. Oba působí na úplný protokol konzoly instance, ne jen na
viditelné kousky.

Nahraný soubor `.algo` nebyl zde nikdy sestaven, takže jeho sloupec **Last Build** na stránce cBots zůstává
prázdný (zobrazuje čas sestavení pouze pro cBoty, které jste vytvořili v prohlížeči).

## Edit & re-run a stopped instance

**Zastavená** instance (běh nebo backtest) má ovládací prvek **Edit** — ikonu na řádku v seznamu **a**
vedle Start/Stop na stránce podrobností — která otevírá dialog **předvyplněný** její aktuální konfigurací.
Můžete změnit **obchodní účet, symbol, timeframe, sadu parametrů a značku image** (a pro
backtest i **okno a všechna nastavení backtestování** výše), pak **Save & start** ji restartuje s
novými nastaveními (nahradí zastavené instance). Ovládací prvek je **zakázán, když je instance aktivní** —
pouze zastavená instance se dá upravit.

## Run from the code editor

Kliknutí na **Run** v editoru kódu otevře dialog místo spuštění slepého, pevného kódovaného běhu:

- **Trading account** (povinný) — obchodní účet cTrader, ke kterému se cBot připojí.
- **Parameter set** (volitelně) — vyberte existující sadu, nebo ji ponechte prázdnou pro běh s **výchozími hodnotami parametrů** cBotu. Tlačítko **+** vedle selektoru vytvoří novou sadu parametrů
  inline (viz níže) a vybere ji.
- **Symbol / Timeframe** se výchozím nastavením na `EURUSD` / `h1` a lze je změnit; **Cancel** nebo **Run**.

Při kliknutí na **Run** editor uloží + sestaví aktuální zdroj, spustí instanci na vybraném účtu
s vybranými parametry, pak sleduje živé protokoly kontejneru. (Proud protokolu předá ověřovací soubor cookie
přihlášeného uživatele rozbočovači SignalR `/hubs/logs`, takže se připojí místo selhání s
`Invalid negotiation response received`.)

## Parameter sets

**Sada parametrů** je pojmenovaná, opakovaně použitelná sada přepsání parametrů cBotu uložená jako plochý JSON
objekt mapující každý název parametru na skalární hodnotu, např. `{"Period": 14, "Label": "trend"}`. Při
spuštění/backtestování se změní na soubor cTrader `params.cbotset`
(`{ "Parameters": { … } }`). Můžete vytvořit/upravit sadu jako surový JSON z dialogu **Parameter
sets** cBotu nebo inline z dialogu Run.

Každá sada parametrů **patří cBotu**: dialog New Parameter Set vypíše všechny vaše cBoty a vy
**musíte jeden vybrat** — vytvoření je blokováno, dokud není vybrán cBot. **Název sady je jedinečný na cBot**:
vytvoření nebo přejmenování sady na název, který již používá jiná sada stejného cBotu, je odmítnuto (jasná
chyba v dialogu, `409 Conflict` v API). Stejný název lze znovu použít na **jiném** cBotu.

JSON je **ověřen** při uložení: musí to být jeden plochý objekt, jehož hodnoty jsou všechny skalární
(řetězec / číslo / logická hodnota). Nekořenový objekt, pole, vnořený objekt, hodnota `null`, nebo špatně utvořený
JSON je odmítnut (jasná chyba v dialogu, `400 Bad Request` v API). Prázdný objekt `{}`
je povolen a znamená "žádné přepsání".

## cTrader Console CLI notes

Backtesty potřebují `--data-mode` (výchozí `m1`), data jako `dd/MM/yyyy HH:mm`, a
JSON `params.cbotset` poziční argument; `run` odmítá `--data-dir` (pouze pro backtest). Viz
`ContainerCommandHelpers`.

## Nodes & scale

Kapacita provádění se rozšiřuje přidáním agentů uzlů (samořegistrující se a srdeční tep). Viz
[node discovery](../operations/node-discovery.md) a [scaling](../deployment/scaling.md).

## A trading account is required

Spuštění nebo backtestování cBotu vyžaduje obchodní účet cTrader, ke kterému se má připojit. Dokud jej nepřidáte v
**Trading accounts**, jsou tlačítka **Run New cBot** / **Backtest New cBot** zakázána (s
tooltipem) a stránka zobrazuje výzvu s odkazem na nastavení účtu — už se nesetkáte s chybou
`stream connect failed` ze botu bez účtu.
