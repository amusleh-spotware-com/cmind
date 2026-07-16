---
description: "Gradnja, zagon in backtest cTrader cBotov (C# in Python, oba .NET) iz brskalnika Monaco IDE, zagon na uradni sliki ghcr.io/spotware/ctrader-console."
---

# Gradnja in backtest cBotov

Gradnja, zagon in backtest cTrader cBotov (C# **in** Python, oba .NET) iz brskalnika Monaco
IDE, zagon na uradni sliki `ghcr.io/spotware/ctrader-console`.

## Gradnja

- Stran **Builder** gosti urejevalnik Monaco; `CBotBuilder` prevede projekt z
  `dotnet build` **v enkratni posodi** (`AppOptions.BuildImage`, delovni imenik bind-mount
  na `/work`), tako da nezaupljivi uporabniški MSBuild ne doseže gostitelja. NuGet restore je predpomnjen
  med gradnjami preko deljene prostornine. Spletni gostitelj potrebuje dostop do vtičnika Docker.
- Startne predloge C# in Python živijo v `src/Nodes/Builder/Templates/`.

## Zagon in backtest

- **Instances** = hierarhija stanja TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Prehod zamenja entiteto (sprememba id),
  id posode se prenese.
- `NodeScheduler` izbere najmanj obremenjeno upravičeno vozlišče; `ContainerDispatcherFactory` preusmeri na
  agenta oddaljene vozlišča HTTP ali lokalni razporejevalnik Docker.
- Zapolnilni pollerji usklajujejo izstopljene posode (backtest posode se samopreklapljajo prek
  `--exit-on-stop`); poročilo prisotno → zaključeno (shrani `ReportJson`), manjkajoče → neuspešno.
- Toki živih posod se pretakajo v brskalnik preko SignalR; krivulje lastniškega kapitala backtest se razčistijo iz
  poročila in narišejo.

## Podatki o backtest trgu so predpomneni na račun

cTrader Console prenese zgodovinske podatke o tiku/stolpcu v `--data-dir`. Ta imenik je
**stabilen, trajen predpomnilnik, ključan na trgovalnem računu** (njegova številka računa) — bind-mounted iz
diska vozlišča na njegovo lastno pot posode (`/mnt/data`), **ločen, ne-ugniježdan mount** iz
delovnega imenika per-instance. Torej vsak backtest na istem računu **ponovno uporabi** že prenesene
podatke namesto, da bi jih znova prenesli pri vsakem zagonu. (Prej je bil
imenik podatkov pod delovnim imenikom per-instance, katerega id se spremeni pri vsakem zagonu, kar je prisililo svežo
prenos pri vsakem backtestu.) Efemerni delovni imenik per-instance še vedno drži algo, parametre, geslo
in poročilo; skupni predpomnilnik podatkov se šteje v porabo backtest-data vozlišča in se počisti z
akcijo čiščenja vozlišča.

## Nastavitve backtest

Dialog **Backtest** izpostavlja nastavitve backtest cTrader Console, ki jih uporabnik lahko spreminja, tako da se nikoli ne morate
dotakniti ukazne vrstice:

- **Symbol / Timeframe** — okvir časa je **spustni seznam vsakega cTrader obdobja** (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1` in Renko/Range/Heikin obdobja), v
  kanoničnem slogu konzole, tako da vedno izberete veljaven `--period`.
- **From / To** — okno backtest (`--start` / `--end`).
- **Data mode** — eno od treh cTrader načinov (`--data-mode`): **Tick data** (`tick`, natančno),
  **m1 bars** (`m1`, hitro) ali **Open prices only** (`open`, najhitreje).
- **Starting balance** — privzeto `10000` (`--balance`). **Saldo 0 ne izvršuje trgovin in povzroči, da cTrader
  izda prazno poročilo in se nato zrušimo** ("Message expected"), zato se vedno pošlje saldo, ki ni nič.
- **Commission** — `--commission`.
- **Spread** — `--spread`, **numerično polje v pipih, ki ne more biti manjše od 0**. Je **skrito v Tick
  data mode**, kjer cTrader izpelje razpred iz samih podatkov tika (ni poslano `--spread`).

Imenik podatkov (`--data-file` / `--data-dir`) upravlja sama aplikacija (predpomnilnik per-račun, glej
zgoraj), ni izpostavljen v dialogu.

:::note cTrader se zruši na praznem backtestu
Če backtest ne prinaša **rezultatov** — brez trgovin ali brez tržnih podatkov za izbrane datume/simbol —
pisatelj poročil lastne konzole cTrader vrže `Message expected` in se zaključi brez poročila. Aplikacija ne more
rešiti to nadaljnjo napako, vendar jo zazna in označi instanco **Failed** z razumljivo razlogo
("no backtest results for the selected range…") namesto surove sledi sklada. Izberite širše časovno obdobje
s katerim so razpoložljivi tržni podatki in poskusite znova.
:::

## Stran s podrobnostmi instance

Odpiranje instance (`/instance/{id}`) prikaže njeno stanje v realnem času, dnevnike in — za backtest — krivuljo lastniškega kapitala.
Naslov zavihka **brskalnika** odraža posebno instanco (**ime cBota · vrsta · simbol**, npr.
`TrendBot · Backtest · EURUSD`), zato je zavihek živega zagona in zavihek backtest razločljiv na prvi pogled.
Zagon in backtest istoimenske cBota se sledita kot ločeni **rodovi** (stabilen id rodu, prešel
med prehodi stanja), zato stran sledi natanko eni instanci in nikoli ne meša podatke zagona s podatki
backtest.

## Kontrole životnega cikla instance

Vsaka vrstica instance (in njena stran s podrobnostmi) ima kontrole, prilagojene stanju. **Aktivna** instanca prikaže
**Stop**; **končna** (Stopped / Completed / Failed) prikaže **Start (▶)**, da jo ponovno zaženete z
istim cBotom, računom, simbolom, okvirom časa, naborom parametrov in sliko (zagon se ponovno zagane kot zagon, backtest kot backtest). Klik na Stop prikaže obvestilo "Stopping…" in onemogoči ikono, dokler se ne razreši, nova instance pa se
pojavi v seznamu takoj — brez osvežitve strani.

Dnevniki konzole so **shranjeni, ko se instanca konča** — za zagon (pri Stop) in za
**backtest** (pri zaključku) enako — tako da dnevniki zadnjega zagona ostanejo vidni na strani s podrobnostmi in,
prek orodne vrstice dnevnika, **kopirani v odložišče** (ikona Kopiraj dnevnike) ali **preneseni** (ikona Prenesi dnevnike)
tudi potem, ko posoda izgine. Oba delujeta na polnem dnevniku konzole instance, ne samo na
vidnem repu.

Naložena `.algo` ni bila nikoli zgrajena tukaj, zato njena stolpec **Last Build** na strani cBots ostane
prazen (prikaže čas gradnje samo za cBote, ki jih zgradiš v brskalniku).

## Urejanje in ponovno zagon ustavljene instance

**Ustavljena** instanca (zagon ali backtest) ima kontrolo **Edit** — ikono na njeni vrstici v seznamu **in**
poleg Start/Stop na njeni strani s podrobnostmi — ki odpre dialog **vnaprej izpolnjen** s trenutno konfiguracijo.
Lahko spremenite **trgovalni račun, simbol, okvir časa, nabor parametrov in oznako slike** (in za
backtest tudi **okno in vse zgornje nastavitve backtest**), nato **Save & start** ga ponovno zagane z
novimi nastavitvami (zamenja ustavljeno instanco). Kontrola je **onemogoča, medtem ko je instanca aktivna** —
samo ustavljena instanca se lahko ureja.

## Zagon iz urejevalnika kode

Klik na **Run** v urejevalniku kode odpre dialog namesto da bi sprožil slepi, trdo kodiran zagon:

- **Trading account** (obvezno) — račun cTrader, s katerim se cBot poveže.
- **Parameter set** (neobvezno) — izberite obstoječi nabor ali ga pustite praznega, da se zaženete s **privzetimi vrednostmi parametrov** cBota.
  Gumb **+** poleg izbirnika ustvari novo vrednost parametra
  vstavki (glej spodaj) in jo izbere.
- **Symbol / Timeframe** privzeto `EURUSD` / `h1` in se lahko spremenita; **Cancel** ali **Run**.

Na **Run** urejevalnik shrani + gradi trenutni vir, začne instanco na izbranem računu
s izbranimi parametri, nato pa sledi tokam dnevnika živih posod. (Tok dnevnika preusmeri piskotko avtentifikacije
podpisanega uporabnika na hub SignalR `/hubs/logs`, zato se poveže namesto da bi bil neuspešen s
`Invalid negotiation response received`.)

## Nabori parametrov

**Parameter set** je imenovan, ponovno utiliskan nabor preglasitev parametrov cBota, shranjen kot raven objekt JSON,
ki preslika vsako ime parametra na skalarno vrednost, npr. `{"Period": 14, "Label": "trend"}`. Pri
zagonu/backtestu se pretvori v datoteko cTrader `params.cbotset`
(`{ "Parameters": { … } }`). Nabor lahko ustvarite/uredite kot surovec JSON iz dialoga **Parameter
sets** cBota ali vstavki iz dialoga Run.

Vsak nabor parametrov **pripada cBotu**: dialog New Parameter Set navaja vse vaše cBote in morate
**izbrati enega** — ustvarjanje je blokirano, dokler ni izbran cBot. Ime nabora je **edinstveno na cBot**:
ustvarjanje ali preimenovanje nabora na ime, ki ga že uporablja drugi nabor istoimenske cBota, je zavrnjeno (jasna
napaka v dialogu, `409 Conflict` pri API). Isto ime se lahko ponovno uporablja na **drugem** cBot.

JSON je **preverjen** pri shranjevanju: biti mora raven objekt, katerega vrednosti so vse skalarne
(niz / število / bool). Nenumerski koren, niz, ugniježdan objekt, vrednost `null` ali slabo oblikovan
JSON je zavrnjen (jasna napaka v dialogu, `400 Bad Request` pri API). Prazen objekt `{}`
je dovoljen in pomeni "brez preglasitve".

## Opombe CLI cTrader Console

Backtest potrebuje `--data-mode` (privzeto `m1`), datume kot `dd/MM/yyyy HH:mm` in
`params.cbotset` JSON pozicijski argument; `run` zavrne `--data-dir` (samo backtest). Glejte
`ContainerCommandHelpers`.

## Vozlišča in lestvica

Zmogljivost izvršitve se skalira z dodajanjem vozlišč agentov (samoprivolitev + srčni utrip). Glejte
[node discovery](../operations/node-discovery.md) in [scaling](../deployment/scaling.md).

## Potreben je trgovalni račun

Zagon ali backtest cBota potrebuje trgovalni račun cTrader za povezavo. Dokler ne dodate enega v razdelku
**Trading accounts**, so gumbi **Run New cBot** / **Backtest New cBot** onemogoči (s
napoeko) in stran prikaže poziv, ki se poveže na nastavko računa — ne boste več dobili surove
napake `stream connect failed` od bota brez računa.
