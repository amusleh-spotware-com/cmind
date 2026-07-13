---
title: Ekonomiski koledar
description: "cMind pošilja svoj lasten ekonomiski koledar — urnik objav, dejanski podatki, napovedi, revizije in model vpliva na podlagi podatkov — viran iz primarnih avtoritet, z nič odvisnosti od aggregatorjev."
---

# Ekonomiski koledar

cMind pošilja svoj **lasten** ekonomiski koledar — urnik objav, dejanski podatki, napovedi, revizije in
model vpliva na podlagi podatkov — viran iz **primarnih avtoritet** (centralne banke in narodne
statistične agencije), z **nič odvisnosti** od ForexFactory, FXStreet, Investing.com ali katerega koli
agregatorja. Je točkovno-pravilen, hrani ≥10 let zgodovine in je napeljan v trgovanje,
javni API, MCP, cBote, AI, opozorila in backteste. Je decupliran modul: lahko je onemogočen z
ničelnim učinkom na trgovalno jedro.

> **Status.** Jedro domene (model vpliva, država→simbol preslikava, politika okna novice, točkovno-čas
> verige revizij, dvoslojna vrata) **in** vztrajnost (shema `calendar` Postgres, append-only
> branje/pisanje stran, FRED connector in config-gate ingestion worker) sta implementirana in testirana
> (enote + Testcontainers integracija). JWT REST API, MCP orodja in UI pristanejo v kasnejših
> fazah opisanih spodaj.

## Kaj ga ločuje

Ponavljajoče se pritožbe zoper vodilne koledarje so postale naše projektne omejitve:

- **Brez tihih sprememb ocene vpliva.** Naša ocena vpliva je **deterministična, verzionirana in revizijska**. Vsaka sprememba je posnetek z časovnim žigom — nikoli tiho prepisovanje. Uporabnik lahko vidi natančno *zakaj* je dogodek Visok.
- **En UTC sidro na dogodek.** Vsak dogodek je sidran na en UTC trenutek iz uradnega urnika primarnega
  vira; časovni pas vira je shranjen in upodabljanje na uporabnika uporablja eksplicitno IANA časovno
  cono z DST rokovanjem z cono baze podatkov — nikoli ročni ±1h preklop.
- **Polne verige revizij, povsod.** Izvirna vrednost in vsaka revizija sta prvorazredna, izpostavljen
  identično čez API, MCP in cBot površine.
- **≥10 let zgodovine, brez zidu.** Neomejen razpon brskanja; brez 60-dnevnega limita, brez registracijskih vrat.
- **Točkovno-čas po konstrukciji.** Vsako dejstvo nosi `KnownAt` (kdaj smo *mi* izvedeli zanj) in
  `EffectiveAt` (trenutek dogodka). "Kot je koledar izgledal ob času T" je prvorazredno poizvedovanje, torej
  backtestirano pravilo novice se obnaša natančno kot v živo — brez naprej pogleda iz uporabe revidiranih vrednosti v zgodovini.

## Model vpliva

Točkovni rezultat vpliva je čista, deterministična funkcija v `[0, 100]`, pasovna do Nizka / Srednja / Visoka /
Kritična. Njeni vnosi so samo podatki znani ob času točkovanja (brez futurovega puščanja):

- **Serija pred** — osnovna utež na razred indikatorja (odločitev obrestne mere pretehta CPI, ki
  pretehta manjšo anketo).
- **Odtujeno-volatility odtis** — median absolutni donosi primarnih prizadetih simbolov v
  oknu po *preteklih* objavah te serije: "ta objava zgodovinsko premakne ceno toliko."
- **Presenečenjska občutljivost** — kako močno je абсолутна presenečenje (z-vrednost) zgodovinsko
  koreliralo s premikom po objavi.

Točkovnik zmeša te s fiksnimi utežmi in žig `ImpactModelVersion`. Ponovni izračun je
ekspliciten, beležen prenos, ki proizvede **novo revizijo** — nikoli mutiranje — torej ocena je vedno
reproducirajna iz svojih vnosov.

## Država → valuta → preslikava simbolov

