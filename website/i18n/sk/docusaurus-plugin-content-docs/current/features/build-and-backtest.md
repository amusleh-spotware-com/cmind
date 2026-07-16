---
description: "Zostavte, spustite a otestujte cBots cTrader (C# a Python, oba .NET) z integrovaného Monaco editora v prehliadači, spúšťajte na oficiálnom image ghcr.io/spotware/ctrader-console."
---

# Build & backtest cBots

Zostavte, spustite a otestujte cBots cTrader (C# **a** Python, oba .NET) z integrovaného Monaco
editora v prehliadači, spúšťajte na oficiálnom image `ghcr.io/spotware/ctrader-console`.

## Build

- **Builder** stránka obsahuje Monaco editor; `CBotBuilder` skompiluje projekt s
  `dotnet build` **v dočasnom kontajneri** (`AppOptions.BuildImage`, pracovný adresár bind-mount
  na `/work`), aby nedôveryhodné MSBuild ciele nemali prístup k hostiteľskému systému súborov. NuGet obnovenie sa cachuje
  medzi zostaveniami cez zdieľaný zväzok. Web host potrebuje prístup k Docker socketu.
- Štartovacie šablóny C# a Python sa nachádzajú v `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = TPH hierarchia stavov (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Prechod nahradí entitu (zmena id),
  id kontajnera sa zachovávajú.
- `NodeScheduler` vyberie najmenej zaťažený oprávnený uzol; `ContainerDispatcherFactory` smeruje na
  vzdialený node HTTP agent alebo lokálny Docker dispatcher.
- Completion pollers zosúlaďujú opustené kontajnery (backtest kontajnery sa automaticky ukončujú prostredníctvom
  `--exit-on-stop`); správa prítomná → dokončená (uloží `ReportJson`), chýbajúca → neúspešná.
- Živé logy kontajnera sa prenášajú do prehliadača cez SignalR; equity krivky backtestu sa analyzujú zo
  správy a zobrazia v grafe.

## Backtest market data is cached per account

cTrader Console si stiahne historické tick/bar dáta do `--data-dir`. Ten adresár je
**stabilný, perzistentný cache klúčovaný na obchodný účet** (jeho číslo účtu) — bind-mounted z
disk uzla na jeho vlastnú cestu kontajnera (`/mnt/data`), **samostatný, vnorený mount** z
adresára pre každý prípad. Takže každý backtest na tom istom účte **znova použije** už
stahované dáta namiesto nového stiahnutia. (Predtým sa
dátový adresár nachádzal pod adresárom dočasného pracovného priestoru na prípad, ktorého id sa mení pri každom spustení, čo prinútilo nový
stahovaný backtest.) Ephemeral adresár pracovného priestoru per-instance stále obsahuje algo, parametre, heslo
a správu; zdieľaný cache dát sa počítajú do backtest-data využitia uzla a vyčistia sa akciou
node-clean.

## Backtest settings

Dialog **Backtest** vystavuje užívateľom nastaviteľné nastavenia backtestingu cTrader Console, takže nikdy nemusíte
dotýkať sa príkazového riadku:

- **Symbol / Timeframe** — časový rámec je **rozbaľovací zoznam všetkých cTrader období** (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1`, a Renko/Range/Heikin obdobia), v
  konzole kanonickej kázne, aby ste vždy vybrali platné `--period`.
- **From / To** — okno backtestingu (`--start` / `--end`).
- **Data mode** — jeden z troch režimov cTrader (`--data-mode`): **Tick data** (`tick`, presné),
  **m1 bars** (`m1`, rýchle), alebo **Open prices only** (`open`, najrýchlejšie).
- **Starting balance** — predvolene `10000` (`--balance`). **0 zväčka nevytvára obchody a spôsobuje,
  že cTrader emituje prázdnu správu, na ktorej sa potom zrúti** ("Message expected"), takže nenulové zvýšenie je
  vždy poslané.
- **Commission** — `--commission`.
- **Spread** — `--spread`, **numerické pole v pip-och, ktoré nemôže ísť nižšie ako 0**. Je **skryté v režime Tick
  data**, kde cTrader spracovávajú spread z samotných tick dát (žiadny `--spread` sa neposiela).

Dátový adresár (`--data-file` / `--data-dir`) je spravovaný samotnou aplikáciou (per-account cache, viď
vyššie), nie je vystavený v dialógu.

:::note cTrader crashes on an empty backtest
Ak backtest produkuje **žiadne výsledky** — žiadne obchody, alebo žiadne trhové dáta pre zvolené dátumy/symbol —
vlastný príkazový riadok cTrader Console napíše `Message expected` a skončí bez správy. Aplikácia to nemôže
opraviť upstream bug, ale zistí to a označí inštanciu **Failed** s činnosťou dôvod
("no backtest results for the selected range…") namiesto raw stack trace. Zvoľte si širší rozsah dátumov
ktoré majú dostupné trhové dáta a skúste znova.
:::

## Instance detail page

Otvorenie inštancie (`/instance/{id}`) ukáže jej aktívny stav, logy a — pre backtest — equity
krivku. **Názov záložky prehliadača** odráža konkrétnu inštanciu (**názov cBotu · druh · symbol**, napr.
`TrendBot · Backtest · EURUSD`) takže Live-run záložka a backtest záložka sú rozlíšiteľné na prvý pohľad.
Spustenie a backtest toho istého cBotu sú sledované ako odlišné **lineáže** (stabilný lineáž id nesen
cez stavy prechod), takže stránka sleduje presne jednu inštanciu a nikdy nemieša data spustenia s
backtestom.

## Instance lifecycle controls

Každý riadok inštancie (a jej stránka detailov) má stavy-správne prvky. Aktívna **inštancia** zobrazuje
**Stop**; **terminál** jeden (Stopped / Completed / Failed) zobrazuje **Start (▶)** na relunch s
tým istým cBotom, účtom, symbolom, časovým rámcom, parameter set a image (spustenie sa reštartuje ako spustenie, backtest
ako backtest). Kliknutie na Stop zobrazí oznámenie "Stopping…" a zakáže ikonu až kým sa
vyrieši, a novo vytvorené spustenie sa okamžite objaví v zozname — žiadne obnovenie stránky.

Console logy sú **perzistované keď sa inštancia skončí** — pre spustenie (na Stop) a pre
**backtest** (na dokončenie) rovnako — takže logy posledného spustenia zostávajú viditeľné na detailu stránka a,
cez toolbar logy, **skopírované do schránky** (Copy logs ikona) alebo **stiahnuté** (Download logs
ikona) dokonca aj keď je kontajner preč. Oba pôsobia na úplný console log inštancie, nie len na
on-screen tail.

A **completed backtest** tiež perzistuje jeho **cTrader report** v oboch formátoch — raw **JSON**
(tá istá, ktorú čítajú equity krivka a AI analýza) a kompletná **HTML** report. Oboje sú
stahovateľné z backtest riadku **a** stránka detailov cez vyhradené ikony. Len **posledného spustenia**
reports sa uchovávajú, a ikony sú **zakázané** pre akýkoľvek backtest, ktorý je not-started, running alebo
failed (a nikdy sa neukazujú pre spustenie inštancie) — len dokončený backtest má správu na stahnutie.

A **uploaded** `.algo` tu nikdy nebola vybudovaná, takže jej **Last Build** stĺpec na cBots stránke je
prázdny (zobrazuje čas zostavenia len pre cBots, ktoré tu v prehliadači vybudujete).

## Edit & re-run a stopped instance

A **stopped** inštancia (spustenie alebo backtest) má **Edit** ovládanie — ikona na jej riadku v zozname **a**
vedľa Start/Stop na jej detailu stránke — ktorá otvorí dialóg **prefilled** s jej aktuálnej konfigurácie.
Môžete zmeniť **obchodný účet, symbol, časový rámec, parameter set a image tag** (a, pre
backtest, **okno a všetky backtest nastavenia** vyššie), potom **Save & start** ho relancuje s
novými nastaveniami (nahradením zastavenú inštanciu). Prvok je **zakázaný keď je inštancia aktívna** —
len zastavená inštancia môže byť upravená.

## Run from the code editor

Kliknutie na **Run** v editore kódu otvorí dialóg namiesto oslepého, pevne zakódovaného spustenia:

- **Trading account** (required) — cTrader účet, ku ktorému sa cBot pripojuje.
- **Parameter set** (optional) — zvoľte si existujúcu sadu, alebo ponechajte prázdnu na spustenie s cBotom
  **default parameter values**. A **+** gombík vedľa selektora vytvorí novú sadu parametrov
  inline (viď nižšie) a vyberie ju.
- **Symbol / Timeframe** default `EURUSD` / `h1` a môžu sa zmeniť; **Cancel** alebo **Run**.

Na **Run** editor uloží + skompiluje aktuálny zdroj, spustí inštanciu na vybranom účte
s vybranými parametrami, potom tails živé logy kontajnera. (Stream logy predávajú podpísaného
užívateľa auth cookie k `/hubs/logs` SignalR hub, takže sa pripojí namiesto zlyhania s
`Invalid negotiation response received`.)

## Parameter sets

A **parameter set** je pomenovaná, znova použiteľná sada cBot parameter overrides uložená ako plochý JSON
objekt mapujúci každý parameter name na skalárnu hodnotu, napr. `{"Period": 14, "Label": "trend"}`. Na
run/backtest čas sa zmení na cTrader `params.cbotset` súbor
(`{ "Parameters": { … } }`). Môžete vytvoriť/upraviť sadu ako raw JSON z cBot **Parameter
sets** dialógu alebo inline z Run dialógu.

Každá parameter set **patrí do cBotu**: dialóg New Parameter Set uvádza všetky vaše cBots a vy
**musíte zvoliť jeden** — vytvorenie je blokované až do výberu cBotu. Sada **názov je jedinečný na cBot**:
vytvorenie alebo premenovanie sady na názov, ktorý je iná sada rovnakého cBotu už používa sa zamietavanie (jasná
chyba v dialógu, `409 Conflict` v API). Rovnaký názov môže byť znova použitý na **iný** cBot.

JSON je **validácia** na uložiť: musí to byť jeden plochý objekt, ktorého hodnoty sú všetky skaláry
(string / number / bool). Non-object root, pole, vnorený objekt, `null` hodnota, alebo zle formované
JSON je zamietnutý (jasná chyba v dialógu, `400 Bad Request` v API). Prázdny objekt `{}`
je povolený a znamená "no overrides".

## cTrader Console CLI notes

Backtesty potrebujú `--data-mode` (default `m1`), dátumy ako `dd/MM/yyyy HH:mm`, a
`params.cbotset` JSON positional arg; `run` reject `--data-dir` (backtest-only). Viď
`ContainerCommandHelpers`.

## Nodes & scale

Výkonnosť spustenia mierka pridaním node agentov (self-register + heartbeat). Viď
[node discovery](../operations/node-discovery.md) a [scaling](../deployment/scaling.md).
## A trading account is required

Spustenie alebo backtest cBotu potrebuje cTrader obchodný účet na pripojenie. Kým nebudete mať čas na pridať
jeden pod **Trading accounts**, **Run New cBot** / **Backtest New cBot** gombíky sú zakázané (s
tooltip) a stránka zobrazuje výzvu, ktorá odkazuje na nastavenie účtu — viac nebude dostať raw
`stream connect failed` chyba z bota bez účtu.
