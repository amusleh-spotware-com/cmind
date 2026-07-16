---
description: "Gradnja, zagon in backtesting cTrader cBotov (C# in Python, oba .NET) iz brskalnika Monaco IDE, zagon na uradenem ghcr.io/spotware/ctrader-console sliki."
---

# Build & backtest cBots

Gradnja, zagon in backtesting cTrader cBotov (C# **in** Python, oba .NET) iz brskalnika Monaco IDE, zagon na uradenem `ghcr.io/spotware/ctrader-console` sliki.

## Build

- **Builder** stran gosti Monaco urejevalnik; `CBotBuilder` prevede projekt z `dotnet build` **v enkratnem kontejnerju** (`AppOptions.BuildImage`, delovni direktorij pritrjen na `/work`), zato nezaupljivi MSBuild cilji uporabnika ne dosežejo gostitelja. NuGet obnovljeni predpomnilnik je deljen med gradnjami preko skupne prostornine. Spletni gostitelj potrebuje dostop do Docker vtičnice.
- Začetne predloge C# in Python živijo v `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = TPH hierarhija stanj (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). Prehod zamenja entiteto (sprememba ID-ja), ID kontejnerja je prenesen.
- `NodeScheduler` izbere najmanj obremenjeno primerno vozlišče; `ContainerDispatcherFactory` usmeri na oddaljeni HTTP agent vozlišča ali lokalni Docker razpošiljalnik.
- Zaključni pollers uskladi izstopljene kontejnerje (backtesting kontejnerji samodejna izstopajo prek `--exit-on-stop`); poročilo prisotno → zaključeno (shranjeni `ReportJson`), manjkajoče → neuspešno.
- Živi dnevniki kontejnerja se pretakajo v brskalnik prek SignalR; backtesting krivulje kapitala so razčlenjene iz poročila in narisane.

## Backtest market data is cached per account

Konzola cTrader prenese zgodovinske podatke o kljukah/brah v svoj `--data-dir`. Ta direktorij je **stabilen, trajni predpomnilnik ključen na trgovinski račun** (njegovega števila računa) — pritrjen s diska vozlišča na njegov lastni direktorij kontejnerja (`/mnt/data`), **ločen, nevgnježdeni priklop** od direktorija dela na instanco. Zato vsak backtest na istem računu **ponovno uporabi** že prenešene podatke namesto da bi jih znova prejel. (Prej je direktorij podatkov živel pod direktorjem dela na instanco, katerega ID se spremeni pri vsakem zagonu, kar je prisilo sveži prenos pri vsakem backtestiranju.) Ephemeralni direktorij dela za instanco še vedno drži algoritem, parametre, geslo in poročilo; skupni predpomnilnik podatkov se šteje v porabi backtesting-podatkov vozlišča in ga očisti dejanje čiščenja vozlišča.

## Backtest settings

Dialog **Backtest** razkrije vsako nastavitev, ki jo sprejme CLI za backtesting konzole cTrader, zato nikoli ne boste morali dotikati ukazne vrstice:

- **From / To** — okno backtestiranja (`--start` / `--end`).
- **Data mode** — ena od treh načinov cTrader (`--data-mode`): **Tick data** (`tick`, natančno), **m1 bars** (`m1`, hitro) ali **Open prices only** (`open`, najhitreje).
- **Starting balance** — privzeto `10000` (`--balance`). **Stanje 0 ne zloži nobenega trgovanja in naredi, da cTrader izda prazno poročilo, ki ga nato sruši** ("Message expected"), zato se vedno pošlje stanje, ki ni nič.
- **Commission** in **Spread** — `--commission` / `--spread` (razpored v pipih).
- **Data file** (opcijsko) — pot na strani vozlišča do zgodovinskega podatkovnega datoteke (`--data-file`); pustite prazno, da uporabite prenešene/predpomnilniške podatke.
- **Expose environment variables** — preklop, ki prosledi spremenljivkam okolja gostitelja cBotu (zastavica `--environment-variables`).

## Instance detail page

Odpiranje instance (`/instance/{id}`) prikaže njeno živega stanja, dnevnike in — za backtest — krivuljo kapitala. **Naslov zavihka brskalnika** odraža specifično instanco (**ime cBota · vrsta · simbol**, npr. `TrendBot · Backtest · EURUSD`), zato sta zavihek živega teka in zavihek backtestiranja razlikljiva na prvi pogled. Tek in backtest istega cBota se spremljata kot ločeni **linijah** (stabilen ID linije, prenos prek prehodov stanja), zato stran sledi točno eni instanci in nikoli ne meša podatkov teka s podatki backtestiranja.

## Instance lifecycle controls

Vsaka vrstica instance (in njena detaljska stran) ima nadzornike, prilagojene stanju. **Aktivna** instanca prikaže **Stop**; **terminalna** (Stopped / Completed / Failed) prikaže **Start (▶)** za ponovni zagon z istim cBotom, računom, simbolom, časovnim okvirom, nizom parametrov in sliko (tek se ponovno zažene kot tek, backtest kot backtest). Klik na Stop prikaže obvestilo "Stopping…" in onemogoči ikono, dokler se ne razreši, v seznamu pa se takoj pojavi novo ustvarjena tekurija — brez osvežitve strani.

Dnevniki konzole so **trajno shranjeni, ko se instanca zaključi** — za tek (ob Stop) in za **backtest** (ob zaključku) — zato ostanejo dnevniki zadnjega zagona ogledljivi na detaljski strani in, prek orodne vrstice dnevnika, **kopirani v odložišče** (ikona Copy logs) ali **prejeti** (ikona Download logs) tudi po tem, ko je kontejner izbris. Oba delujeta na polnem dnevniku konzole instance, ne samo na zaslonski repu.

Naložena `.algo` nikoli ni bila zgradjena tukaj, zato je njen stolpec **Last Build** na strani cBots prazna (prikaže čas gradnje samo za cBote, ki jih gradite v brskalniču).

## Edit & re-run a stopped instance

**Zaustavljena** instanca (tek ali backtest) ima nadzor **Edit** — ikono na njeni vrstici v seznamu **in** ob Start/Stop na njen detaljski strani — ki odpre dialog **predhodno izpolnjen** s trenutno konfiguracijo. Spremenite **trgovinski račun, simbol, časovni okvir, niz parametrov in oznako slike** (in za backtest, **okno in vse nastavitve backtestiranja** zgoraj), nato **Save & start** ga znova zažene z novimi nastavitvami (zamenja zaustavljeno instanco). Nadzor je **onemogočen, medtem ko je instanca aktivna** — samo zaustavljena instanca je lahko urejena.

## Run from the code editor

Klik na **Run** v urejevalniku kode odpre dialog namesto da bi izvedel slepo, trdno kodirana tekurija:

- **Trading account** (obvezno) — račun cTrader, na katerega se cBot poveže.
- **Parameter set** (opcijsko) — izberite obstoječe, ali pustite prazno, da teče z **privzetimi vrednostmi parametrov** cBota. Gumb **+** ob izbirniku ustvari nov niz parametrov vstavljen (glej spodaj) in ga izbere.
- **Symbol / Timeframe** privzeto na `EURUSD` / `h1` in je mogoče spremeniti; **Cancel** ali **Run**.

Pri **Run** urejevalnik shrani + zgradi trenutni vir, zažene instanco na izbranem računu z izbranimi parametri, nato slednje žive dnevnike kontejnerja. (Tok dnevnika presledi piskavec avtentičnosti prijavljenega uporabnika na vozlišče SignalR `/hubs/logs`, zato se poveže namesto da bi propadel s `Invalid negotiation response received`.)

## Parameter sets

**Parameter set** je imenovan, ponovno uporabljiv niz nadomestkov parametrov cBota, shranjen kot ravna predmeta JSON, ki preslikovano vsak parameter ime na skalarno vrednost, npr. `{"Period": 14, "Label": "trend"}`. Med tekurijo/backtestiranjem je pretvorjen v datoteke cTrader `params.cbotset` (`{ "Parameters": { … } }`). Niz lahko ustvarite/uredite kot surovi JSON iz dialoga **Parameter sets** cBota ali vstavljeno iz dialoga Run.

Vsak niz parametrov **pripada cBotu**: dialog New Parameter Set prikazuje vse vaše cBote in **mora izbrati enega** — ustvarjanje je blokirano, dokler ni izbran cBot. **Ime niza je edinstveno na cBot**: ustvarjanje ali preimenovanje niza na ime, ki je že uporabljal drugi niz istega cBota, je zavrnjeno (jasna napaka v dialogo, `409 Conflict` pri API-ju). Isto ime je mogoče ponovno uporabiti na **drugem** cBotu.

JSON je **preverjen** ob shranjevanju: mora biti en sam raven predmet, katerega vrednosti so vse skalarne (niz / številka / bool). Ne-predmeten koren, niz, gnježdeni predmet, vrednost `null` ali malformed JSON je zavrnjen (jasna napaka v dialogo, `400 Bad Request` pri API-ju). Prazen predmet `{}` je dovoljen in pomeni "brez nadomestkov".

## cTrader Console CLI notes

Backtestiranje potrebuje `--data-mode` (privzeto `m1`), datume kot `dd/MM/yyyy HH:mm` in `params.cbotset` JSON pozicijski argument; `run` zavrne `--data-dir` (samo backtesting). Glejte `ContainerCommandHelpers`.

## Nodes & scale

Kapaciteta izvedbe se razširi z dodajanjem vozlišč agentov (samodejna registracija + srčni utrip). Glejte [node discovery](../operations/node-discovery.md) in [scaling](../deployment/scaling.md).

## A trading account is required

Zagon ali backtesting cBota potrebuje trgovinski račun cTrader za povezavo. Dokler ne dodate enega pod **Trading accounts**, sta gumba **Run New cBot** / **Backtest New cBot** onemogočena (s posameznim namigom) in stran prikazuje poziv, ki se veže na nastavitev računa — ne naletite več na suro napako `stream connect failed` iz bota brez računa.
