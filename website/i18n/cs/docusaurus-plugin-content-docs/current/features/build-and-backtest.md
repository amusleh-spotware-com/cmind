---
description: "Vytváření, spuštění, backtesting cTrader cBotů (C# a Python, oba .NET) z prohlížeče s editorem Monaco, spuštění na oficiální imagi ghcr.io/spotware/ctrader-console."
---

# Vytváření a backtesting cBotů

Vytváření, spuštění a backtesting cTrader cBotů (C# **a** Python, oba .NET) z prohlížeče
s editorem Monaco, spuštění na oficiální imagi `ghcr.io/spotware/ctrader-console`.

## Vytváření

- **Builder** stránka hostuje editor Monaco; `CBotBuilder` kompiluje projekt s
  `dotnet build` **v jednorázovém kontejneru** (`AppOptions.BuildImage`, pracovní adresář bind-mount
  na `/work`), takže nedůvěryhodné cílové MSBuild nedosáhnou hostitele. Obnovení NuGet je cachováno
  mezi sestaveními přes sdílený svazek. Webový hostitel potřebuje přístup k soketu Docker.
- Šablony startéru C# a Python jsou v `src/Nodes/Builder/Templates/`.

## Spuštění a backtesting

- **Instance** = hierarchie TPH stavu (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Přechod nahradí entitu (změna id),
  id kontejneru se přenese.
- `NodeScheduler` vybere nejméně zatížený způsobilý uzel; `ContainerDispatcherFactory` směruje na
  vzdálený uzel HTTP agenta nebo místní dispatcher Docker.
- Dokončovací pollery sjednocují ukončené kontejnery (backtest kontejnery se sami ukončují přes
  `--exit-on-stop`); zpráva přítomna → dokončeno (uloží `ReportJson`), chybí → selhalo.
- Živé protokoly kontejneru se streamují do prohlížeče přes SignalR; křivky kapitálu backtestů jsou parsovány ze
  zprávy a vykresleny.

## Data backtestů jsou cachována na účet

Konzola cTrader si stáhne historická data tick/bar do `--data-dir`. Tento adresář je
**stabilní, perzistentní cache seřazený na obchodní účet** (jeho číslo účtu) — bind-mounted z disku
uzlu na jeho vlastní cestu kontejneru (`/mnt/data`), **odděleného, nevnořeného mount** z
pracovního adresáře na jednu instanci. Takže každý backtest na stejném účtu **znovu používá** již
stáhnutá data namísto jejich opětovného stažení v každém běhu. (Dříve
adresář dat žil pod pracovním adresářem na jednu instanci, jeho id se mění v každém běhu, což vynutilo nové
stažení při každém backtestingu.) Efemerní pracovní adresář na jednu instanci stále obsahuje algoritmus, parametry, heslo
a zprávu; sdílená cache dat se počítá v backtest-data využití uzlu a vymazáním akce čištění uzlu.

## Nastavení backtestů

Dialog **Backtest** zpřístupňuje každé nastavení, které rozhraní CLI backtestů konzoly cTrader přijímá, takže nikdy
nemusíte příkazový řádek dotýkat:

- **Od / Do** — okno backtestingu (`--start` / `--end`).
- **Režim dat** — `m1` (1-minutové sloupce) nebo `tick` (`--data-mode`).
- **Počáteční zůstatek** — výchozí `10000` (`--balance`). **Zůstatek 0 umístí žádné obchody a způsobí
  cTrader vyzáří prázdnou zprávu, kterou poté selhá** ("Očekávána zpráva"), takže nenulový zůstatek se
  vždy odešle.
- **Provize** a **Spread** (`--commission` / `--spread`, spread v pipech).
- **Pokročilé možnosti** — pole pro volnou formu `name=value` na řádek pro jakoukoli jinou backtest-možnost, kterou cTrader
  podporuje (např. `applyCommissionAutomatically=true`); každý řádek se stane `--name value` CLI argumentem.

## Stránka podrobností instance

Otevření instance (`/instance/{id}`) zobrazuje jejího živého stav, protokoly a — pro backtest — křivku kapitálu.
Název **karty prohlížeče** odráží konkrétní instanci (**název cBotu · druh · symbol**, např.
`TrendBot · Backtest · EURUSD`), takže se rozlišuje živý běh a karta backtestingu na první pohled.
Běh a backtest stejného cBotu jsou sledovány jako odlišné **linie** (stabilní ID linie přenášené
přes přechody stavu), takže stránka sleduje přesně jednu instanci a nikdy nemichá data běhu s
backtestingem.

## Ovládací prvky životního cyklu instance

Každý řádek instance (a jeho stránka s podrobnostmi) má stavy-správné ovládací prvky. **Aktivní** instance ukazuje
**Stop**; **terminální** jeden (Stopped / Completed / Failed) ukazuje **Start (▶)** k jeho opětovnému spuštění se
stejným cBotem, účtem, symbolem, časovým rámcem, sadou parametrů a imagí (běh se restartuje jako běh, backtest
jako backtest). Kliknutí na Stop zobrazí upozornění "Zastavování..." a zakáže ikonu, dokud se nevyřeší, a nově
vytvořený běh se ihned objeví v seznamu — bez přeložení stránky.

Protokoly konzoly jsou **perzistentní, když instance skončí** — pro běh (na Stop) a pro
**backtest** (po dokončení) stejně — takže protokoly posledního běhu zůstanou viditelné na stránce s podrobnostmi a,
přes panel nástrojů protokolu, **zkopíruje do schránky** (ikona Kopírovat protokoly) nebo **stahuje** (ikona Stáhnout protokoly)
i poté, co kontejner zmizí. Oba jednají na úplném protokolu konzoly instance, ne jen na
viditelném ocasu.

Nahrané `.algo` nikdy nebylo zde postaveno, takže jeho **Poslední sestava** sloupec na stránce cBotů je
prázdný (zobrazuje čas sestavení pouze pro cBoty, které tady v prohlížeči postavíte).

## Úprava a opětovné spuštění zastavené instance

**Zastavená** instance (běh nebo backtest) má ovládací prvek **Úprava** — ikona na jejím řádku v seznamu **a**
vedle Start/Stop na její stránce s podrobnostmi — která otevře dialog **předvyplněný** s její aktuální konfigurací.
Můžete změnit **obchodní účet, symbol, časový rámec, sadu parametrů a tag obrázku** (a pro
backtest, **okno a všechna výše uvedená nastavení backtestů**), pak **Uložit a spustit** jej znovu spustí s
novými nastaveními (nahradí zastavené instance). Ovládací prvek je **zakázán, když je instance aktivní** —
pouze zastavená instance může být upravena.

## Spuštění z editoru kódu

Kliknutí na **Run** v editoru kódu otevře dialog namísto spouštění slepého, pevně zakódovaného běhu:

- **Obchodní účet** (povinné) — účet cTrader, ke kterému se cBot připojuje.
- **Sada parametrů** (volitelné) — vyberte existující sadu, nebo ji nechte prázdnou, abyste běželi s **výchozími hodnotami parametrů** cBotu.
  Tlačítko **+** vedle voliče vytvoří novou sadu parametrů
  inline (viz níže) a vybere ji.
- **Symbol / Časový rámec** je výchozí `EURUSD` / `h1` a lze jej změnit; **Zrušit** nebo **Spustit**.

Na **Spustit** editor uloží + postaví aktuální zdroj, spustí instanci na zvoleném účtu
se zvolenými parametry, poté sleduje živé protokoly kontejneru. (Proud protokolu předá ověřený
ověřovací soubor cookie uživatele do centra SignalR `/hubs/logs`, takže se připojí místo selhání s
`Invalid negotiation response received`.)

## Sady parametrů

**Sada parametrů** je pojmenovaná, opakovaně použitelná sada přepsání parametrů cBotu uložená jako ploché JSON
objektu mapující každý název parametru na skalární hodnotu, např. `{"Period": 14, "Label": "trend"}`. V běhu/backtestingu se
změní na soubor cTrader `params.cbotset`
(`{ "Parameters": { … } }`). Můžete vytvořit/upravit sadu jako surové JSON z dialogu **Sady parametrů** cBotu nebo
inline z Run dialogu.

Každá sada parametrů **patří cBotu**: dialog New Parameter Set vypíše všechny vaše cBoty a musíte
**vybrat jeden** — vytvoření je zablokováno, dokud není vybrán cBot. **Název** sady je **jedinečný na cBot**:
vytvoření nebo přejmenování sady na název, který již sada stejného cBotu používá, je odmítáno (jasná
chyba v dialogu, `409 Conflict` v API). Stejný název lze znovu použít na **jiném** cBotu.

JSON je **ověřen** na uložení: musí to být jeden plochý objekt, jehož hodnoty jsou všechny skalární
(řetězec / číslo / logická). Neobektový kořen, pole, vnořený objekt, `null` hodnota, nebo
poškozené JSON je odmítáno (jasná chyba v dialogu, `400 Bad Request` v API). Prázdný objekt `{}`
je povolen a znamená "bez přepsaní".

## Poznámky k CLI konzoly cTrader

Backtesty potřebují `--data-mode` (výchozí `m1`), data jako `dd/MM/yyyy HH:mm`, a
`params.cbotset` JSON poziční arg; `run` odmítá `--data-dir` (pouze backtest). Viz
`ContainerCommandHelpers`.

## Uzly a měřítko

Kapacita spuštění se zvětšuje přidáváním agentů uzlů (sebe-registrace + heartbeat). Viz
[objevování uzlů](../operations/node-discovery.md) a [škálování](../deployment/scaling.md).

## Vyžaduje se obchodní účet

Spuštění nebo backtesting cBotu vyžaduje obchodní účet cTrader pro připojení. Dokud jej nepřidáte pod
**Obchodní účty**, tlačítka **Spustit nový cBot** / **Backtest nový cBot** jsou zakázána (s
popisem) a stránka zobrazuje výzvu s odkazem na nastavení účtu — už se nesetkáte s chybou
`stream connect failed` od botu bez účtu.
