---
description: "Vytváranie, spustenie a backtesting cTrader cBots (C# a Python, obe .NET) z in-browser Monaco IDE, spustenie na oficiálnom obrázku ghcr.io/spotware/ctrader-console."
---

# Vytváranie a backtesting cBots

Vytváranie, spustenie a backtesting cTrader cBots (C# **a** Python, obe .NET) z in-browser Monaco
IDE, spustenie na oficiálnom obrázku `ghcr.io/spotware/ctrader-console`.

## Vytváranie

- **Stránka Builder** hostí Monaco editor; `CBotBuilder` kompiluje projekt pomocou
  `dotnet build` **v jednorazovom kontajneri** (`AppOptions.BuildImage`, pracovný adresár bind-mount
  na `/work`), aby nedôveryhodné MSBuild ciele hostitela nemohli dosiahnuť. NuGet obnovenie je uložené
  v medzipamäti medzi zostaveniami prostredníctvom zdieľaného zväzku. Webový hostiteľ potrebuje prístup k soketu Docker.
- Startovné šablóny C# a Python sa nachádzajú v `src/Nodes/Builder/Templates/`.

## Spustenie a backtesting

- **Inštancie** = TPH hierarchia stavov (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Prechod nahrádza entitu (zmena id),
  id kontajnera sa prenáša.
- `NodeScheduler` vyberie najmenej zaťažený oprávnený uzol; `ContainerDispatcherFactory` smeruje na
  vzdialený uzol HTTP agent alebo lokálny dispečer Docker.
- Pollovacie služby dokončenia zosúladzia ukončené kontajnery (backtestovací kontajnery sú sami ukončené prostredníctvom
  `--exit-on-stop`); správa prítomná → dokončená (uloženie `ReportJson`), chýbajúca → zlyhala.
- Živé protokoly kontajnera sú streamované do prehliadača cez SignalR; backtestovací krivky vlastného kapitálu sú analyzované zo
  správy a zobrazené v grafe.

## Backtestovací trhové údaje sú uložené v medzipamäti podľa účtu

Konzola cTrader sťahuje historické údaje tick/bar do svojho `--data-dir`. Tento adresár je
**stabilná, trvalá medzipamäť klúčovaná na obchodnom účte** (jeho číslo účtu) — bind-mounted z
disku uzla na jeho vlastnú cestu kontajnera (`/mnt/data`), **samostatný, nevnorený mount** od
adresára per-inštancia. Takže každý backtest na rovnakom účte **opätovne používa** už stiahnuté údaje
namiesto ich opätovného stiahnutia pri každom spustení. (Skôr bol
dátový adresár umiestnený v adresári per-inštancia, ktorého id sa mení pri každom spustení, čo nútilo stiahnutie
pri každom backteste.) Dočasný adresár per-inštancia stále obsahuje algoritmus, parametre, heslo
a správu; zdieľaná medzipamäť údajov sa počíta do backtestovacieho užitia údajov uzla a je vymazaná akcií
čištenia uzla.

## Nastavenia backtestingu

Dialógové **Backtest** zverejňuje nastavenia backtestingu konzoly cTrader, ktoré môže užívateľ nastavovať, takže nikdy nemusíte
dotýkať sa príkazového riadka:

- **Symbol / Timeframe** — časový rámec je **rozbaľovací zoznam všetkých období cTrader** (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1` a Renko/Range/Heikin obdobia), v
  kvázikanonickej pravopise konzoly, aby ste vždy vybrali platnú `--period`.
- **From / To** — okno backtestingu (`--start` / `--end`).
- **Dátový režim** — jeden z troch režimov cTrader (`--data-mode`): **Tick data** (`tick`, presné),
  **m1 bars** (`m1`, rýchle) alebo **Iba otváracie ceny** (`open`, najrýchlejšie).
- **Počiatočný zostatok** — predvolené `10000` (`--balance`). **Nulový zostatok umiestnenie neprevádzkuje a nechá
  cTrader vykázať prázdnu správu a potom sa zrúti** ("Message expected"), takže je vždy odoslaný nenulový zostatok.
- **Komisia** — `--commission`.
- **Spread** — `--spread`, **numerické pole v pipoch, ktoré nemôže klesnúť pod 0**. Sú **skryté v režime Tick
  data**, kde cTrader odvodzuje rozptyl z tick údajov samotných (nie je odoslaný žiadny `--spread`).

Dátový adresár (`--data-file` / `--data-dir`) je spravovaný samotnou aplikáciou (medzipamäť per-účet, viď
vyššie), nie je vystavený v dialógu.

:::note cTrader sa zrúti na prázdnom backteste
Ak backtest vyprodukuje **žiadne výsledky** — žiadne obchody alebo žiadne trhové údaje pre zvolené dátumy/symbol —
vlastný zapisovateľ správ konzoly cTrader vyhodí `Message expected` a skončí bez správy. Aplikácia to nemôže
opraviť v tomto úlohe nadradenom chybe, ale detekuje to a označí inštanciu **Failed** s akčným dôvodom
("no backtest results for the selected range…") namiesto hrubého stack trace. Vyberte si širší rozsah dátumov
ktorý má dostupné trhové údaje a skúste znova.
:::

## Stránka s podrobnosťami inštancie

Otvorenie inštancie (`/instance/{id}`) zobrazuje jej živý stav, protokoly a — pre backtest — krivku vlastného kapitálu.
**Názov karty prehliadača** odráža špecifickú inštanciu (**názov cBotu · druh · symbol**, napr.
`TrendBot · Backtest · EURUSD`), aby bola na prvý pohľad rozlíšená karta živého spustenia a karta backteste.
Spustenie a backtest rovnakého cBotu sú sledované ako odlišné **lineáže** (stabilné id lineáže prenášané
cez prechody stavov), takže stránka nasleduje presne jednu inštanciu a nikdy nemieša údaje spustenia s
backtestom.

## Ovládacie prvky životného cyklu inštancie

Každý riadok inštancie (a jeho stránka s podrobnosťami) má ovládacie prvky správneho stavu. Aktívna **aktívna** inštancia zobrazuje
**Stop**; **terminálna** (`Stopped` / `Completed` / `Failed`) zobrazuje **Start (▶)** na spustenie
s rovnakým cBotom, účtom, symbolom, časovým rámcom, sadou parametrov a obrázkom (spustenie sa restartuje ako spustenie, backtest
ako backtest). Kliknutie na Stop zobrazí oznámenie "Stopping…" a zakáže ikonu, kým sa to nevyrieši, a novo vytvorené spustenie
sa okamžite objaví v zozname — bez opätovného načítania stránky.

Protokoly konzoly sú **uchovávané, keď sa inštancia ukončí** — pre spustenie (na Stop) a pre
**backtest** (po dokončení) — takže protokoly posledného spustenia zostávajú prezerateľné na stránke s podrobnosťami a
prostredníctvom panela nástrojov protokolu sa **kopírujú do schránky** (ikona Copy logs) alebo **sťahujú** (ikona Download logs)
dokonca aj po zmiznutí kontajnera. Obidve pôsobia na úplný protokol konzoly inštancie, nie len na viditeľný chvost.

Nahraný `.algo` nikdy nebol tu vytvárený, takže jeho stĺpec **Last Build** na stránke cBots je prázdny
(zobrazuje čas zostavenia iba pre cBots, ktoré tu vytvárate v prehliadači).

## Úprava a opätovné spustenie zastavnej inštancie

**Zastavená** inštancia (spustenie alebo backtest) má ovládací prvok **Úprava** — ikonu na jej riadku v zozname **a**
vedľa Start/Stop na jej stránke s podrobnosťami — ktorá otvorí dialóg **prefilled** s jeho aktuálnou konfigurácií.
Môžete zmeniť **obchodný účet, symbol, časový rámec, sadu parametrov a značku obrázku** (a pre backtest
**okno a všetky nastavenia backtestingu** vyššie), potom **Uložiť a spustiť** ho znovu spustí s
novými nastaveniami (nahradenie zastavnej inštancie). Ovládací prvok je **zakázaný, keď je inštancia aktívna** —
iba zastavená inštancia sa môže upravovať.

## Spustenie z editora kódu

Kliknutie na **Run** v editore kódu otvorí dialóg namiesto spustenia slepo, pevného spustenia:

- **Obchodný účet** (povinný) — účet cTrader, ku ktorému sa cBot pripája.
- **Sada parametrov** (voliteľné) — vyberte existujúcu sadu alebo ju ponechajte prázdnu na spustenie s **predvolenými
  hodnotami parametrov** cBotu. Tlačidlo **+** vedľa selektora vytvorí novú sadu parametrov
  vloženú (pozri nižšie) a vyberie ju.
- **Symbol / Timeframe** sa predvolené `EURUSD` / `h1` a je možné ich zmeniť; **Zrušiť** alebo **Spustiť**.

Na **Spustenie** editor uloží a skompiluje aktuálny zdroj, spustí inštanciu na zvolenom účte
so zvolenými parametrami a potom sleduje živé protokoly kontajnera. (Tok protokolu presmeroví cookie autentifikácie prihláseneho
používateľa do centra `/hubs/logs` SignalR, aby sa pripojil namiesto zlyhania s
`Invalid negotiation response received`.)

## Sady parametrov

**Sada parametrov** je pomenovaná, opätovne použiteľná sada prepísaní parametrov cBotu uložená ako plochý JSON
objekt mapujúci každý názov parametra na skalárnu hodnotu, napr. `{"Period": 14, "Label": "trend"}`. V
čase spustenia/backtestingu sa zmení na súbor cTrader `params.cbotset`
(`{ "Parameters": { … } }`). Môžete vytvoriť/upraviť sadu ako neobrázku JSON z **Parameter
sets** dialógu cBotu alebo vloženého z dialógu Run.

Každá sada parametrov **patrí cBotu**: dialóg New Parameter Set uvádza všetky vaše cBots a musíte
**vybrať jeden** — vytvorenie je zablokované, kým nie je vybraný cBot. Názov sady je **jedinečný na cBot**:
vytvorenie alebo premenovanie sady na názov, ktorý už používa iná sada rovnakého cBotu, je odmietnuté (jasná
chyba v dialógu, `409 Conflict` v API). Rovnaký názov sa môže opätovne použiť na **inom** cBote.

JSON je **validovaný** pri uložení: musí to byť jeden plochý objekt, ktorého hodnoty sú všetky skalárne
(string / number / bool). Neobjem root, pole, vnorený objekt, hodnota `null` alebo malformed
JSON je odmietnutý (jasná chyba v dialógu, `400 Bad Request` v API). Prázdny objekt `{}`
je povolený a znamená "bez prepísaní".

## Poznámky k CLI konzoly cTrader

Backtesty potrebujú `--data-mode` (predvolené `m1`), dátumy ako `dd/MM/yyyy HH:mm` a
`params.cbotset` JSON poziční argument; `run` odmietajú `--data-dir` (iba backtest). Pozri
`ContainerCommandHelpers`.

## Uzly a rozšírenie

Kapacita vykonávania sa rozširuje pridávaním agentov uzlov (samá registrácia a pulz). Pozri
[node discovery](../operations/node-discovery.md) a [scaling](../deployment/scaling.md).

## Obchodný účet je povinný

Spustenie alebo backtesting cBotu vyžaduje obchodný účet cTrader, aby sa naň pripájal. Kým neho pridáte pod
**Obchodné účty**, tlačidlá **Run New cBot** / **Backtest New cBot** sú zakázané (s
tooltip) a stránka zobrazuje výzvu s odkazom na nastavenie účtu — už viac nenarazíte na surový
`stream connect failed` chybu z bota bez účtu.
