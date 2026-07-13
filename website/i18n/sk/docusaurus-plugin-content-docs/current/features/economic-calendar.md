# Ekonomický kalendár

cMind dodáva jeho **vlastný** ekonomický kalendár — plán uvoľnenia, aktuálnosti, prognozy, revízie a dáta
-driven impact model — v zdroji z **primárnych autorít** (centrálnych bánk a národných
agentúr štatistiky), s **nula závislosť** na ForexFactory, FXStreet, Investing.com alebo akýchkoľvek
agregátore. Je bod-v-čase správny, udržiava ≥10 roky histórie a je zapojený do obchodovania, verejný
API, MCP, cBots, AI, upozornenia a backtesty. Je to oddelený modul: môže byť zakázaný s
nula efekt na obchodné jadro.

> **Stav.** Doménové jadro (impact model, krajina→symbol mapovanie, news-window politika, point-in-time
> reviduje reťazce, dvoj-tier gating) **a** persistence (schéma `calendar` Postgres, append-only
> čítať/napísať stranu, FRED konektor a config-gated ingestion worker) sú implementované a testované
> (jednotka + Testcontainers integrácia). JWT REST API, MCP nástroje a UI doLanding v nasledujúcom
> rollout fázy opísané nižšie.

## Čo ho robí iným

Opakujúce sa sťažnosti voči vedúcim kalendárom sa stali našimi dizajnérskymi obmedzeniami:

- **Žiadne tiché zmeny ratingovej vplyvnosti.** Náš impact rating je **deterministický, verzovaný a
  auditovateľný**. Každá zmena je zaznamenaná revízia s timestamp — nikdy tiché prepísanie. A
  používateľ môže vidieť presne *prečo* je udalosť High.
- **Jeden UTC kotva za udalosť.** Každá udalosť je zakotvená do jedného UTC okamžiku od primárneho
  zdroja oficiálny plán; zdroj vlastnej časovej pásy je uložený a per-user renderovanie používa
  explicitný IANA časová zóna s DST spravovaný databázou zóny — nikdy ručný ±1h toggle.
- **Úplné reviduje reťazce, všade.** Pôvodná hodnota a každá revízia sú first-class, exponované
  identicky cez API, MCP a cBot povrchy.
- **≥10 roky histórie, žiadna stena.** Neobmedzené prehliadajúce rozsah; žiadny 60-day cap, žiadna registrácia brána.
- **Point-in-time podľa konštrukcie.** Každá skutočnosť niesie `KnownAt` (kedy *sme* sa to dozvedeli) a
  `EffectiveAt` (udalosť okamžik). "Ako kalendár vyzerá čas T" je first-class dopyt, takže a
  backtestovaná news pravidlo chová sa presne ako live — žiadny look-ahead z použitia reviduje hodnoty v histórii.

## Impact model

Impact skóre je čisté, deterministické funkcie v `[0, 100]`, pásmo na Low / Medium / High /
Critical. Jej vstupy sú iba dáta poznané v čase skórovania (žiadny budúcnosť únik):

- **Series prior** — baseline váha na indikátor triedy (rozhodnutie ceny outwieghs CPI, ktorý
  outwieghs minor prieskum).
- **Realizované-volatility footprint** — stredný absolútny návrat primárneho postihnutého
  symboly v okne po tomto série *minulý* uvoľnenie: "táto uvoľnenie historicky pohyby cena toľko."
- **Prekvapenie citlivosť** — ako silne absolútny prekvapenie (z-skóre) má historicky
  korelácia s post-release povedomie.

Skóre zmiešava tieto s pevnými váhami a pečiatok `ImpactModelVersion`. Opätovne vypočítať je
explicitný, protokolovaný operácia, ktorá produkuje **nový revízii** — nikdy mutovať — takže skóre je vždy
reprodukovateľný z jeho vstupy.

## Krajina → mena → symbol mapovanie

