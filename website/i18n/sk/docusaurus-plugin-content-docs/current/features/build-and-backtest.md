---
description: "Zostavujte, spúšťajte a testujte backtestom cTrader cBots (C# a Python, oba .NET) z integrovaného edátora Monaco v prehliadači, spustené na oficiálnom obrázku ghcr.io/spotware/ctrader-console."
---

# Build & backtest cBots

Zostavujte, spúšťajte a testujte backtestom cTrader cBots (C# **a** Python, oba .NET) z integrovaného edátora Monaco v prehliadači, spustené na oficiálnom obrázku `ghcr.io/spotware/ctrader-console`.

## Build

- **Builder** stránka hosťuje editor Monaco; `CBotBuilder` kompiluje projekt s `dotnet build` **v jednorazovom kontajneri** (`AppOptions.BuildImage`, pracovný adresár bind-mount na `/work`), takže nedôveryhodné používateľské MSBuild ciele nemôžu dosiahnuť hostiteľa. Obnovenie NuGet je uložené v cache medzi zostaveniami prostredníctvom zdieľaného zväzku. Webový hostiteľ potrebuje prístup k soketu Docker.
- C# a Python štartovacie šablóny nachádzajú sa v `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = TPH hierarchia stavov (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). Prechod nahradí entitu (zmena id), id kontajnera sa prenáša.
- `NodeScheduler` vyberie najmenej zaťažený oprávnený uzol; `ContainerDispatcherFactory` smeruje na vzdialený uzol HTTP agent alebo lokálny Docker dispatcher.
- Pollers na dokončenie zosúladžujú ukončené kontajnery (backtest kontajnery sa sami ukončia cez `--exit-on-stop`); správa prítomná → ukončená (uloží `ReportJson`), chýbajúca → neúspešná.
- Priame protokoly kontajnera sa streamujú do prehliadača cez SignalR; backtest krivky kapitálu sú parsované zo správy a zobrazené v grafe.

## Backtest market data is cached per account

Nástroj cTrader Console stiahne historické údaje tick/bar do svojho `--data-dir`. Tento adresár je **stabilná, perzistentná cache klúčovaná na obchodný účet** (jeho číslo účtu) — bind-mountovaná z disku uzla na jeho vlastnej ceste kontajnera (`/mnt/data`), **samostatný, nevnorený mount** z adresára pracovnej plochy na inštanciu. Takže každý backtest na tom istom účte **opäť používa** už stiahnuté údaje namiesto opätovného stiahnutia pri každom spustení. (Predtým adresár údajov žil pod adresárom pracovnej plochy na inštanciu, ktorého id sa zmení pri každom spustení, čo prinútilo stiahnutie čerstvých údajov pri každom backteste.) Ephemeral adresár pracovnej plochy na inštanciu stále drží algo, params, heslo a správu; zdieľaná cache údajov sa počíta v používaní backtest-data uzla a vymaže sa akciou na čistenie uzla.

## Backtest settings

Dialóg **Backtest** vystavuje všetky nastavenia, ktoré CLI backtest cTrader Console akceptuje, takže nikdy nemusíte dotýkať sa príkazového riadka:

- **From / To** — okno backtestu (`--start` / `--end`).
- **Data mode** — jeden z troch režimov cTrader (`--data-mode`): **Tick data** (`tick`, presný), **m1 bars** (`m1`, rýchly), alebo **Open prices only** (`open`, najrýchlejší).
- **Starting balance** — predvolené na `10000` (`--balance`). **Nulový zostatok neuskutoční žiadne obchody a spôsobí, že cTrader vydelí prázdnu správu, na ktorej sa potom zrúti** ("Message expected"), takže vždy sa odošle nenulový zostatok.
- **Commission** a **Spread** — `--commission` / `--spread` (spread v pipoch).
- **Data file** (voliteľné) — cesta na strane uzla k histórickému dátovému súboru (`--data-file`); nechajte prázdne na použitie stiahnutých/cachovovaných údajov.
- **Expose environment variables** — prepínač, ktorý odovzdá premenné prostredia hostiteľa cBotu (príznak `--environment-variables`).

## Instance detail page

Otvorenie inštancie (`/instance/{id}`) zobrazí jej priamy stav, protokoly a — pre backtest — krivku kapitálu. **Názov karty prehliadača** odráža špecifickú inštanciu (**názov cBot · druh · symbol**, napr. `TrendBot · Backtest · EURUSD`), takže záložka priameho spustenia a záložka backtesta sú rozpoznateľné na prvý pohľad. Spustenie a backtest toho istého cBot sú sledované ako odlišné **lineáže** (stabilné id lineáže prenášané cez prechody stavov), takže stránka sleduje presne jednu inštanciu a nikdy nemieša údaje spustenia s údajmi backtesta.

## Instance lifecycle controls

Každý riadok inštancie (a jeho stránka s podrobnosťami) má ovládacie prvky správne stavové. **Aktívna** inštancia ukazuje **Stop**; **terminálna** (Stopped / Completed / Failed) ukazuje **Start (▶)** na opätovné spustenie s tým istým cBot, účtom, symbolom, časovým rámcom, súborom parametrov a obrázkom (spustenie sa reštartuje ako spustenie, backtest ako backtest). Kliknutie na Stop zobrazí upozornenie "Stopping…" a zakáže ikonu, kým sa nevyriešia, a novo vytvorené spustenie sa okamžite objaví v zozname — bez obnovy stránky.

Protokoly konzoly sú **uchovávané, keď sa inštancia ukončí** — pre spustenie (pri zastavení) a pre **backtest** (pri dokončení) — takže protokoly posledného spustenia zostávajú viditeľné na stránke s podrobnosťami a cez panel s nástrojmi protokolu **skopírované do schránky** (ikona Kopírovať protokoly) alebo **stiahnuté** (ikona Stiahnutí protokoly) aj po zániku kontajnera. Obe pôsobia na úplný protokol konzoly inštancie, nie len na viditeľný chvost na obrazovke.

Nahraný `.algo` bol nikdy zostavený tu, takže jeho stĺpec **Last Build** na stránke cBots zostáva prázdny (zobrazuje čas zostavenia iba pre cBots, ktoré zostavíte v prehliadači).

## Edit & re-run a stopped instance

Zastavená inštancia (spustenie alebo backtest) má ovládací prvok **Edit** — ikona na jej riadku v zozname **a** vedľa Start/Stop na jej stránke s podrobnosťami — ktorá otvára dialóg **predvyplnený** jej súčasnou konfiguráciou. Môžete zmeniť **obchodný účet, symbol, časový rámec, súbor parametrov a značku obrázku** (a pre backtest **okno a všetky nastavenia backtesta** vyššie), potom **Save & start** ho reštartuje s novými nastaveniami (nahrádzajú zastavenú inštanciu). Ovládací prvok je **zakázaný, kým je inštancia aktívna** — iba zastavená inštancia sa dá upraviť.

## Run from the code editor

Kliknutím na **Run** v editore kódu sa otvorí dialóg namiesto zaslepého, pevne zakódovaného spustenia:

- **Trading account** (povinné) — obchodný účet cTrader, ku ktorému sa cBot pripája.
- **Parameter set** (voliteľné) — vyberte existujúci súbor alebo ho nechajte prázdny na spustenie s **predvolenými hodnotami parametrov** cBot. Tlačidlo **+** vedľa voľby vytvorí nový súbor parametrov vložene (pozri nižšie) a vyberie ho.
- **Symbol / Timeframe** majú predvolené nastavenie `EURUSD` / `h1` a dajú sa zmeniť; **Cancel** alebo **Run**.

Na **Run** editor uloží + kompiluje aktuálny zdroj, spustí inštanciu na vybranom účte s vybranými parametrami, potom chvostí priame protokoly kontajnera. (Prúd protokolu preposlúcha auth cookie prihláseného používateľa do centra SignalR `/hubs/logs`, takže sa pripája namiesto zlyhania s `Invalid negotiation response received`.)

## Parameter sets

**Parameter set** je pomenovaná, opätovne použiteľná sada prepisy parametrov cBot uloženej ako plochý objekt JSON mapujúci každý názov parametra na skálenú hodnotu, napríklad `{"Period": 14, "Label": "trend"}`. V čase spustenia/backtesta sa zmení na súbor cTrader `params.cbotset` (`{ "Parameters": { … } }`). Súbor môžete vytvoriť/upraviť ako surové JSON z dialógu **Parameter sets** cBot alebo vložene z dialógu Run.

Každý súbor parametrov **patrí cBotu**: dialóg New Parameter Set obsahuje zoznam všetkých vašich cBots a **musíte si vyberte jeden** — vytvorenie je zablokované, kým nie je vybraný cBot. **Názov sady parametrov je jedinečný na cBot**: vytvorenie alebo premenovanie sady na názov, ktorý už používa iná sada toho istého cBot, sa odmietne (jasná chyba v dialógu, `409 Conflict` v API). Rovnaký názov sa môže opätovne použiť na **iný** cBot.

JSON je **overený** pri uložení: musí to byť jeden plochý objekt, ktorého hodnoty sú všetky skalárne (reťazec / číslo / bool). Neobjekt root, pole, vnorený objekt, hodnota `null` alebo chybne formátovaný JSON sa odmietne (jasná chyba v dialógu, `400 Bad Request` v API). Prázdny objekt `{}` je povolený a znamená "žiadne prepisy".

## cTrader Console CLI notes

Backtesty potrebujú `--data-mode` (predvolené `m1`), dátumy ako `dd/MM/yyyy HH:mm` a `params.cbotset` JSON poziční arg; `run` odmietne `--data-dir` (iba backtest). Pozri `ContainerCommandHelpers`.

## Nodes & scale

Kapacita vykonávania sa škáluje pridávaním uzlov agentov (samo-registrácia + tep srdca). Pozri [node discovery](../operations/node-discovery.md) a [scaling](../deployment/scaling.md).

## A trading account is required

Spustenie alebo testovanie backtestom cBot vyžaduje obchodný účet cTrader na pripojenie. Kým nebudete mať priložený jeden pod **Trading accounts**, tlačidlá **Run New cBot** / **Backtest New cBot** sú zakázané (s tooltipom) a stránka ukazuje výzvu s odkazom na nastavenie účtu — už nebudete mať surový error `stream connect failed` z botu bez účtu.
