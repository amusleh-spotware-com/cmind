---
description: "Vytvářejte, spouštějte a testujte cBoty cTrader (C# a Python, oba .NET) z webového editoru Monaco, spouštělejte na oficiálním obrázku ghcr.io/spotware/ctrader-console."
---

# Vytváření a testování cBotů

Vytvářejte, spouštějte a testujte cBoty cTrader (C# **a** Python, oba .NET) z webového editoru
Monaco, spouštělejte na oficiálním obrázku `ghcr.io/spotware/ctrader-console`.

## Vytváření

- **Stránka Builder** hostuje editor Monaco; `CBotBuilder` kompiluje projekt s
  `dotnet build` **v dočasném kontejneru** (`AppOptions.BuildImage`, pracovní adresář bind-mount
  na `/work`), aby nedůvěryhodné MSBuild cíle uživatele nedosáhly na hostitele. Obnovení NuGetu je ukládáno
  do mezipaměti v různých sestaveních prostřednictvím sdíleného svazku. Webový hostitel potřebuje přístup k soketu Docker.
- Startovací šablony pro C# a Python se nacházejí v `src/Nodes/Builder/Templates/`.

## Spuštění a testování

- **Instance** = TPH hierarchie stavů (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Přechod nahradí entitu (změna id),
  id kontejneru se přenese.
- `NodeScheduler` vybere nejméně zatížený oprávněný uzel; `ContainerDispatcherFactory` směruje na
  vzdálený HTTP agent uzlu nebo místní dispečer Docker.
- Doplňovací polleři slaďují ukončené kontejnery (testovací kontejnery se sami ukončují přes
  `--exit-on-stop`); zpráva přítomna → dokončeno (uložení `ReportJson`), chybí → selhalo.
- Živé protokoly kontejneru se streamují do prohlížeče přes SignalR; křivky vlastního kapitálu testů se parsují ze
  zprávy a vykreslují.

## Data tržiště testů jsou uložena v mezipaměti pro účet

cTrader Console stáhne historické údaje tick/bar do svého `--data-dir`. Tento adresář je
**stabilní, trvalá mezipaměť klíčovaná podle obchodního účtu** (jeho číslo účtu) — bind-mounted z
disku uzlu na jeho vlastní cestu v kontejneru (`/mnt/data`), **samostatný, vnořený mount** z
jednotlivého pracovního adresáře. Takže každý test na stejném účtu **znovu používá** již stažená data
místo jejich opětovného stažení. (Dříve adresář dat žil pod jednotlivým pracovním adresářem,
jehož id se změnilo každý běh, což vynutilo svěže
stažení každý test.) Dočasný jednotlivý pracovní adresář stále obsahuje algoritmus, parametry, heslo
a zprávu; sdílená mezipaměť dat se počítá v použití backtest-dat uzlu a vymazává akcí
čištění uzlu.

## Nastavení testování

Dialog **Backtest** zpřístupňuje nastavení testu cTrader Console, které lze ladit uživatelem, takže nikdy nemusíte
dotýkat se příkazového řádku:

- **Symbol / Timeframe** — timeframe je **rozbalovací seznam každé cTrader periody** (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1` a periody Renko/Range/Heikin), v
  kanonickém formátu konzoly, takže vždy vyberete platný `--period`.
- **From / To** — okno testu (`--start` / `--end`).
- **Data mode** — jeden ze tří režimů cTrader (`--data-mode`): **Tick data** (`tick`, přesné),
  **m1 bars** (`m1`, rychlé), nebo **Open prices only** (`open`, nejrychlejší).
- **Starting balance** — výchozí `10000` (`--balance`). **Nulový zůstatek neumožňuje žádné obchody a způsobí
  cTrader emitovat prázdnou zprávu, na které pak zhroucuje** ("Message expected"), proto se vždy
  pošle nenulový zůstatek.
- **Commission** — `--commission`.
- **Spread** — `--spread`, **číselné pole v pipech, které nemůže klesnout pod 0**. Je **skryto v režimu Tick
  data**, kde cTrader odvozuje spread ze samotných tick údajů (není poslán `--spread`).

Adresář dat (`--data-file` / `--data-dir`) spravuje samotná aplikace (mezipaměť pro jednotlivé účty, viz
výše), není v dialogu vystavená.

:::note cTrader zhroutí se na prázdném testu
Pokud test vytvoří **žádné výsledky** — žádné obchody, nebo žádné tržní údaje pro zvolená data/symbol —
vlastní zapisovatel zpráv cTrader Console vyhodí `Message expected` a ukončí se bez zprávy. Aplikace nemůže
tuto upstream chybu opravit, ale detekuje ji a označí instanci **Selhalo** s praktickým důvodem
("žádné výsledky testu pro vybrané období…") místo surového stacktrace. Vyberte širší časové období
s dostupnými tržními údaji a zkuste znovu.
:::

## Stránka podrobností instance

Otevření instance (`/instance/{id}`) zobrazí její živý stav, protokoly a — pro test — křivku vlastního kapitálu.
**Název karty prohlížeče** odráží konkrétní instanci (**název cBotu · typ · symbol**, např.
`TrendBot · Backtest · EURUSD`), takže karta živého běhu a karta testu se liší na první pohled.
Běh a test stejného cBotu jsou sledovány jako samostatné **lineáže** (stabilní id lineáže přenesené
přes přechody stavů), takže stránka sleduje přesně jednu instanci a nikdy nemixuje data běhu s
daty testu.

## Kontroly životního cyklu instance

Každý řádek instance (a jeho stránka podrobností) má stavově správné ovládací prvky. **Aktivní** instance zobrazuje
**Stop**; **terminální** (Stopped / Completed / Failed) zobrazuje **Start (▶)** pro opětovné spuštění se
stejným cBotem, účtem, symbolem, timeframe, sadou parametrů a obrázkem (běh se restartuje jako běh, test jako test). Kliknutí na Stop zobrazí oznámení "Zastavování…" a deaktivuje ikonu, dokud se nevyřeší, a nově vytvořený běh se okamžitě objeví v seznamu — bez opětovného načtení stránky.

Protokoly konzoly jsou **perzistentní, když se instance ukončí** — pro běh (na Stop) a pro
**test** (po dokončení) — tak aby byly poslední běhové protokoly viditelné na stránce podrobností a,
prostřednictvím lišty nástrojů protokolu, **zkopírovány do schránky** (ikona Kopírovat protokoly) nebo **staženy** (ikona Stáhnout protokoly) i po odstranění kontejneru. Oba pracují s úplným protokolem konzoly instance, ne jen se zobrazeným ocasem.

Nahrané `.algo` nebylo nikdy postaveno zde, takže jeho sloupec **Last Build** na stránce cBotů je ponechán
prázdný (zobrazuje čas sestavení pouze pro cBoty, které v prohlížeči vytvoříte).

## Úprava a opětovné spuštění zastavené instance

**Zastavená** instance (běh nebo test) má ovládací prvek **Úprava** — ikonu na jejím řádku v seznamu **a**
vedle Start/Stop na její stránce podrobností — která otevře dialog **předvyplněný** jejím aktuálním nastavením.
Můžete změnit **obchodní účet, symbol, timeframe, sadu parametrů a značku obrázku** (a, pro
test, **okno a všechna výše uvedená nastavení testu**), pak **Uložit a spustit** jej znovu spustí s
novým nastavením (nahrazením zastavené instance). Ovládací prvek je **deaktivován, když je instance aktivní** —
pouze zastavená instance může být upravena.

## Spuštění z editoru kódu

Kliknutí na **Run** v editoru kódu otevře dialog místo spuštění slepého, pevně zakódovaného běhu:

- **Trading account** (povinné) — cTrader účet, ke kterému se cBot připojuje.
- **Parameter set** (volitelné) — vyberte existující sadu, nebo ji ponechte prázdnou pro spuštění s
  **výchozími hodnotami parametrů cBotu**. Tlačítko **+** vedle selektoru vytvoří novou sadu parametrů
  inline (viz níže) a vybere ji.
- **Symbol / Timeframe** standardně na `EURUSD` / `h1` a lze je změnit; **Zrušit** nebo **Spustit**.

Na **Spuštění** editor uloží + zkompiluje aktuální zdroj, spustí instanci na vybraném účtu
s vybranými parametry, poté sleduje živé protokoly kontejneru. (Stream protokolu předá cookie autentizace přihlášeného uživatele k `/hubs/logs` SignalR rozbočovač, takže se připojí místo selhání s
`Invalid negotiation response received`.)

## Sady parametrů

**Sada parametrů** je pojmenovaná, znovupoužitelná sada přepisů parametrů cBotu uložená jako plochý JSON
objekt mapující každý název parametru na skalární hodnotu, např. `{"Period": 14, "Label": "trend"}`. V
čase běhu/testu se změní na soubor cTrader `params.cbotset`
(`{ "Parameters": { … } }`). Sadu můžete vytvořit/upravit jako raw JSON z dialogu **Parameter
sets** cBotu nebo inline z dialogu Run.

Každá sada parametrů **patří k cBotu**: dialog New Parameter Set vypíše všechny vaše cBoty a musíte
**vybrat jeden** — vytvoření je blokováno, dokud není vybrán cBot. **Název sady je jedinečný per cBot**:
vytvoření nebo přejmenování sady na název, který již stejný cBot používá, je odmítnuto (jasná
chyba v dialogu, `409 Conflict` v API). Stejné jméno lze opakovat na **jiném** cBotu.

JSON je **ověřován** při uložení: musí to být jeden plochý objekt, jehož hodnoty jsou všechny skalární
(řetězec / číslo / bool). Non-object root, pole, vnořený objekt, `null` hodnota, nebo poškozený
JSON je odmítnut (jasná chyba v dialogu, `400 Bad Request` v API). Prázdný objekt `{}`
je povolen a znamená "bez přepisů".

## Poznámky k cTrader Console CLI

Testy potřebují `--data-mode` (výchozí `m1`), data jako `dd/MM/yyyy HH:mm` a
`params.cbotset` JSON pozicionální argument; `run` odmítá `--data-dir` (pouze pro test). Viz
`ContainerCommandHelpers`.

## Uzly a měřítko

Kapacita spouštění se zvyšuje přidáním agentů uzlů (samostatná registrace + heartbeat). Viz
[node discovery](../operations/node-discovery.md) a [scaling](../deployment/scaling.md).

## Je vyžadován obchodní účet

Spuštění nebo testování cBotu vyžaduje obchodní účet cTrader, ke kterému se má připojit. Dokud
nepřidáte jeden v části **Trading accounts**, tlačítka **Run New cBot** / **Backtest New cBot** jsou deaktivována (s
tooltipem) a stránka zobrazuje výzvu s odkazem na nastavení účtu — již neudělíte surovou
chybu `stream connect failed` od botu bez účtu.
