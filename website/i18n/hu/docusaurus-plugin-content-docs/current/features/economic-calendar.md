# Gazdasági naptár

A cMind a saját **gazdasági naptárát** szállítja — közzétételi ütemterv, tényadatok, előrejelzések, revíziók és egy adatvezérelt hatásmodell — **elsődleges hatóságokból** (központi bankok és nemzeti statisztikai ügynökségek) forrásolva, **nulla függőséggel** a ForexFactory, FXStreet, Investing.com vagy bármely aggregator felé. Pont-időben helyes, ≥10 év történetet tart, és csatlakoztatva van a kereskedéshez, a nyilvános API-hoz, MCP-hez, cBot-okhoz, AI-hoz, riasztásokhoz és backtesztekhez. Decentralizált modul: letiltható nulla hatással a kereskedési magra.

> **Állapot.** P0–P4 implementálva és szállítva. A domain mag, a perzisztencia (EF `calendar` séma,
> append-only olvasás/írás, FRED + BLS + központi bank-menetrend források, config-kapuzott ingestion
> worker forrás szerinti frissesség-követéssel), a verziózott JWT REST API, a mobilközpontú
> `/economic-calendar` UI, az MCP eszközök, a cBot JWT API, a nagy hatású esemény riasztások,
> a copy-trade news-blackout szünet, a backteszt esemény overlay, az SSE stream, a HMAC-aláírt
> webhookok és a típusos `CmindCalendarClient` mind implementálva és integrációs tesztekkel ellenőrizve.
> A P5 extrák (meglepetés-elemzés, iCal/CSV export, kulcsszavas keresés, pluggable konszenzus) a
> maradék elemek — lásd az alábbi rollout fázisokat.

## Mi különbözteti meg

A vezető naptárak elleni visszatérő panaszok lettek a tervezési kényszereink:

- **Nincs csendes hatás-értékelés változás.** A mi hatás értékelésünk **determinisztikus, verziózott és auditálható**. Minden változtatás egy rögzített revízió, timestamp-pel — soha nem csendes felülírás. A felhasználó pontosan láthatja, *miért* High egy esemény.
- **Egy UTC anchor per esemény.** Minden esemény egyetlen UTC pillanathoz van kötve az elsődleges forrás hivatalos ütemtervéből; a forrás saját időzónája tárolva van, és a per-felhasználó renderelés egy explicit IANA időzónát használ DST-vel a zone adatbázis által kezelt — soha nem manuális ±1h toggle.
- **Teljes revíziós láncok, mindenhol.** Az eredeti érték és minden revízió első osztályú, az API-n, MCP-n és cBot felületeken azonosan kitett.
- **≥10 év történet, nincs fal.** Korlátozás nélküli böngészési tartomány; nincs 60 napos sapka, nincs regisztrációs kapu.
- **Point-in-time építés szerint.** Minden tény `KnownAt`-ot hordoz (amikor *mi* megtudtuk) és `EffectiveAt`-ot (az esemény pillanata). "Ahogy a naptár T időpontban nézett ki" egy első osztályú lekérdezés, így egy backtesztelt news szabály pontosan úgy viselkedik, mint élőben — nincs look-ahead a revizált értékek használatával a történetben.

## A hatásmodell

A hatás pontszám egy tiszta, determinisztikus függvény `[0, 100]` intervallumban, Low / Medium / High / Critical sávokra osztva. A bemenetei csak a pontozási időpontban ismert adatok (nincs jövő szivárgás):

- **Sorrend prior** — egy baseline súly per indikátor osztály (a kamat döntés felülmúlja a CPI-t, ami felülmúlja egy kisebb felmérést).
- **Realizált-volatility footprint** — az elsődleges érintett szimbólumok median abszolút hozama az ablakban az adott sorozat *múltbeli* közzétételei után: "ez a közzététel történetesen eddig mozgatta az árat."
- **Meglepetés érzékenység** — mennyire erősen korrelált historikusan az abszolút meglepetés (egy z-score) az utána következő mozgással.

A pontszám ezeket fix súlyokkal keveri és bélyegzi egy `ImpactModelVersion`-nel. Az újraszámítás egy explicit, naplózott művelet, amely egy **új revíziót** termel — soha nem mutációt — így a pontszám mindig reprodukálható a bemeneteiből.

## Country → currency → symbol mapping