Jediný najčastejšie citovaný algo integrácia papercut je riešené raz, ako čistý funkcie: a krajina mapuje do
Its mena (každý euro-area člen fanů v na EUR), a mena mapuje do
watchlist symboly citujú to buď nohu. Takže **EURUSD je postihnutý obidvoma EU a US udalosti**; XAUUSD je USD-exponovaný;
US500 mapy USD. To pohány news filter, postihnutý-symboly rozlíšenie a blackout matematika.

## News-window politika

A `NewsWindowRule` je `{ minImpact, beforeMinutes, afterMinutes, currencies?, series? }`. Jeden jediný,
zdieľaný, čisté implementácia odpovede "je okamžik T vnútri blackout na symbol S?" — používaný cBot
news filter, copy-trade pause a AI risk guard, takže oni nikdy divergovať. Na neistota a
blackout odpoveď štandardne na nakonfigurovaný konzervatívny hodnota (fail-closed štandardne) takže a data
medzera nikdy Silent green-lights obchodovania cez high-impact uvoľnenie.

## Point-in-time & revízie

Aktuálnosti, prognozy a impact skóre sú **append-only**. Každá udalosť vlastní objednaný reťazec
revízií, monotónny v `KnownAt`:

- `Scheduled` — udalosť bola prvý naplánovaný (prior impact, bez skutočný).
- `Released` — prvý tlačená skutočný dorazil.
- `Revised` — neskôr opravená hodnota dorazila.
- `Rescheduled` — zdroj presunul uvoľnenie okamžik (auditovateľný, alertable).
- `Rescored` — impact skóre bolo prepočítané pod nový model verzia.

Dopytovanie `as of` minulé okamžik vracia presne revízii známy potom — záruka, že zabíja
look-ahead v backtestovaný news pravidlá.

## Prognóza / konsenzus

Prieskum medián ekonómy je **nie** voľne publikované primárnym zdrojom — to je
agregátorov proprietárne value-add a my sa nemôžeme fabricate. A event schéma nesie
nullable `Forecast`; nasadení môže drôt licencované konsenzus feed cez voliteľný `IForecastProvider`
prístav (prinesie-your-own kľúč, vypnutý štandardne). Predchádzajúce hodnoty a revízie vždy pochádzajú z oficiálneho
zdroj.

## Dáta zdroje

Dve oddelené vrstvy, všetky primárne — nikdy agregátore:

- **Plán / načasovanie:** FRED uvoľnenie kalendár; národné štatistické agentúry (BLS, BEA, Census,
  Eurostat, ONS, Destatis, INSEE, e-Stat, ABS, StatCan); centrálna-bank stretnutie kalendáre (Fed, ECB,
  BoE, BoJ, RBA, BoC, SNB, RBNZ).
- **Skutočné hodnoty:** FRED (s vintage dátumy na revízie a point-in-time), plus BLS, BEA, Census,
  ECB SDW, Eurostat a OECD SDMX APIs.

Mŕtvy zdroj degraduje pokrytie **tú zdroj iba**; kalendár udržiava slúžiť všetko ostatné
a povrchy medzeru ako freshness metriku.

## Rate limiting & backup plán

Vonkajší poskytovatelia publikujú rate limitov (FRED umožňuje ~120 požiadavky/minúta). Kalendár je postavený takže to
**nikdy trip poskytovania limit**, a takže bytia škrtené alebo cut off nikdy degraduje čítať:

- **Proaktívny throttling.** Každý zdroj HTTP klient ide cez zdieľaný, thread-safe rate brána
  ktorý priestor odchdzajúci požiadavky na nakonfigurovaný rozpočtu (`App:Calendar:FredRequestsPerMinute`, štandardne
  100 — zámyselne pod poskytovateľa strop). Požiadavky sú zařadené a tempované, nikdy bursted.
- **Honor `429 Retry-After`.** Ak poskytovateľ kedy vracia `429 Too Many Requests`, brána zálohy
  celú zdroj vypnutí server-požiadané cooldown (alebo `App:Calendar:RateLimitBackoff`, štandardne 60s)
  pred ďalšou volaní — bez Napätý retry slučka.