Najpogosteje citiran algoritem integracijski papir je rešen enkrat, kot čista funkcija: država mapira v
svojo valuto (vsako evro-območje član fan v EUR), in valuta mapira v spremljane simbole
ki kotirajo na katerikoli nogi. Torej **EURUSD je prizadet od EU in US dogodkov**; XAUUSD je izpostavljen USD.
US500 mapira v USD. To poganja filter novice, resolucijo prizadetih simbolov in blackout matematiko.

## Politika okna novice

`NewsWindowRule` je `{ minImpact, beforeMinutes, afterMinutes, currencies?, series? }`. Ena,
deljena, čista implementacija odgovarja "ali je trenutek T znotraj blackouta za simbol S?" —
uporabljena s cBot filterjem novice, pavziranjem kopiraj-trgovanje in AI varovalko tveganja, torej
ne morejo razhajati. Ob negotovosti blackout odgovor privzame konfigurirano konzervativno vrednost
(privzeto zaprto napake) torej vrzel podatkov nikoli ne izda tiho zelene luči za trgovanje skozi
visoko-vplivno objavo.

## Točkovno-čas in revizije

Dejanski podatki, napovedi in ocene vpliva so **append-only**. Vsak dogodek ima verigo
naročeno zaporedje revizij, monotono v `KnownAt`:

- `Scheduled` — dogodek je bil prvič načrtovan (predhodni vpliv, brez dejanskih).
- `Released` — prvi natisnjena dejanska prišla.
- `Revised` — kasnejša revidirana vrednost prispela.
- `Rescheduled` — vir premaknil uro objave (revizijska, opozorljiva).
- `Rescored` — ocena vpliva ponovno izračunana pod novo verzijo modela.

Poizvedba `as of` preteklega trenutka vrne natančno revizijo takrat znano — garancija ki ubije
naprej pogled v backtestiranih pravilih novice.

## Napoved / konsenz

Mediana ankete ekonomistov **ni** prosto objavljena s strani primarnih virov — je
agregatorjeva lastniška dodana vrednost in je ne izmišljujemo. Shema dogodka nosi nullable
`Forecast`; namestitev lahko napelje licenčiran feed konsenza prek izbirnega `IForecastProvider`
porta (prinesi lastni ključ, privzeto izključeno). Prejšnje vrednosti in revizije vedno prihajajo iz uradnega
vira.

## Viri podatkov

Dva decuplirana sloja, vsi primarni — nikoli aggregator:

- **Urnik / časovni podatki:** FRED koledar objav; narodne statistične agencije (BLS, BEA, Census,
  Eurostat, ONS, Destatis, INSEE, e-Stat, ABS, StatCan); koledarji sestankov centralnih bank (Fed, ECB,
  BoE, BoJ, RBA, BoC, SNB, RBNZ).
- **Dejanske vrednosti:** FRED (z datumi vinjet za revizije in točkovno-čas), plus BLS, BEA, Census,
  ECB SDW, Eurostat in OECD SDMX API.

Mrtev vir degradira pokritost **samo za ta vir**; koledar še vedno streže vse ostalo
in površini vrzel kot mero svežine.

## Omejevanje hitrosti in načrt za rezervo

Zunanji ponudniki objavljajo omejitve hitrosti (FRED dovoljuje ~120 zahtevkov/minuto). Koledar je zgrajen tako da
**nikoli ne sproži limita ponudnika**, in da biti throttled ali odrezan nikoli ne degradira branj:

- **Proaktivno throttle.** Vsak HTTP odjemalec vira gre prek deljene, threadsafe rate vrata
  ki razporeja odhodne zahtevke na konfiguriran proračun (`App:Calendar:FredRequestsPerMinute`, privzeto
  100 — namenoma pod stropom ponudnika). Zahtevki so vrsta in razporejeni, nikoli ne bušeni.
- **Spoštuj `429 Retry-After`.** Če ponudnik kadar koli vrne `429 Too Many Requests`, vrata backs off
  celoten vir za server-requested cooldown (ali `App:Calendar:RateLimitBackoff`, privzeto 60s)
  prej naslednjega klica — nobene tesne zanke ponovitev.