A leggyakrabban idézett algo integrációs papírvágás megoldva egyszer, tiszta függvényként: egy country mapolja a saját devizáját (minden euro-területi tag belefolyik az EUR-ba), és egy deviza mapolja a jegyzési szimbólumokat, amelyek egyik lábon vagy másikon idézik. Tehát **az EURUSD-t az EU és US események egyaránt érintik**; az XAUUSD USD-kitettségű; az US500 USD-ra mapol. Ez vezeti a news szűrőt, az érintett-szimbólumok feloldását és a blackout matekot.

## News-window politika

Egy `NewsWindowRule` = `{ minImpact, beforeMinutes, afterMinutes, currencies?, series? }`. Egyetlen, megosztott, tiszta implementáció válaszol "az instant T bent van-e egy S szimbólum blackout-jában?" — használva a cBot news szűrő, a copy-trade pause és az AI risk guard által, így soha nem divergálhatnak. Bizonytalanság esetén a blackout válasz a konfigurált konzervatív értékre áll (default fail-closed), így egy adat rés nem csendben zöldlámpáz a kereskedésnek egy magas hatású közzétételen.

## Point-in-time & revíziók

A tényadatok, előrejelzések és hatás pontszámok **append-only-k**. Minden esemény birtokol egy rendezett revíziós láncot, monoton `KnownAt`-ban:

- `Scheduled` — az eseményt először ütemezték (prior hatás, nincs tény).
- `Released` — az első nyomtatott tény megérkezett.
- `Revised` — egy későbbi revideált érték megérkezett.
- `Rescheduled` — a forrás mozgatta a közzétételi pillanatot (auditálható, riasztható).
- `Rescored` — a hatás pontszám újra lett számolva egy új modell verzió alatt.

`as of` egy múltbeli pillanatot kérdezve pontosan a revíziót adja vissza, amit akkor ismertünk — ez a garancia, ami megöli a look-ahead-et a backtesztelt news szabályokban.

## Előrejelzés / konszenzus

A közgazdászok felmérési mediánja **nem** szabadon publikált az elsődleges források által — ez az aggregatorok proprietary érték-hozzáadása, és mi nem gyártjuk. Az esemény séma hordoz egy nullable `Forecast`-ot; egy telepítés egy opcionális `IForecastProvider` port-on keresztül vezethet egy licencelt konszenzus feedet (bring-your-own-key, alapértelmezés szerint ki). Az előző értékek és revíziók mindig az hivatalos forrásból jönnek.

## Adatforrások

Két decuplikált réteg, minden elsődleges — soha aggregator:

- **Ütemterv / időzítés:** FRED release calendar; nemzeti statisztikai ügynökségek (BLS, BEA, Census, Eurostat, ONS, Destatis, INSEE, e-Stat, ABS, StatCan); központi banki találkozó naptárak (Fed, ECB, BoE, BoJ, RBA, BoC, SNB, RBNZ).
- **Tényértékek:** FRED (vintage dátumokkal revíziókhoz és point-in-time-hoz), plusz BLS, BEA, Census, ECB SDW, Eurostat és OECD SDMX API-k.

Egy halott forrás csak annak a forrásnak a lefedettségét degradálja; a naptár tovább szolgál mindent mást és a rést frissességi metrikaként tünteti fel.

## Rate limiting & a tartalék terv

A külső szolgáltatók publikálják a rate limitjeiket (FRED ~120 kérés/percek enged). A naptár úgy van építve, hogy **soha nem üti meg egy szolgáltató limitjét**, és hogy a throttling vagy lekapcsolás soha ne degradálja az olvasásokat:

- **Proaktív throttling.** Minden forrás HTTP kliense egy megosztott, thread-safe rate kapun megy keresztül, amely a kimenő kéréseket egy konfigurált költségvetésre térképezi (`App:Calendar:FredRequestsPerMinute`, alapértelmezés 100 — szándékosan a szolgáltató Plafond alatt). A kérések sorba állnak és tempóznak, soha nem burst-elve.
- **`429 Retry-After` tiszteletben tartása.** Ha egy szolgáltató valaha `429 Too Many Requests`-et ad vissza, a kapu a teljes forrást a szerver által kért cooldown-ra (vagy `App:Calendar:RateLimitBackoff`, alapértelmezés 60s) visszaveszi a következő hívás előtt — nincs szoros újrapróbálkozási hurok.
- **Standard rugalmasság.** Minden forrás kliens örökli az alkalmazás-szintű rugalmassági handlert (retry backoff + jitter, circuit breaker, timeouts), így az átmeneti hibák elnyelődnek és egy tartósan sikertelen forrás parkol (a lefedettsége avult lesz) anélkül, hogy a többire hatással lenne.
- **A tartalék terv — a tartós olvasás-through cache.** Az olvasások **soha nem** a szolgáltató hívásával szolgáltatódnak. Amint egy tartomány fetch-elve van, az append-only módon perzisztálódik Postgresbe és onnan szolgáltatódik örökre (lásd §"On-demand load"). Tehát még ha egy forrás rate-limited vagy le van kapcsolva, a naptár a cached, point-in-time-correct adatokból válaszol; a hiányzó span egyszerűen fedetlen marad és a következő ingestion ciklusban újrapróbálkozik. A blackout válaszok továbbá a konzervatív default-hoz fail-colnak bizonytalanság alatt, így egy adat rés soha nem zöldlámpáz a kereskedést egy közzétételen.
- **Olcsó polling.** Conditional fetch (ETag / If-Modified-Since / source vintage cursors) és a "fetch egy span egyszer, soha többé" cache az aktuális kérés volumenét messze bármely limit alatt tartják normál működésben — a rate kapu egy biztonsági háló, nem a közös út.

## Bekapcsolás / kikapcsolás

Két független szint, pontosan mint más cMind funkciók:

- **1. szint — runtime feature toggle** (`Feature.EconomicCalendar`) a Features admin UI-ból kapcsolva; nincs újradeploy, élőben érvényesül.
- **2. szint — white-label hard gate** (`App:Branding:EnableEconomicCalendar`, alapértelmezés `true`). Egy viszonteladó `false`-ra állítja a funkció teljes eltávolításához; egy operátor aztán nem tudja újra engedélyezni.

Az effektív állapot `Branding.EnableEconomicCalendar && FeatureToggle.EconomicCalendar`. Amikor le van tiltva, a nav bejegyzés rejtett és `/economic-calendar`, `/api/calendar/**` és az MCP naptár eszközök tiszta feature-disabled `404`-et adnak vissza — soha nem `500`-t. A perzisztált történet megmarad runtime toggle-off-on, így az újra-engedélyezés instant.

## Rollout fázisok

