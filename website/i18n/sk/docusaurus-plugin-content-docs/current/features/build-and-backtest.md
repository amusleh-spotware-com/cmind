---
description: "Zostavujte, spúšťajte a testujte cTrader cBots (C# aj Python, obe na .NET) z editorov v prehliadači s podporou Monaco, spúšťajte na oficiálnom obraze ghcr.io/spotware/ctrader-console."
---

# Zostavovanie a backtesting cBotov

Zostavujte, spúšťajte a testujte cTrader cBots (C# **a** Python, obe na .NET) z editorov v prehliadači
s podporou Monaco, spúšťajte na oficiálnom obraze `ghcr.io/spotware/ctrader-console`.

## Zostavovanie

- **Builder** stránka hostuje editor Monaco; `CBotBuilder` kompiluje projekt pomocou
  `dotnet build` **v dočasnom kontajneri** (`AppOptions.BuildImage`, pracovný adresár pripojený
  na `/work`), takže nedôveryhodný MSBuild používateľa nemôže dosiahnuť hostiteľa. NuGet restore je uložený
  v cache prostredníctvom zdieľaného zväzku v rámci zostavovacích procesov. Webový hostiteľ potrebuje prístup k soketom Docker.
- Šablóny na začatie pre C# a Python sa nachádzajú v `src/Nodes/Builder/Templates/`.

## Spúšťanie a backtesting

- **Instances** = TPH stavová hierarchia (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Prechod stavu nahrádza entitu (zmena id),
  id kontajnera sa prenáša.
- `NodeScheduler` vyberie menej zaťažený oprávnený uzol; `ContainerDispatcherFactory` smeruje na
  vzdialený HTTP agent uzla alebo lokálny Docker dispečer.
- Polléry dokončenia zosúladzia ukončené kontajnery (backtestové kontajnery sa automaticky ukončia cez
  `--exit-on-stop`); správa prítomná → vykonaná (uloží `ReportJson`), chýbajúca → zlyhala.
- Živé konzoľové logy sa streamujú do prehliadača cez SignalR; backtestové krivky kapitálu sa analyzujú zo
  správy a vykreslujú do grafu.

## Backtestové trhové údaje sú uložené v cache podľa účtu

cTrader Console stiahne historické údaje tick/bar do svojho `--data-dir`. Tento adresár je
**stabilná, trvalá cache indexovaná podľa obchodného účtu** (jeho čísla účtu) — pripojená z disku uzla na jeho vlastnú cestu kontajnera (`/mnt/data`), **samostatné, neprepletené pripojenie** z
adresára práce jednotlivej inštancie. Preto každý backtest na tom istom účte **znovu použije** už stiahnuté údaje
namiesto ich stiahnutia pri každom spustení. (Skôr mal
dátový adresár umiestnenie pod adresárom práce jednotlivej inštancie, ktorého id sa mení pri každom spustení, čo núti čerstvé
stiahnutie pri každom backteste.) Dočasný adresár práce jednotlivej inštancie stále obsahuje algo, parametre, heslo
a správu; zdieľaná cache dát sa počíta pri použití backtestových dát uzla a vymaže sa akciou čistenia uzla.

## Nastavenia backtestingu

Dialóg **Backtest** sprístupňuje nastavenia backtestingu cTrader Console, ktoré je možné nastaviť používateľom, takže nikdy nemusíte
dotýkať sa príkazového riadka:

- **Symbol / Timeframe** — timeframe je **rozbaľovacia ponuka každej cTrader periódy** (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1` a Renko/Range/Heikin periódy), v
  kánickej kapitalizácii konzoly, takže vždy vyberiete platnú `--period`.
- **From / To** — backtestové okno (`--start` / `--end`).
- **Data mode** — jeden z troch režimov cTrader (`--data-mode`): **Tick data** (`tick`, presná),
  **m1 bars** (`m1`, rýchla), alebo **Open prices only** (`open`, najrýchlejšia).
- **Starting balance** — na výchozí 10000 (`--balance`). **Nulový zůstatek neumožní žádné obchody a cTrader
  emituje prázdnu správu, na ktorej potom havaruje** ("Message expected"), takže sa vždy pošle nenulový zůstatek.
- **Commission** a **Spread** — `--commission` / `--spread` (spread v pipoch).

Dátový adresár (`--data-file` / `--data-dir`) spravuje samotná aplikácia (cache podľa účtu, pozri
vyššie), nie je sprístupnená v dialógu.

## Stránka detail inštancie

Otvorenie inštancie (`/instance/{id}`) zobrazí jej živý stav, logy a — v prípade backtestingu — krivku kapitálu.
**Titul karty prehliadača** odráža konkrétnu inštanciu (**meno cBotu · typ · symbol**, napr.
`TrendBot · Backtest · EURUSD`), aby bola nezabudnuteľná karta spusteného behu a karta backtestingu
na prvý pohľad rozlíšiteľná. Run a backtest toho istého cBotu sú sledované ako odlišné **lineáže** (stabilné lineáž id
prenášané cez zmeny stavov), takže stránka sleduje presne jednu inštanciu a nikdy necamí zmiešavať údaje z behu s backtestom.

## Ovládacie prvky životného cyklu inštancie

Každý riadok inštancie (a jeho stránka detail) má ovládacie prvky správneho stavu. **Aktívna** inštancia zobrazuje
**Stop**; **terminálna** (Stopped / Completed / Failed) zobrazuje **Start (▶)** na jej opätovné spustenie s
tým istým cBotom, účtom, symbolom, timefrámom, súborom parametrov a obrazom (spustenie sa restartuje ako spustenie, backtest ako backtest). Kliknutie na Stop zobrazuje "Stopping…" upozornenie a deaktivuje ikonu, kým sa
nevyrieši, a novo vytvorený spoj sa okamžite objaví v zozname — bez obnovy stránky.

Konzoľové logy sú **trvalé, keď inštancia končí** — pre spustenie (na Stop) a pre
**backtest** (po dokončení) — takže logy posledného spustenia zostanú viditeľné na stránke detail a,
cez panel nástrojov logu, **skopírované do schránky** (Kopírovať logy ikona) alebo **stiahnuté** (Stiahnúť logy
ikona) aj po zmiznutí kontajnera. Obe pôsobia na úplný konzoľový log inštancie, nie len na
viditeľný chvost.

**Nahraný** `.algo` nikdy nebol zostavený tu, takže jeho **Last Build** stĺpec na stránke cBotov sa necháva
prázdny (zobrazuje čas zostavenia iba pre cBoty, ktoré ste zostavili v prehliadači).

## Úprava a opätovné spustenie zastavené inštancie

**Zastavená** inštancia (spustenie alebo backtest) má ovládací prvok **Edit** — ikona na jej riadku v zozname **a**
vedľa Start/Stop na jej stránke detail — ktorá otvára dialóg **prefilled** s jej aktuálnou konfiguráciou.
Môžete zmeniť **obchodný účet, symbol, timeframe, súbor parametrov a značku obrazu** (a pre
backtest, **okno a všetky backtestové nastavenia** vyššie), potom **Save & start** jej opätovné spustenie s
novými nastaveniami (nahradí zastavenú inštanciu). Ovládací prvok je **deaktivovaný, kým je inštancia aktívna** —
iba zastavená inštancia môže byť upravená.

## Spustenie z editora kódu

Kliknutie na **Run** v editore kódu otvára dialóg namiesto slepého, pevne kódovaného spustenia:

- **Trading account** (povinné) — obchodný účet cTrader, ku ktorému sa cBot pripája.
- **Parameter set** (voliteľné) — zvoľte existujúci súbor alebo nechajte ho prázdny na spustenie s cBotom
  **default values parametrov**. Tlačidlo **+** vedľa selektora vytvorí nový súbor parametrov
  inline (pozri nižšie) a vyberie ho.
- **Symbol / Timeframe** majú východzí `EURUSD` / `h1` a môžu sa zmeniť; **Cancel** alebo **Run**.

Na **Run** editor uloží + kompiluje aktuálny zdroj, spustí inštanciu na zvolenom účte
so zvolenými parametrami, potom si sleduje živé konzoľové logy. (Log stream prenáša cookie autentifikácie
prihláseného používateľa na hub `/hubs/logs` SignalR, takže sa pripája namiesto zlyhania s
`Invalid negotiation response received`.)

## Súbory parametrov

**Súbor parametrov** je pomenovaný, opakovaně použiteľný súbor prepísania parametrov cBotu uložený ako plochý JSON
objekt mapujúci názvy jednotlivých parametrov na skalárnu hodnotu, napr. `{"Period": 14, "Label": "trend"}`. V
čase spustenia/backtestingu sa pretvára na súbor cTrader `params.cbotset`
(`{ "Parameters": { … } }`). Môžete vytvoriť/upraviť súbor ako hrubý JSON z dialógu **Parameter
sets** cBotu alebo inline z dialógu Run.

Každý súbor parametrov **patrí cBotu**: dialóg New Parameter Set uvádza všetky vaše cBoty a vy
**musíte vybrať jeden** — vytvorenie je zablokované, kým nie je vybraný cBot. **Názov** súboru je jedinečný na cBot:
vytvorenie alebo premenovanie súboru na názov, ktorý už iný súbor toho istého cBotu používa, sa odmietne (jasná
chyba v dialógu, `409 Conflict` v API). Rovnaký názov možno opätovne použiť na **iný** cBot.

JSON je **oveľovaný** pri uložení: musí byť jeden plochý objekt, ktorého hodnoty sú všetky skalárne
(string / number / bool). Nekódy objekt root, pole, vnorený objekt, `null` hodnota alebo malformovaný
JSON sa odmietne (jasná chyba v dialógu, `400 Bad Request` v API). Prázdny objekt `{}`
je povolený a znamená "žiadne prepísania".

## Poznámky k CLI cTrader Console

Backtesty potrebujú `--data-mode` (východzí `m1`), dátumy ako `dd/MM/yyyy HH:mm` a
`params.cbotset` JSON pozičný argument; `run` odmietne `--data-dir` (iba backtest). Pozrite
`ContainerCommandHelpers`.

## Uzly a škálování

Kapacita vykonávania sa zvyšuje pridávaním agentov uzla (samoregulácia + tep). Pozrite
[node discovery](../operations/node-discovery.md) a [scaling](../deployment/scaling.md).

## Obchodný účet je povinný

Spustenie alebo backtesting cBotu potrebuje obchodný účet cTrader, aby sa mohol pripojiť. Kým nepridáte
jeden v časti **Trading accounts**, sú tlačidlá **Run New cBot** / **Backtest New cBot** zákázané (s
tooltip) a stránka zobrazuje výzvu s odkazom na nastavenie účtu — už sa nenachádzate v situácii hrubej
`stream connect failed` chyby od bota bez účtu.