- **Standardna odpornost.** Vsak odjemalec vira prav tako podeduje app-wide obravnavo odpornosti (ponovitev z
  backoff + jitter, vezje varovalka, časovne omejitve), torej prehodni izpadi so absorbirani in vztrajno
  neuspešen vir je parkiran (njegova pokritost postane zastarela) brez vpliva na druge.
- **Načrt rezerve — trajni read-through predpomnilnik.** Branje **nikoli ne strežejo s klicanjem
  ponudnika**. Ko je obseg enkrat pridobljen, je vztrajno append-only v Postgres in strežen iz
  tam za vedno (glej §"Zahtevo-naloži"). Torej celo ko je vir rate-limited ali down, koledar
  še vedno odgovarja iz predpomnenih, točkovno-časovno pravilnih podatkov; manjkajoči razpon preprosto ostane
  nepokrit in se poskusi na naslednjem ciklu ingestije. Blackout odgovori dodatno propadejo v
  konzervativni privzeti pod negotovostjo, torej vrzel podatkov nikoli ne izda zelene luči za trgovanje skozi objavo.
- **Poceni polling.** Pogojni fetch (ETag / If-Modified-Since / kurzorji vinjet vira) in
  "fetch obseg enkrat, nikoli znova" predpomnilnik ohranjata dejansko prostornino zahtevkov daleč pod katerim koli limtom v normalni
  operaciji — vrata hitrosti so varnostna mreža, ne skupna pot.

## Omogočanje / onemogočanje

Dve neodvisni plasti, natančno kot druge cMind funkcije:

- **Plast 1 — runtime funkcijska zastavica** (`Feature.EconomicCalendar`) preklopljena iz Features admin UI;
  brez ponovnega uvajanja, učinkuje v živo.
- **Plast 2 — white-label trda vrata** (`App:Branding:EnableEconomicCalendar`, privzeto `true`).
  Preprodajalec jo nastavi na `false` da v celoti odstrani funkcijo; operater potem ne more
  ponovno omogočiti.

Efektivno stanje je `Branding.EnableEconomicCalendar && FeatureToggle.EconomicCalendar`. Ko onemogočeno,
vnos nav je skrit in `/economic-calendar`, `/api/calendar/**` ter MCP koledar orodja vrnejo
čisto feature-onemogočeno `404` — nikoli `500`. Vztrajana zgodovina je ohranjena ob runtime izklopu
tako da je ponovni vklop hiter.

## Fazni razvoj

- **P0 — jedro domene** *(implementirano)*: agregati, vrednostni objekti, porti, model vpliva,
  država→simbol preslikava, politika okna novice, dvoslojna vrata, polna enota suite.
- **P1 — vztrajnost + en vir** *(implementirano)*: EF shema `calendar` (lastne tabele, append-only,
  vroči indeksi), read-through `IEconomicCalendar` bralnik s točkovno-časom `asOf`, idempotentni
  append-only write storitev, FRED connector za hrbtno odporno tipiziranega odjemalca, in config-gated
  ingestion worker; Testcontainers integracijski testi (vztrajnost, PIT, idempotenca, blackout).
- **P2 — javni JWT REST API + Web UI** *(implementirano)*: verzioniran, JWT-zavarovan `/api/calendar/v1`
  API — izdaja odjemalca, zamenjava žetona, jedrne bralne končne točke (dogodki, zgodovina, serije,
  presenečenja, naslednji, blackout, prizadeti-simboli, zdravje) s stopnjevanjem obsega in dvoslojnimi vrati,
  integracija-testirano. Plus mobilno-prva **`/economic-calendar` stran** — na vratih, v celoti lokalizirana
  (23 jeziki) agenda prihodnjih objav kot prijazne kartice za telefon z barvno-pasovnimi čipi vpliva
  in MudBlazor **filter dialog** (valute + minimalni vpliv + **Od datuma** izbirnik da skoči na
  **kateri koli pretekli datum** čez celotno zgodovino — brez 60-dnevnega limita, brez zidu); nav vnos,
  smoke/mobilno/a11y/E2E testirano. **Stran zgodovine serije na indikator** (`/economic-calendar/series/{code}`, povezana
  z vsakega dogodka) navaja polno zgodovino natisov posamezne serije. Grafi presenektj + neskončno
  drsenje brskalnik sledi.
