---
description: "Agent Studio — ustvarite agente brez osebnosti-vozila brez osebnosti z značajem in arhitekturo, ki upravljajo račune proti vašim ciljem pod jedrom Avtonomije in varnosti (obvojnica tveganja, prekinjač vezja, smrtonosni stikalo, verzioniran umor)."
---

# Agent Studio

Agent Studio vam omogoča, da ustvarite **agenta s personažem** — brez kode — in mu dodelite upravljanje
vaših računov proti merljivih ciljem. Agent je kot osebnost-vožena cBot: izberite arhitekturo
in stališče, nastavite varovaje in teče pod **Avtonomije in varnosti jedrom**.

Odprite **AI → Agent Studio** (`/agent-studio`).

## Ustvarite agenta

Razlog za **novega agenta** zbira, brez kode:

- **Ime** in **arhitektura** — Scalper, Day Trader, Swing Trader, Position Trader, News Trader,
  Contrarian, Mean Reversion ali Breakout/Momentum. Vsak prednastavite fiksne razumno kadence in drži.
- **Stališče** — agresivnosti, potrpljenja in trend-sledenja drsnikov.
- **Upravljani račun(i)** — **najmanj en je potreben za ustvarjanje agenta** (agent brez računa nikoli ne bi mogel začeti, zato *Ustvari* ostane onemogočen, dokler ga ne izberete). Če še niste povezali trgovalnega računa, bo dialog to povedal in vas usmeril, da ga najprej povežete.
- **Raven avtonomije** — **Svetovalnega** (predlaga samo) ali **Odobritev-vrat** (deluje samo po vašem
  na-delovanje odobritev). **Polne Avto** (nobenega na-trgovini odobritev) tudi zahteva **obvojnico tveganja**
  in sprejetje tveganja umora pred rukovanjem.

Persona prevede **deterministično** v agentov sistem poziva (nobeno LLM avtorjev), zato isto
konfiguracija vedno proizvede iste navodila — ponovljiv in pregledljiv.

## Rosterja

Vsak agent se pojavi v tabeli nadzorne sobe: **kateri agent, njegov tip, koliko računov upravlja, njegove
cilje, status teka, in zadnje delovanje**, z **Start / Stop / Kill** kontrolo. Smrtonosni stikalo zaustavlja a
tekočega agenta takoj.

## Varnost je domenska invarianta, ne nastavitev

Vsi denarni dotikajo poti skozi **Avtonomije in varnosti jedrom**:

- **Obvojnica tveganja** — trdo na-naročilo meje (maks dnevna izguba, odprta izpostavljenost, velikost položaja, vzvod,
  zaporedne izgube, naročila/uro, dovoljeni simboli). Vsako naročilo je preverjeno proti njej pred pošiljanjem;
  kršitev je zavrnjena, ne stisnjeno. Potrebno pred agentom lahko dosežemo Polne Avto.
- **Prekinjač vezja** — deterministično zaustavlja novo tveganje na izgube niz, dnevni-izguba kršitev, a **trd
  zmogljivost-cilj kršitev**, ali **AI-ponudnik nedostopnost** (dol ali halluciniranje model nikoli odprte
  sveže položaje).
- **Verzioniran umor dovoljenja** — a edine-čas, verzioniran sprejetje zahtevano za rukovanjem Polne Avto
  (zakonito-potrebo sprejetje, ne na-trgovini odobritev); bumpanje umora sili ponovno-pristanke.
- **Smrtonosni stikalo** — an idempotent nujni preprečitev na vsakem tekoči agent.

## Cilje

Dajte agenta **merljive namene** — npr *obdržite maks padec spodaj 4%*, *dejavnik dobička najmanj
1.5*, *stopnja zmage ≥ 55%*. Vsak cilj je **Trda** (a varovaje — a kršitev potuje vezje prevoz) ali
**Mehka** (usmerja sklepanje samo), ovrednoteno kot V-nasledku / V-tveganju / Prelomljeno.

