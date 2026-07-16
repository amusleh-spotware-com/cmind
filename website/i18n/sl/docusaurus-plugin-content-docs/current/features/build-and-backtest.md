---
description: "Gradnja, pogon, backtesting cTrader cBotov (C# in Python, oboje .NET) iz brskalnika Monaco IDE, pogon na uradni sliki ghcr.io/spotware/ctrader-console."
---

# Gradnja in backtesting cBotov

Gradnja, pogon, backtesting cTrader cBotov (C# **in** Python, oboje .NET) iz brskalnika Monaco
IDE, pogon na uradni sliki `ghcr.io/spotware/ctrader-console`.

## Gradnja

- **Builder** stran je domačin Monaco urejevalnika; `CBotBuilder` kompajlira projekt z
  `dotnet build` **v začasnem kontejnerju** (`AppOptions.BuildImage`, delovni imenik pripet
  na `/work`), zato se nedovoljenemu uporabnikovemu MSBuild-u ni mogoče dostopati do gostitelja. NuGet obnovljeno predpomnjenja
  preko gradov preko skupne glasnosti. Spletni gostitelj potrebuje dostop do Docker vtičnice.
- Začetne predloge C# + Python živijo v `src/Nodes/Builder/Templates/`.

## Pogon in backtesting

- **Instances** = TPH hierarhija stanja (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Prehod zamenja entiteto (sprememba id),
  id kontejnerja prenesenega.
- `NodeScheduler` izbere najmanj obremenjeno upravičeno vozlišče; `ContainerDispatcherFactory` usmeri na
  oddaljeno vozlišče HTTP agenta ali lokalni Docker dispečer.
- Polniki zaključka usklajijo izhojene kontejnerje (kontejnerji backtestiranja se samodejavno izstopijo prek
  `--exit-on-stop`); poročilo prisotno → zaključeno (shrani `ReportJson`), manjka → ni uspelo.
- Živi dnevniki kontejnerja se pretakajo do brskalnika prek SignalR; krivulje kapitala backtestiranja analizirane iz
  poročila + prikazane v grafu.

## Podatki o trgu backtestiranja so predpomninjeni po računu

cTrader Console prenese zgodovinske podatke o kljukah/stolpcih v svoj `--data-dir`. Ta imenik je
**stabilna, trajna predpomnilnik ključnega računa za trgovanje** (njegove številke računa) — pripet
od disketa vozlišča na njegovi poti lastnega kontejnerja (`/mnt/data`), **ločeno, ugniježđeno priklapljanje**
od vsakega delovnega imenika. Torej je vsak backtesting na istem računu **ponovno uporabi** že prejete podatke
namesto ponovno prenesenih podatkov pri vsakem teku. (Prej je
podatkovni imenik živel pod delovnim imenikom na instanco, katerega id se spremeni z vsakim tekom, kar je prisililo novo
prenos ob vsakem backtestiranju.) Efemerni delovni imenik na instanco še vedno drži algo, parametre, geslo
in poročilo; skupna predpomnilnik podatkov se šteje v uporabi backtestiranja vozlišča in se očisti s
akcijo čiščenja vozlišča.

## Nastavitve backtestiranja

Dialog **Backtest** izpostavlja vsako nastavitev, ki jo sprejema CLI backtestiranja cTrader Console, zato nikoli
ne moraš se dotakniti ukazne vrstice:

- **From / To** — okno backtestiranja (`--start` / `--end`).
- **Data mode** — `m1` (palice v 1-minutnem intervalu) ali `tick` (`--data-mode`).
- **Starting balance** — privzeto na `10000` (`--balance`). **0 ravnotežje ne naredi nobenih trgov in naredi
  cTrader izda prazno poročilo, ki ga nato sesuje** ("Message expected"), zato je vedno poslano ne-ničelno ravnotežje.
- **Commission** in **Spread** (`--commission` / `--spread`, razporeditev v pipa).
- **Advanced options** — polje s prosto obliko `name=value` na vrstico za katero koli drugo nastavitev backtestiranja cTrader
  podpira (npr. `applyCommissionAutomatically=true`); vsaka vrstica postane `--name value` CLI argument.

## Stran s podrobnostmi instance

Odpiranje instance (`/instance/{id}`) prikaže njen živih status, dnevnike in — za backtesting — ploščo
krivulje kapitala. **Naslovna vrstica brskalnika** odraža specifično instanco (**cBot ime · vrsta · simbol**, npr.
`TrendBot · Backtest · EURUSD`) tako da so zavihek živega teka in zavihek backtestiranja razlikovati na prvi pogled.
Tek in backtesting istega cBota se sledita kot jasni **liniji** (stabilen id linaje prenesenega
prek prehodov stanja), tako da stran sledi točno eni instanci in nikoli ne meša podatkov teka s podaci
backtestiranja.

## Kontrole v življenjskem ciklu instance

Vsaka vrstica instance (in njena stran s podrobnostmi) ima kontrole, ki so pravilne za stanje. **Aktivna** instanca prikaže
**Stop**; **končna** (`Stopped` / `Completed` / `Failed`) prikaže **Start (▶)** za ponovno zagon s
istim cBotom, računom, simbolom, časovnim okvirom, setom parametrov in sliko (tek se ponovno zažene kot tek, backtesting
kot backtesting). Klik Stop prikaže obvestilo "Stopping…" in izključi ikono dokler se ne razreši, in novo ustvarjen tek se pojavi v seznamu takoj — brez osvežitve strani.

Dnevniki konzole so **trajni ko se instanca zaključi** — za tek (ob Stop) in za
**backtesting** (ob zaključku) — zato dnevniki zadnjega teka ostanejo vidni na strani s podrobnostmi in,
prek orodni vrstice dnevnika, **kopirani v odložišče** (ikona Kopiran dnevnika) ali **preneseni** (ikona Preneseni dnevnika)
tudi ko je kontejner pošen. Oboje deluje na popolnem dnevniku konzole instance, ne samo na
vidni rep.

Naloženega `.algo` nikoli ni bilo zgrajeno tukaj, zato je njegov stolpec **Last Build** na strani cBotov
prazen (prikazuje čas gradnje samo za cBote, ki jih gradite v brskalniki).

## Urejanje in ponovno zaganjanje ustavljene instance

**Ustavljena** instanca (tek ali backtesting) ima kontrolo **Edit** — ikono v njenem vrsti v seznamu **in**
poleg Start/Stop na njeni strani s podrobnostmi — ki odpre dialog **prepolnjen** s svojo trenutno konfiguracijo.
Lahko spremenite **račun za trgovanje, simbol, časovni okvir, nabor parametrov in oznako slike** (in za
backtesting, **okno in vse nastavitve backtestiranja** zgoraj), nato **Save & start** ponovno zažene s
novimi nastavitvami (zamenja ustavljeno instanco). Kontrola je **onemogočena medtem ko je instanca aktivna** —
samo ustavljeno instanco je mogoče urejati.

## Pogon iz urejevalnika kode

Klik **Run** v urejevalniку kode odpre dialog namesto da izstrelite slepo, tvrdo programirano tekmo:

- **Trading account** (zahtevano) — račun cTrader, s katerim se cBot povezuje.
- **Parameter set** (opcijsko) — izbrite obstoječi set ali ga pustite praznega za pogon z cBotom
  **privzete vrednosti parametrov**. Gumb **+** poleg izbirnika ustvari nov nabor parametrov
  vgrajen (poglejte spodaj) in ga izbere.
- **Symbol / Timeframe** privzeto `EURUSD` / `h1` in se lahko spremenita; **Cancel** ali **Run**.

Pri **Run** urejevalnik shrani + kompajlira trenutni vir, zažene instanco na izbranem računu
s izbranimi parametri, nato sledira živim dnevnikom kontejnerja. (Tok dnevnika posreduje varnostni piskotok
prijavljenega uporabnika pijavici `/hubs/logs` SignalR, tako da se poveže namesto da ne uspe z
`Invalid negotiation response received`.)

## Nabori parametrov

**Parameter set** je imenovan, ponovno uporabljiv set prepisa parametra cBota, shranjen kot raven JSON
predmet, ki vsak naziv parametra preslika na skalarno vrednost, npr. `{"Period": 14, "Label": "trend"}`. Pri
teku/backtestiranju se pretvori v datoteko cTrader `params.cbotset`
(`{ "Parameters": { … } }`). Lahko ustvarite/uredite nabor kot surov JSON iz dialoga cBota **Parameter
sets** ali vgrajen iz dialoga Run.

Vsak nabor parametrov **pripada cBoту**: dialog New Parameter Set navede vse vaše cBote in vi
**morate izbrati enega** — ustvarjanje je blokirano, dokler ni izbran cBot. **Ime nabora je edinstveno na cBot**:
ustvarjanje ali preimenovanje nabora v ime, ki ga že uporablja drug nabor istega cBota, je zavrnjeno (jasna
napaka v dialogu, `409 Conflict` pri API). Isto ime se lahko ponovno uporabi na **drugem** cBoту.

JSON je **preverjen** ob shranjevanju: mora biti en sam raven predmet, katerega vrednosti so vse skalarne
(niz / število / bool). Koren, ki ni predmet, niz, ugniježđeni predmet, vrednost `null` ali slabo oblikovan
JSON je zavrnjen (jasna napaka v dialogu, `400 Bad Request` pri API). Prazen predmet `{}`
je dovoljen in pomeni "brez prepisa".

## Opombe CLI cTrader Console

Backtesti potrebujejo `--data-mode` (privzeto `m1`), datume kot `dd/MM/yyyy HH:mm`, in
`params.cbotset` JSON argument na položaju; `run` zavrne `--data-dir` (samo backtesting). Poglejte
`ContainerCommandHelpers`.

## Vozlišča in lestvica

Zmogljivost izvedbe se povečuje z dodajanjem agentov vozlišča (samoregistracija + utrip). Oglejte si
[odkrivanje vozlišča](../operations/node-discovery.md) in [skaliranje](../deployment/scaling.md).

## Zahtevam se račun za trgovanje

Pogon ali backtesting cBota potrebuje račun za trgovanje cTrader, se poveže. Dokler ne dodate enega pod
**Trading accounts**, sta gumba **Run New cBot** / **Backtest New cBot** onemogočena (z
napovedjo) in stran prikaže poziv, ki se povezuje do nastavitve računa — ne boste več prejeli surove
napake `stream connect failed` od bota brez računa.