- **P3 — več virov in ogrevanje** *(začeto)*: **katalog osnovnih serij** (CPI, Jedrni CPI, NFP,
  brezposelnost, GDP, PCE, Fed funds, maloprodaja → njihovi FRED id-ji) je sejan avtomatsko ob startu,
  in enokratni, idempotentni, po-letih chunked **proaktivni backfill** potegne njihovo ≥10-letno zgodovino
  tako da je pogost primer topel brez čakanja da uporabnik kaj spregleda. **Ingestija je privzeto vključena**
  (`App:Calendar:IngestionEnabled`, privzeto `true`): **koledar centralne banke vir** potrebuje **ni API
  ključa**, torej FOMC / ECB / BoE koledar odločitev se napolni iz škatle — backfill seje te
  datume sestankov čez **obe pred kratkim zgodovini in naprej horizont**, torej brskanje *lanskega meseca* (ali
  katerokoli preteklo okno) kaže sestanke celo preden so nastavljeni FRED/BLS ključi; vrednostne serije se napolnijo
  ko so njihovi ključi nastavljeni. Delavci spoštujejo dvoslojna vrata koledarja — white-label namestitev
  ali lastnik ki onemogoči ekonomiski koledar funkcijo ustavi ingestijo, in `App:Calendar:IngestionEnabled=false`
  jo izključi eksplicitno. **Svežina na vir** je zdaj resnična: delavec beleži zadnji uspešni poll
  vsakega vira, zaporedje-neuspeh števca in zastavica tripped-vezja (vztrajana v app nastavitvah,
  cross-process), in `/health` končna točka + `calendar_health` MCP orodje poročata resnično
  `stale` sodbo na vir. **BLS** (2. vir vrednosti) in **koledar sestankov centralne banke** (FOMC / ECB /
  BoE datumi odločitev, backfillana čez zgodovino in sinhronizirana naprej v horizontovo okno s strani delavca)
  sta notri. Še vedno na poti: BEA/Census/ECB-SDW/Eurostat/OECD viri vrednosti in pomirjevalni prehod.
- **P4 — globoka integracija**: **MCP orodja** *(implementirano — polna parity bralnega API: `calendar_events`,
  `calendar_event`, `calendar_history`, `calendar_series`, `calendar_surprises`, `calendar_next`,
  `calendar_blackout`, `calendar_affected_symbols`, `calendar_health`, na vratih funkcije)* in
  **alerts `EconomicEvent` sprožilec** *(implementirano — `AlertRule` ki sproži N minut pred
  prihajajočo objavo pri/iznad izbranega vpliva, izbirno zožen na valute; ocenjen od
  obstoječega opozorilnega delavca brez AI, deduplikacijski na objavo; ustvarjen prek
  `POST /api/alerts/rules/economic-event`)*. Prop-guard novice-blackout vrata **in
  pavziranju kopiraj-trgovanje** sta notri (§5.1 — izbirni `App:Copy:NewsPauseEnabled`, privzeto off: odprt
  vir katerega simbol sedi v Kritičen-vpliv blackout je preskočen, byte-identična vroča pot ko off). **Backtest prekrivanj dogodkov** je notri — `GET /api/calendar/v1/for-symbol` in
  `calendar_events_for_symbol` MCP orodje vrneta točkovno-pravilne dogodke ki prizadenejo simbol v
  oknu, in **stran poročila instance/backtest** upodobi visoko-vplivne objave ki so padle
  znotraj okna backtest pod krivuljo equity (torej avtor vidi katere trgovine so pristale na NFP), na vratih in
  lokalizirano. Celoten načrt je zdaj implementiran.
- **P5 — dodatki**: presenečenjska analitika, iCal/CSV izvoz, iskanje po ključ besedi, priklopni konsenz.

Glej [cBot & REST API referenco](calendar-cbot-api.md) za integracijsko površino.
