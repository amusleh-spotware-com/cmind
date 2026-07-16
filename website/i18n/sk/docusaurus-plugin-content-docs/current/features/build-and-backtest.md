---
description: "Skladajte, spúšťajte, backtestujte cTrader cBots (C# a Python, oba .NET) z v prehliadači integrovaného editora Monaco, spusťte na oficiálnej imágii ghcr.io/spotware/ctrader-console."
---

# Skladanie a backtestovanie cBots

Skladajte, spúšťajte, backtestujte cTrader cBots (C# **a** Python, oba .NET) z v prehliadači integrovaného editora Monaco, spusťte na oficiálnej imágii `ghcr.io/spotware/ctrader-console`.

## Skladanie

- **Builder** stránka hostuje editor Monaco; `CBotBuilder` skladá projekt s `dotnet build` **v dočasnom kontajneri** (`AppOptions.BuildImage`, pracovný adresár bind-mountovaný v `/work`), takže nedôveryhodné ciele MSBuild používateľov nemajú prístup k hostiteľskému súboru. Obnova NuGet je cacovaná medzi stavbami prostredníctvom zdieľaného zväzku. Webový hostiteľ potrebuje prístup k socketom Dockeru.
- Startovné šablóny C# a Python sú umiestnené v `src/Nodes/Builder/Templates/`.

## Spúšťanie a backtestovanie

- **Instances** = TPH stavová hierarchia (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). Prechod zastúpi entitu (zmena id), id kontajnera sa prenáša.
- `NodeScheduler` vyberá najmenej zaťažený oprávnený uzol; `ContainerDispatcherFactory` smeruje na vzdialený uzol HTTP agenta alebo miestny dispečer Docker.
- Dokončovací poller odsúhlasia ukončené kontajnery (backtestovací kontajnery sú automaticky ukončené prostredníctvom `--exit-on-stop`); správa prítomná → dokončená (uložiť `ReportJson`), chýbajúca → zlyhala.
- Živé denníky kontajnera sa vysielajú do prehliadača prostredníctvom SignalR; backtestovací krivky vlastného kapitálu sú analyzované z správy a graficky znázornené.

## Backtestovacia trhová údaja sú cachované na účet

cTrader Console stiahne historické tick/bar údaje do svojho adresára `--data-dir`. Tento adresár je **stabilná, trvalá cache kľúčovaná na obchodný účet** (jej číslo účtu) — bind-mountovaná z disku uzla na jeho vlastnú cestu kontajnera (`/mnt/data`), **samostatný, neiký mount** z pracovného adresára na inštanciu. Takže každý backtest na rovnakom účte **znovu používa** už stiahnuté údaje namiesto opätovného stiahnutia pri každom spustení. (Predtým bol adresár údajov umiestnený v adresári pracovného adresára na inštanciu, ktorého id sa mení pri každom spustení, čo si vyžiadalo nové stiahnutie pri každom backteste.) Dočasný pracovný adresár na inštanciu stále obsahuje algoritmus, parametre, heslo a správu; zdieľaná cache údajov sa počítavá v backtestovacích údajoch uzla a je vyčistená akciou čistenia uzla.

## Nastavenia backtestu

Dialóg **Backtest** vystavuje všetky nastavenia, ktoré CLI backtestu cTrader Console akceptuje, aby ste nikdy nemuseli dosiahnuť príkazový riadok:

- **From / To** — backtestovací časový rámec (`--start` / `--end`).
- **Data mode** — `m1` (1-minútové pruhy) alebo `tick` (`--data-mode`).
- **Starting balance** — predvolene `10000` (`--balance`). A **0 zostatok neuskutočňuje žiadne obchody a spôsobí, že cTrader vyšle prázdnu správu, na ktorej potom zlyháva** ("Message expected"), takže sa vždy posiela nenulový zostatok.
- **Commission** a **Spread** (`--commission` / `--spread`, spread v pipoch).
- **Advanced options** — pole s voľným textom `name=value` na riadok pre akékoľvek ďalšie možnosti backtestu, ktoré cTrader podporuje (napr. `applyCommissionAutomatically=true`); každý riadok sa stane argumentom CLI `--name value`.

## Stránka s podrobnosťami inštancie

Otvorenie inštancie (`/instance/{id}`) zobrazí jej stav v reálnom čase, denníky a — pre backtest — krivku vlastného kapitálu. **Názov karty prehliadača** odráža špecifickú inštanciu (**názov cBot · druh · symbol**, napr. `TrendBot · Backtest · EURUSD`) takže karta s živým spustením a backtestovacia karta sú na pohľad rozlíšiteľné. Spustenie a backtest rovnakého cBot sú sledované ako odlišné **lineáže** (stabilný ID lineáže prenášaný počas prechodu stavu), takže stránka sleduje presne jednu inštanciu a nikdy nemieša údaje spustenia s backtestom.

## Ovládacie prvky životného cyklu inštancie

Každý riadok inštancie (a jej stránka s podrobnosťami) má ovládacie prvky správne nastavené na stav. **Aktívna** inštancia zobrazuje **Stop**; **terminálna** (Stopped / Completed / Failed) zobrazuje **Start (▶)** na jej opätovné spustenie s rovnakým cBot, účtom, symbolom, časovým rámcom, sadou parametrov a obrázkom (spustenie sa restartuje ako spustenie, backtest ako backtest). Kliknutím na Stop sa zobrazí upozornenie "Stopping…" a vykonáte ikonu až do jeho vyriešenia a nové vytvorené spustenie sa okamžite objaví v zozname — bez opätovného načítania stránky.

Denníky konzoly sú **trvalé, keď sa inštancia ukončí** — pre spustenie (pri zastavení) a pre **backtest** (po dokončení) — takže denníky posledného spustenia zostávajú viditeľné na stránke s podrobnosťami a **cez panel nástrojov denníka sú **skopírované do schránky** (ikona Kopírovať denníky) alebo **stiahnuté** (ikona Stiahnuť denníky) aj po odstránení kontajnera. Obe pôsobia na úplnom denníku konzoly inštancie, nie len na viditeľnom chvoste.

Nahraný `.algo` tu nikdy nie bol vytvorený, takže jeho **Last Build** stĺpec na stránke cBots je ponechaný prázdny (zobrazuje čas vytvorenia iba pre cBots, ktoré vytvárate v prehliadači).

## Úprava a opätovné spustenie zastavená inštancia

**Zastavená** inštancia (spustenie alebo backtest) má ovládací prvok **Edit** — ikonu na jej riadku v zozname **a** vedľa Start/Stop na jej stránke s podrobnosťami — ktorá otvorí dialóg **predinformovaný** s jej aktuálnou konfiguráciou. Môžete zmeniť **obchodný účet, symbol, časový rámec, sadu parametrov a značku obrázka** (a pre backtest, **časový rámec a všetky nastavenia backtestu** vyššie), potom **Save & start** ho znova spustí s novými nastaveniami (nahradzuje zastavenú inštanciu). Ovládací prvok je **zakázaný, zatiaľ čo je inštancia aktívna** — iba zastavená inštancia je možné upraviť.

## Spustenie z editora kódu

Kliknutím na **Run** v editore kódu sa otvorí dialóg namiesto spustenia slepého, pevného spustenia:

- **Trading account** (povinné) — obchodný účet cTrader, ku ktorému sa cBot pripája.
- **Parameter set** (voliteľné) — vyberte existujúcu sadu alebo nechajte ju prázdnu na spustenie s hodnotami parametrov cBot **predvolené**. Tlačidlo **+** vedľa voľby vytvorí novú sadu parametrov v riadku (pozri nižšie) a vyberie ju.
- **Symbol / Timeframe** predvolene `EURUSD` / `h1` a je možné ich zmeniť; **Cancel** alebo **Run**.

Pri **Run** editor uloží + zostaví aktuálny zdroj, spustí inštanciu na vybranom účte s vybranými parametrami, potom odsúhlasí živé denníky kontajnera. (Tok denníka prenáša cookie overenia prihláseného používateľa na rozbočovač SignalR `/hubs/logs`, aby sa pripojila namiesto zlyhania s chybou `Invalid negotiation response received`.)

## Sady parametrov

**Parameter set** je pomenovaná, opäť použiteľná sada parametrov cBot prepísaní uložených ako plochý objekt JSON mapujúci každý názov parametra na skalárnu hodnotu, napr. `{"Period": 14, "Label": "trend"}`. V čase spustenia/backtestu sa zmení na súbor cTrader `params.cbotset` (`{ "Parameters": { … } }`). Sadu môžete vytvoriť/upraviť ako raw JSON z dialógu **Parameter sets** cBot alebo v riadku z dialógu Run.

Každá sada parametrov **patrí cBot**: dialóg New Parameter Set uvádza všetky vaše cBots a **musíte si ho vybrať** — vytvorenie je blokované, kým nie je vybraný cBot. Názov** sady je **jedinečný na cBot**: pokus o vytvorenie alebo premenovanie sady na názov, ktorý už používa iná sada rovnakého cBot, je odmietnutý (jasná chyba v dialógu, `409 Conflict` v API). Rovnaký názov sa môže použiť znova na **iný** cBot.

JSON je **validovaný** pri uložení: musí to byť jeden plochý objekt, ktorého hodnoty sú všetky skalárne (string / number / bool). Koreň bez objektu, pole, vnorený objekt, hodnota `null` alebo chybný JSON sú odmietnuté (jasná chyba v dialógu, `400 Bad Request` v API). Prázdny objekt `{}` je povolený a znamená "bez prepísaní".

## cTrader Console CLI poznámky

Backtesty potrebujú `--data-mode` (predvolene `m1`), dátumy ako `dd/MM/yyyy HH:mm` a `params.cbotset` JSON pozičný arg; `run` odmietá `--data-dir` (len backtest). Pozrite sa na `ContainerCommandHelpers`.

## Uzly a škálovanie

Kapacita spustenia sa škáluje pridávaním agentov uzlov (samozaregistrácia + heartbeat). Pozrite si [objavovanie uzlov](../operations/node-discovery.md) a [škálovanie](../deployment/scaling.md).

## Požaduje sa obchodný účet

Spustenie alebo backtestovanie cBot vyžaduje obchodný účet cTrader, aby sa k nemu pripojil. Kým nepridáte účet v sekcii **Trading accounts**, sú tlačidlá **Run New cBot** / **Backtest New cBot** zakázané (s tooltipom) a stránka zobrazuje výzvu s odkazom na nastavenie účtu — už nenarazíte na chybu `stream connect failed` z bota bez účtu.