- **Štandardný odolnosti.** Každý zdroj klient tiež dedičí app-wide resilience handler (retry s
  backoff + jitter, circuit breaker, timeouts), takže prechodný blips sú absorbované a a persistentne
  zlyhajúci zdroj je zaparkovaný (Its pokrytie ide zastaraný) bez afekcie ostatných.
- **Backup plán — trvalý read-through cache.** Čítame sú **nikdy** podávané volaní poskytovania.
  Raz rozsah je načítaný to je trvalé append-only na Postgres a podávané z tam
  navždy po (vidieť §"On-demand load"). Takže aj keď a zdroj je rate-limited alebo dole,
  kalendár ponúka odpoveďe z cached, point-in-time-správne dáta; chýbajúci span jednoducho zostať
  nepokryté a je opakované na ďalšom ingestion cyklus. Blackout odpovede dodatočne zlyhajú na konzervatívny
  štandardne pod neistota, takže a data medzera nikdy green-lights obchodovania cez uvoľnenie.
- **Lacný polling.** Podmienené fetch (ETag / If-Modified-Since / zdroj vintage kurzory) a
  "fetch a span raz, nikdy znova" cache udržiavať skutočný požiadavka objem ďaleko pod žiadny limit v normálne
  prevádzka — rate brána je bezpečnostný sieť, nie obvyklý cesta.

## Povoliť / zakázať

Dva nezávislé vrstvy, presne ako ďalší cMind funkcie:

- **Vrstva 1 — runtime feature toggle** (`Feature.EconomicCalendar`) pretočené z Features admin UI;
  žádný redeploy, trvá účinok live.
- **Vrstva 2 — white-label hard brána** (`App:Branding:EnableEconomicCalendar`, štandardne `true`). A
  reseller nastaví ho `false` na úplné odstránenie funkcie; operátor potom nemôže re-enable to.

Efektívny stav je `Branding.EnableEconomicCalendar && FeatureToggle.EconomicCalendar`. Keď zakázaný,
nav položka je skrytá a `/economic-calendar`, `/api/calendar/**` a MCP kalendár nástroje vrať
čisté feature-disabled `404` — nikdy `500`. Trvalá histórie je ponechaný na runtime toggle-off
takže re-enabling je okamžitý.

## Rollout fázy

- **P0 — domain core** *(implementované)*: agregáty, objekty hodnôt, porty, impact model,
  krajina→symbol mapovanie, news-window politika, dvoj-tier gating, úplný jednotka balík.
- **P1 — persistence + jeden zdroj** *(implementované)*: EF `calendar` schéma (vlastné tabuľky, append-only,
  horké indexy), read-through `IEconomicCalendar` čítač s point-in-time `asOf`, idempotentný
  append-only napísať služba, FRED konektor za resilient typovaný klient a config-gated
  ingestion worker; Testcontainers integrácia testy (persistence, PIT, idempotency, blackout).
- **P2 — verejný JWT REST API + Web UI** *(implementované)*: verzovaný, JWT-zabezpečené `/api/calendar/v1`
  API — klient emisie, token výmena a jadro čítať koncové body (udalosti, histórie, série,
  prekvapenia, ďalšie, blackout, postihnutý-symboly, zdravie) s rozsah presadenie a dvoj-tier gating,
  integrácia-testované. Plus mobile-first **`/economic-calendar` stránka** — a gated, plne-localized
  (23 jazykov) program nadchádzajúcich uvoľnení ako telefón-friendly karty s farba-pásmo impact čipy
  a MudBlazor **filter dialóg** (meny + minimum impact + **From-date** výber skočiť na
  **žiadny** minulý dátum naprieč úplný históriu — žiadny 60-day cap, žiadna stena); nav položka, smoke/mobile/a11y/E2E
  testované. A **per-indicator série história stránka** (`/economic-calendar/series/{code}`, linkovať z každý
  udalosť) zoznam série úplný tisk históriu. Prekvapenie grafy + nekonečný-scroll prehliadač nasledovať.