- **P0 — domain mag** *(implementálva)*: aggregátumok, value object-ek, portok, hatásmodell, country→symbol mapping, news-window politika, két-szintű gating, teljes egység suite.
- **P1 — perzisztencia + egy forrás** *(implementálva)*: EF `calendar` séma (saját táblák, append-only, hot indexek), az olvasás-through `IEconomicCalendar` reader point-in-time `asOf`-fal, az idempotens append-only write szolgáltatás, a FRED connector egy rugalmas típusos kliens mögött, és a config-kapuzott ingestion worker; Testcontainers integrációs tesztek (perzisztencia, PIT, idempotencia, blackout).
- **P2 — nyilvános JWT REST API + Web UI** *(implementálva)*: a verziózott, JWT-biztosított `/api/calendar/v1` API — kliens kibocsátás, token csere és a core olvasási végpontok (events, history, series, surprises, next, blackout, affected-symbols, health) scope kényszerítéssel és két-szintű gating-gel, integráció-tesztelt. Plusz a mobil-először **`/economic-calendar` oldal** — egy kapuzott, teljesen lokalizált (23 nyelv) az upcoming közzétételek agendája telefonos-barát kártyákkal, szín-sávval ellátott hatás chip-ekkel és egy MudBlazor **szűrő dialog-gal** (devizák + minimális hatás + egy **From-date** választó, hogy bármely múltbeli dátumra ugorj a teljes történeten át — nincs 60 napos sapka, nincs fal); nav bejegyzés, smoke/mobile/a11y/E2E tesztelve. Egy **per-indikátor series history oldal** (`/economic-calendar/series/{code}`, minden eseményből linkelve) listáz egy series teljes print történetét. A surprise chartok + infinite-scroll böngésző következik.
- **P3 — több forrás & warm-up** *(indított)*: egy **core-series katalógus** (CPI, Core CPI, NFP, munkanélküliség, GDP, PCE, Fed funds, kiskereskedelmi értékesítés → ezek FRED azonosítói) automatikusan seed-elve van indításkor, és egy egyszeri, idempotens, év-chunkolt **proaktív backfill** lehozza a ≥10 éves történetüket, így a gyakori eset warm anélkül, hogy egy felhasználó miss-re várna. **Az ingestion alapértelmezés szerint be van kapcsolva** (`App:Calendar:IngestionEnabled`, alapértelmezés `true`): a **központi banki ütemterv forrásnak nincs szüksége API kulcsra**, így a FOMC / ECB / BoE döntési naptár kitöltődik a dobozból — a backfill ezeket a találkozói dátumokat tölti fel mind a közelmúlt történetében, mind a forward horizonton, így a *legutóbbi hónap* böngészése (vagy bármely múltbeli ablak) mutatja a találkozókat, még mielőtt bármely FRED/BLS kulcs konfigurálva lenne; az értéksorozatok kitöltődnek, amint a kulcsok be vannak állítva. A workerek tiszteletben tartják a naptár két-szintű gate-jét — egy white-label telepítés vagy a tulajdonos, aki letiltja az economic-calendar funkciót, leállítja az ingestiót, és az `App:Calendar:IngestionEnabled=false` kifejezetten kikapcsolja. **Per-forrás frissesség** is valósá vált: a worker rögzíti minden forrás utolsó sikeres poll-ját, egymást követő-hiba count-ot és egy tripped-circuit flag-et (perzisztálva az alkalmazás beállításaiban, cross-process), és a `/health` végpont + `calendar_health` MCP eszköz egy őszinte `stale` verdiktet jelent per forrás. A **BLS** (egy 2. érték forrás) és a **központi banki ütemterv forrás** (FOMC / ECB / BoE döntési dátumok, backfill-elt a történeten át és szinkronizálva forward egy horizonton ablakba a worker által) kész. Még hátravan: BEA/Census/ECB-SDW/Eurostat/OECD érték források és az egyeztetési átmenet.
- **P4 — mély integráció**: **MCP eszközök** *(implementálva — teljes olvasási-API paritás: `calendar_events`, `calendar_event`, `calendar_history`, `calendar_series`, `calendar_surprises`, `calendar_next`, `calendar_blackout`, `calendar_affected_symbols`, `calendar_health`, a funkcióra kapuzva)* és a **alerts `EconomicEvent` trigger** *(implementálva — egy `AlertRule`, amely N perccel egy upcoming közzététel előtt tüzel a választott hatáson vagy afelett, opcionálisan devizákra szűkítve; az existing alert worker által értékelve AI nélkül, deduplikálva per közzététel; létrehozva `POST /api/alerts/rules/economic-event`-en)*. A prop-guard news-blackout gate **és a copy-trade blackout pause** bent van (§5.1 — egy opcionális `App:Copy:NewsPauseEnabled`, alapértelmezés ki: egy forrás nyitás, amelynek szimbóluma Critical-hatás blackout-ban van, ki van hagyva, byte-azonos hot path ki kapcsolva). A **backtest event overlay** bent van — `GET /api/calendar/v1/for-symbol` és a `calendar_events_for_symbol` MCP eszköz visszaadják a point-in-time-correct eseményeket, amelyek egy szimbólumot érintenek egy ablakban, és az **instance/backtest report oldal** rendereli a magas hatású közzétételeket, amelyek a backtest ablakon belül estek az equity görbe alatt (így egy szerző látja, mely kereskedések landoltak NFP-n), kapuzva és lokalizálva. A teljes terv most implementálva van.
- **P5 — extrák**: surprise analytics, iCal/CSV export, kulcsszó keresés, pluggable konszenzus.

Lásd a [cBot & REST API referenciát](calendar-cbot-api.md) az integrációs felületért.

## Adatforrás szükséges (a funkció rejtett nélküle)

A naptár a tény/előrejelzés/előző értékeket csak egy konfigurált érték forrásból mutatja (FRED vagy BLS). `App:Calendar:FredApiKey` vagy `App:Calendar:BlsApiKey` nélkül a funkció **rejtett** a navigációból; ha erőltetve van engedélyezve (white-label/tulajdonos) kulcs nélkül, az oldal egy cselekvőképes "konfigurálj egy adatforrást" üzenetet mutat az üres értékek helyett, és a szűrő akció rejtett marad, amíg egy forrás be nem állítva. Az esemény sorok a series **nevét** mutatják (a katalógusból), nem a nyers series kódot.