## Odločitve potoka

Ko je začelo, agent teče a **24/7 nadzorovan zanko** (`AgentRuntimeService`). Vsak čas, za vsako
upravljan račun, se prebere **deterministična stanja računa** (resnico tal, nikoli modela spomin);
prosi odločitve motor za potez; ga prosledi skozi **varnostni vrata** (`AgentDecisionProcessor`) —
avtonomija raven → prekinjač vezja → obvojnica tveganja; piše a dodajanje samo **`AgentDecisionRecord`**; in
zaustavlja ali izvršuje kot vrata direktiv. Zanko je **napaka-izolirana** (eni agent napaka nikoli dotakne
drugo ali gostitelja) in **varna privzeto**: je neroden razen če je AI konfiguriran *in*
`App:Ai:AgentRuntimeEnabled` je nastavljen, in nikoli odpre sveže tveganje, medtem ko je AI ponudnik nedostopen.

- **Odobritev vrata** — an **Odobritev-vrat** agenta predlagano naročilo je zabeleženo kot **Pending** in ne
  nič dokler lastnik ne odobri (`POST /api/agent-studio/{id}/decisions/{seq}/approve` ali
  `/reject`); **Polne Avto** očisti skozi obvojnico z nobeno na-trgovini odobritev; **Svetovalnega** samo
  predlaga.
- **Revizija knjiga** — vsaka odločitev je ponovno igrajte: razloga (XAI), dokaze, ki jih je navedel, vrata
  priznanje, namero naročila in ali se je izvršilo, na `GET /api/agent-studio/{id}/decisions`.
- **Raziskovanje klopi** — an na-zahtevo več-agent razpravi: Alpha/Sentiment/Technical/Risk analitikov vsak daj
  pogled in a Reviewer sintetizira predlog (`POST /api/agent-studio/{id}/debate`).
- **Spomin** — agent se spomni vsake odločitve in spomin nedavnega v njegov naslednji poziva za
  kontinuiteto (`GET /api/agent-studio/{id}/memory`).

Vsak rosterja vrsto **Podrobnosti** odpre agentov odločitve dovoliti (z Odobritev/Zavrni na preglede naročila),
jego spomin, in a Teči-razpravi kartici.

## Obseg

Dobavljen: polna agenta cikel, deterministična varnostni vrata, 24/7 časa izvajanja, človek-v-zanki
odobritev vrata, revizija knjiga, in **živo cTrader Open API integracija** — račun-stanja trgovino
(bere pravi saldo, položaje in odprto izpostavljenost v parcelah) in naročilo izvajalec (kraji pravi trg
naročila, parcelam→volumen preko simbola parcel velikost), oba reševanja vsake upravljane račun OAuth poverilnice in
degradiranje varno, ko račun ni povezan. **Zahteva Anthropic API ključ** za model do
ustvarite naročila (do takrat motor drži); še korej bi prišli več-agent razpravi vloge in povrstveno
spomin/razmislek. Čas izvajanja je off razen če je `App:Ai:AgentRuntimeEnabled` nastavljen, zato živo trgovanja samo
se zgodi na izrecni, v celoti-prepričan opt-in.

## Upravljani računi in ureditev

Pri ustvarjanju agenta izberete trgovalnik račun(e), ki jih upravlja — **najmanj en je potreben pri ustvarjanju** (gumb *Ustvari* je onemogočen, dokler ga ne izberete, in končna točka za ustvarjanje zavrne prazno izbiro). Vsak agent se lahko **uredi** pozneje (ime, značaj, avtonomija in upravljani računi) iz svinčnika ikone v njegovo vrsto seznama. Kontrolniki životnega cikla (podrobnosti, ureja, začnite, ustavite, ubijte) so gumbki ikon, vsak onemogočen v stanjih, kjer dejanje ne velja.