- **P3 — viac zdrojů & warm-up** *(spustené)*: a **core-series katalóg** (CPI, Core CPI, NFP,
  nezamestnanosť, GDP, PCE, Fed fondy, maloobchod predaje → ich FRED ids) je osemený automaticky na spustenie,
  a one-time, idempotentný, rok-chunked **proaktívny backfill** ťahá ich ≥10-roky históriu takže
  obvyklý prípad je teplý bez čakania na používateľa miss. **Ingestion je štandardne zapnutý**
  (`App:Calendar:IngestionEnabled`, štandardne `true`): **centrálna-bank plán zdroj** potreby **žádny API
  kľúč**, takže FOMC / ECB / BoE rozhodnutie kalendár populuje z krabice — backfill osemení ty
  stretnutie dátumy naprieč **obe nedávne históriu a vpred horizont**, takže prehliadajúce *minulý mesiac* (alebo žiadny
  minulý okno) ukazuje stretnutia ešte pred žiadny FRED/BLS kľúč je nakonfigurovaný; hodnota série vyplniť
  raz ich kľúče sú nastaví. Pracovníci honor kalendár dvoj-tier brána — a white-label nasadení alebo
  vlastník zákaz ekonomické-kalendár funkcia zastavuje ingestion a `App:Calendar:IngestionEnabled=false`
  jej vypne explicitne. **Per-source freshness** je teraz skutočný tiež: pracovník záznamy každého zdroja poslední
  úspešné anketa, po sebe idúce-zlyhanie počet a a tripped-circuit vlajka (trvalé v app nastavenia,
  cross-process), a `/health` koncový bod + `calendar_health` MCP nástroj hlásenie a pravdu `stale`
  verdikt per zdroj. **BLS** (a 2. hodnota zdroj) a **centrálna-bank plán zdroj** (FOMC / ECB /
  BoE rozhodnutie dátumy, backfilled naprieč históriu a synced vpred do horizont okno podľa pracovníka)
  jsou in. Stále na príde: BEA/Census/ECB-SDW/Eurostat/OECD hodnota zdroje a rekoncilácia prejsť.
- **P4 — hlboké integrácia**: **MCP nástroje** *(implementované — úplný čítať-API parity: `calendar_events`,
  `calendar_event`, `calendar_history`, `calendar_series`, `calendar_surprises`, `calendar_next`,
  `calendar_blackout`, `calendar_affected_symbols`, `calendar_health`, gated na funkcie)* a
  **upozornenia `EconomicEvent` trigger** *(implementované — a `AlertRule`, ktorý oheň N minúty dopredu nadchádzajúcej
  uvoľnenie na/nad vybratý impact, voliteľne zúžené na meny; vyhodnocuje existujúci alert pracovníka bez AI,
  de-duplicated za uvoľnenie; vytvorené cez `POST /api/alerts/rules/economic-event`)*. Prop-guard news-blackout brána
  **a copy-trade blackout pause** sú in (§5.1 — a opt-in `App:Copy:NewsPauseEnabled`, štandardne vypnutý: a zdroj
  otvorené, ktorého symbol sedí v Critical-impact blackout je preskočené, byte-identical horké cesta keď vypnutý). A
  **backtest event overlay** je in — `GET /api/calendar/v1/for-symbol` a
  `calendar_events_for_symbol` MCP nástroj vrätia point-in-time-správne udalosti postihnutý symbol v
  okne a **inštancia/backtest zpráva stránka** vykresluje high-impact uvoľnenia, ktoré padli vnútri
  backtest okno pod equity krivka (takže autor vidi, ktorí obchody pristál na NFP), gated a
  localized. Celý plán je teraz implementované.
- **P5 — extras**: prekvapenie analytika, iCal/CSV export, keyword vyhľadávanie, pluggable konsenzus.

Vidieť [cBot & REST API referencia](calendar-cbot-api.md) na integrácia povrch.
