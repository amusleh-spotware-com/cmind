---
title: AI makro valutna moč in naprej pogled
description: "cMind pošilja AI-podprt, matematično determinističen motor za moč valute. Razvrsti konfigurabilno vesolje valut po trenutni temeljni moči in projicira smer改革开放 naprej."
---

# AI makro valutna moč in naprej pogled

cMind pošilja **AI-podprt, matematično determinističen** motor za moč valute. Razvrsti
konfigurabilno vesolje valut — 8 glavnih plus nastajajoče trge in eksotične valute — po
**trenutni** temeljni moči in projicira **naprej smeren pogled** za vsak par čez izbrano obdobje (1M / 3M / 6M / 12M). Vsak ranking, vsak par bias in vsako število izračuna čista
deterministična matematika v jedru domene; LLM samo **zbere** naprej pogledne vnose ki jih podatki
ne morejo objaviti in **razloži** rezultat v navadni angleščini. Nikoli ne izumi rankinga, smeri ali
števila.

> **Poštena omejitev.** Temelji napovedujejo srednjo-dolgo vrednost dobro in kratkoročno slabo. Obravnavaj to kot filter pozicioniranja / sovpadaanja, **ne** kot kratkoročen časovni signal. Odčitki blizu visoko-vplivnih objav (NFP/CPI/centralna banka) so šumni. Ne finančni nasvet.

## Kako deluje

1. **Trenutni temelji prihajajo iz Ekonomiskega koledarja, ne LLM.** Trda števila — obrestne mere, CPI proti cilju, GDP, zaposlenost, trgovinska bilanca — in njihove **presenečene z-vrednosti** so pridobljene **točno-v času** iz [ekonomiskega koledarja](./economic-calendar.md) modul (FRED/BLS/BEA/ECB in koledarji centralnih bank). Zgodovinski posnetek nikoli ne izda naprej pogleda.
2. **LLM zbere samo kar koledar ne more objaviti** — na valuto: **naprej** trajektorijo (pričakovana pot obrestne mere v bp, inflacijski trend-proti-cilju, rast momentuma) in **geopolitičen** pogled (tveganje-vključeno/izključeno, carine, fiskalni/dolžniški, volitve), plus katerakoli EM/eksotična trenutna števila ki jih koledar nima. Striktni JSON, tier-aware validacija, spletno iskanje vključeno.
3. **Domena deterministično izračuna ranking in matriko naprej.** Vsak gonilnik je točkovan kot **within-tier z-vrednost** (torej 50%-inflacija eksotika nikoli ne popači glavnih), winsoriziran,
   utežno-seštet v kompozit in rangiran najmočnejši→najšibkejši s stabilnim ISO tie-break. Naprej
   plast nosi vsak kompozit po svoji trajektoriji —
   `projected = current + horizonScale · Σ trajectoryDriver·weight` — in preslika vsakega para projicirano
   diferencial v **direzionalen bias** (▲ apreciacija / ▬ nevtralen / ▼ depreciacija) s prepričanjem.
4. **LLM razloži** ranking in zgornje pari klice v navadnem jeziku.

## Gonilniki

| Gonilnik | Učinek na moč | Opombe |
|---|---|---|
| Obrestna mera in trajektorija | Višja / jastrebova ⇒ močnejša | Najvišja utež; divergenca centralne banke poganja največje vrzeli. |
| Inflacija (CPI proti cilju) | Nad ciljem ⇒ šibkejša | Točkovano obratno (izguba kupne moči). |
| Rast GDP | Višja relativna rast ⇒ močnejša | Diferencial proti plošči. |
| Zaposlenost | Močnejše delo ⇒ močnejša | Hrani pot obrestne mere. |
| Trgovinska bilanca / tekoči račun | Presežek ⇒ močnejši | Strukturna povpraševanja. |
| Obrestna drža | Jastrebova ⇒ močnejša | Primarni dolgoročni gonilnik. |
| Presenečeni moment | Nedavni presenki ⇒ močnejši | Iz z-vrednosti presenek koledarja. |
| Geopolitični / tveganje | Tveganje-izključeno ⇒ varne luži (USD/JPY/CHF) močnejše | Omejen naprej tveganje delta. |
| Realni prinos / carry *(EM/eksotiki)* | Pozitivna realna stopnja ⇒ močnejša | Dominanten EM gonilnik v mirnih režimih. |
| Zunanja ranljivost *(EM/eksotiki)* | Primanjkljaji / nizke rezerve / USD dolg ⇒ šibkejša | Strukturni deprecijacijski pritisk. |
| Menjalni pogoji *(komoditni izvozniki)* | Rastoče izvozne cene ⇒ močnejši | BRL, ZAR, CLP, NOK, AUD, CAD. |
| Politično/institucijsko tveganje *(EM/eksotiki)* | Nestabilnost ⇒ šibkejša | Širši mrtvi pas, omejeno prepričanje. |

## Tiered vesolje (glavni + EM + eksotiki)

Vesolje je **konfigurabilno ob namestitvi** (`App:CurrencyStrength:Universe`) — dodajanje valute je
konfiguracija, ne koda. Vsaka valuta ima **tier** (`Major` / `EmergingMarket` / `Exotic`) ki prilagodi
uteževanje, širino mrtvega pasu in omejitev prepričanja:

- **Glavni** — USD, EUR, GBP, JPY, AUD, NZD, CAD, CHF (vodeno z raven obrestne mere).
- **Nastajajoči trgi** — CNH, INR, BRL, MXN, ZAR, KRW, SGD, PLN (+ Skandinavska NOK/SEK); carry + tveganje +
  zunanja ranljivost utežena navzgor, srednje zaupanje.
- **Eksotiki** — TRY, HUF, CZK, plus USD-pegged HKD/SAR; nizko zaupanje, širši mrtvi pas, omejeno
  prepričanje. **Pegged / močno upravljane** valute (HKD, SAR, CNH) so označene, njihova trajektorija je
  podtehtana, in njihov par pogled je stisnjen proti `Neutral`, torej peg nikoli ne bere kot
  prostofloat signal.

Ker so uradni EM/eksotični statistiki nižje frekvence, revidirani in včasih neprozorni, AI-zbrani
številke nosijo **per-tier zaupanje** prikazano kot zanesljivostna značka.

## Gladka degradacija

| Koledar | AI | Rezultat |
|---|---|---|
| ✅ | ✅ | Poln ranking + naprej projekcija + pripoved (`CalendarAndAi`). |
| ✅ | ❌ | Samo koledar trenutni ranking, brez naprej projekcije (`CalendarOnly`). |
| ❌ | ✅ | AI-zbrani trenutni številke + naprej, nižje zaupanje (`AiOnly`). |
| ❌ | ❌ | No snapshot — widget se skrije in stran kaže prazno stanje. |

Aplikacija teče nespremenjena v obe smeri. AI je na vratih AI ključa; koledarjev del spoštuje svoja
white-label vrata + runtime preklopnik.

## Uporaba

- **Omogoči AI** (Settings → AI) in **vključi widget** iz lastne nadzorne plošče **Prilagodi** dialog
  ("Currency strength" — opt-in, privzeto skrit). Widget prikaže najmočnejše/najšibkejše valute in
  naj 3M par klic; povezuje na polno stran.
- **Polna stran** — `/ai/currency-strength`: izbirnik obdobja (1M/3M/6M/12M), filter tier
  (Vsi/Glavni/EM/Eksotiki), trenutni ranking, naprej napoved, matrika pogleda para (bias +
  prepričanje, pegged/nizko zaupanje označeno), in AI pripoved. Pritisni **Osveži zdaj** (lastnik) za
  regeneracijo. Ozadnji delavec (`App:CurrencyStrength:RefreshEnabled`, **privzeto `true`**) osvežuje po
  urniku tako da je stran napolnjena iz škatle; namestitev ali lastnik jo izključi (ali onemogoči
  AI / ekonomiski koledar funkcijo, ki jo osveževalec spoštuje z degradacijo v no snapshot).

## Programski dostop

En deljen model branje (`ICurrencyStrengthQuery`) je dosegljiv na tri načine:

- **In-app AI** — injiciran naravnost (in-process) v AI funkcije.
- **MCP** — `currency_strength` orodje (parametri `horizon`, `tier`) za AI odjemalce/agente.
- **cBot REST** — `GET /api/market/v1/currency-strength/{latest,history,pair/{base}/{quote}}`, zavarovan
  z isto `CalendarJwt` mašinerijo kot [koledar cBot API](./calendar-cbot-api.md) z dodanim
  **`market:read`** obsegom. cBot registrira API odjemalca z `market:read`, zamenja svoj
  id + skrivnost za kratkoživi JWT pri `POST /api/calendar/v1/token`, in kliče končne točke z
  `Bearer` žetonom. Brez druge sheme JWT, brez druge skrivnosti — razkrit žeton je samo za branje, tržišče,
  kratkoživi in preklicni.

Glej [koledar cBot API](./calendar-cbot-api.md) za tok žetona in copy-paste vzorec.
