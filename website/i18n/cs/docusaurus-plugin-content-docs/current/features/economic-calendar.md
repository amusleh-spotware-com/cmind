# Ekonomický kalendář

cMind dodává svůj **vlastní** ekonomický kalendář — plán vydání, skutečnosti, prognózy, revize a datově řízený model dopadu — pocházející z **primárních autorit** (centrální banky a národních statistických agentur), s **nulovou závislostí** na ForexFactory, FXStreet, Investing.com nebo jakémkoliv agregátoru. Je point-in-time správný, udržuje ≥10 let historie, a je zapojen do obchodování, veřejného API, MCP, cBotů, AI, upozornění a backtestů. Je to odpojenýmodul: lze jej zakázat bez jakéhokoliv vlivu na obchodní jádro.

> **Stav.** Doménové jádro (model dopadu, mapování country→symbol, zásada news-window, point-in-time revision chains, dvoustupňové gating) **a** perzistence (schéma `calendar` Postgres, append-only read/write strana, FRED konektor a config-gated ingestion worker) jsou implementovány a testovány (unit + Testcontainers integrace). JWT REST API, MCP nástroje a UI přicházejí v následujících fázích rollout popsaných níže.

## Co to dělá jiným

Opakující se stížnosti na vedoucí kalendáře se staly našimi pravidly návrhu:

- **Žádné tiché změny impact-ratingu.** Náš impact rating je **deterministický, verzovaný a auditovatelný**. Každá změna je zaznamenaná revize s časovým razítkem — nikdy tiché přepsání. Uživatel vidí přesně *proč* je událost High.
- **Jeden UTC anchor na event.** Každá událost je ukotvena do jediného UTC okamžiku z oficiálního plánu primárního zdroje; vlastní časové pásmo zdroje je uloženo, a per-user rendering používá explicitní IANA timezone s DST zpracovaným databází zóny — nikdy ruční ±1h toggle.
- **Kompletní revision chains, všude.** Původní hodnota a každá revize jsou first-class, exponovány identicky přes API, MCP a cBot povrchy.
- **≥10 let historie, bez zdi.** Neomezené procházení rozsahu; žádný 60-denní cap, žádná registrační brána.
- **Point-in-time podle konstrukce.** Každý fakt nese `KnownAt` (kdy jsme se *dozvěděli*) a `EffectiveAt` (event okamžik). "Jak kalendář vypadal v čase T" je first-class query, takže backtestované pravidlo news se chová přesně jako live — žádný look-ahead z použití revidovaných hodnot v historii.

## Model dopadu

Impact skóre je čistá, deterministická funkce v `[0, 100]`, seskupená do Low / Medium / High / Critical. Její vstupy jsou pouze data známá v čase skórování (žádný budoucí únik):

- **Series prior** — baseline váha na třídu indikátoru (rozhodnutí sazby překonává CPI, která překonává menší průzkum).
- **Realizovaná-volatilita footprint** — medián absolutního návratu primárně ovlivněných symbolů v okně po *minulé* vydání této série: "toto vydání historicky pohybuje cenou tolik."
- **Surprise sensitivity** — jak silně absolutní überraschung (z-skóre) historicky koreluje s post-release pohybem.

Skóre smísí tato s pevnými váhami a razítky `ImpactModelVersion`. Recompute je explicitní, zaznamenávaná operace, která vytváří **novou revizi** — nikdy mutaci — takže skóre je vždy reprodukovatelné z jeho vstupů.

## Mapování Country → Currency → Symbol

Nejčastěji citovaný papercut algo integrace je vyřešen jednou, jako čistá funkce: země se mapuje na její měnu (každý člen eurozóny se fanuje do EUR), a měna se mapuje na watchlist symboly ji citující na obou stranách. Takže **EURUSD je ovlivňen jak EU tak US events**; XAUUSD je USD-exponován; US500 se mapuje na USD. To řídí news filtr, řešení ovlivněných symbolů a blackout matematiku.

## Zásada News-Window

`NewsWindowRule` je `{ minImpact, beforeMinutes, afterMinutes, currencies?, series? }`. Jediná, sdílená, čistá implementace odpovídá "je okamžik T uvnitř blackoutu pro symbol S?" — používáno v news filtru cBotu, copy-trade pauze a AI risk guardu, takže nikdy nemohou divergovat. Při nejistotě se blackout odpověď vrací k konfigurované konzervativní hodnotě (fail-closed výchozí) takže mezera dat nikdy tiše zelenolisuje obchodování přes high-impact vydání.

## Point-in-time & Revize

Skutečnosti, prognózy a impact skóre jsou **append-only**. Každá událost vlastní řetězec revidovaný monotonní v `KnownAt`:

- `Scheduled` — událost byla nejdřív naplánována (prior impact, bez skutečnosti).
- `Released` — první tištěná skutečnost přišla.
- `Revised` — později revidovaná hodnota přišla.
- `Rescheduled` — zdroj posunul release okamžik (auditovatelný, alertovatelný).
- `Rescored` — impact skóre bylo přepočítáno pod novou verzí modelu.

Dotazování `as of` minulého okamžiku vrátí přesně revizi známou pak — záruka, která zabíjí look-ahead v backtestovaných pravidlech news.

## Prognóza / Konsenzus

Průměr průzkumu ekonomů není volně zveřejňován primárními zdroji — je to agregátorů proprietární value-add, a my jej nefabriku... Schéma event nese nullable `Forecast`; nasazení může zapojit licencovaný konsenzus feed přes volitelný `IForecastProvider` port (bring-your-own key, výchozí off). Předchozí hodnoty a revize vždy pocházejí z oficiálního zdroje.

## Zdroje dat

Dva oddělené vrstvy, všechny primární — nikdy agregátor:

- **Plán / načasování:** FRED release kalendář; národní statistické agentury (BLS, BEA, Census, Eurostat, ONS, Destatis, INSEE, e-Stat, ABS, StatCan); kalendáře centrálních bank (Fed, ECB, BoE, BoJ, RBA, BoC, SNB, RBNZ).
- **Skutečné hodnoty:** FRED (s vintage data pro revize a point-in-time), plus BLS, BEA, Census, ECB SDW, Eurostat a OECD SDMX APIs.

Mrtvý zdroj degraduje pokrytí **pouze pro tento zdroj**; kalendář pokračuje v servování všeho ostatního a povrchu mezery jako metrika čerstvosti.

## Omezení rychlosti & plán záloh

Externí poskytovatelé zveřejňují omezení sazeb (FRED umožňuje ~120 žádostí/minutu). Kalendář je postaven tak, aby **nikdy nespustil omezení poskytovatele**, a tak, aby být drosslen nebo vypnut nikdy nedegraduje čtení:

- **Proaktivní throttling.** Klient HTTP každého zdroje prochází sdílenou, thread-safe rate gate, která prostoruje odchozí požadavky na konfigurovaný rozpočet (`App:Calendar:FredRequestsPerMinute`, výchozí 100 — záměrně pod ceiling poskytovatele). Žádosti jsou zařazeny do fronty a tempovány, nikdy nespouštěny v shluku.
- **Cti `429 Retry-After`.** Pokud poskytovatel někdy vrátí `429 Too Many Requests`, brána zase celý zdroj off podle server-requested cooldown (nebo `App:Calendar:RateLimitBackoff`, výchozí 60s) před další voláním — bez těsné retry loop.
- **Standardní resilience.** Klient každého zdroje také zdědí app-wide resilience handler (retry s backoff + jitter, circuit breaker, timeouts), takže přechodné háčky jsou absorbovány a persistentně selhávající zdroj je parkován (jeho pokrytí se stává zastaralým) bez ovlivnění ostatních.
- **Plán záloh — durable read-through cache.** Čtení **nikdy** nejsou podávaná voláním poskytovatele. Jakmile se rozsah načte je persistován append-only do Postgres a podáván odtud navždy (viz §"On-demand load"). Takže i když je zdroj rate-limited nebo down, kalendář pokračuje v odpovídání z cached, point-in-time-correct dat; chybějící rozpětí jednoduše zůstává nepokryté a je znovu vyzkoušeno v dalším ingestion cyklu. Blackout odpovědi navíc selžou konzervativní default pod nejistotou, takže mezera dat nikdy zelenolisuje obchodování přes vydání.
- **Levné polling.** Podmíněné načtení (ETag / If-Modified-Since / source vintage cursory) a "načtení rozpětí jednou, nikdy znovu" cache udržuje skutečný objem žádostí daleko pod jakýmkoliv limitem v normálním provozu — rate gate je bezpečnostní síť, ne běžná cesta.

## Povolení / Zákaz

Dvě nezávislé úrovně, přesně jako ostatní cMind vlastnosti:

- **Úroveň 1 — runtime feature toggle** (`Feature.EconomicCalendar`) převrácený z Features admin UI; žádný redeploy, vstupuje v platnost live.
- **Úroveň 2 — white-label hard gate** (`App:Branding:EnableEconomicCalendar`, výchozí `true`). Prodejce jej nastaví `false` aby zcela odstranil vlastnost; operátor jej pak nemůže znovu povolit.

Efektivní stav je `Branding.EnableEconomicCalendar && FeatureToggle.EconomicCalendar`. Když je zakázán, nav entry je skryta a `/economic-calendar`, `/api/calendar/**` a MCP calendar nástroje vrací čistý feature-disabled `404` — nikdy `500`. Persistovaná historie je zachována na runtime toggle-off takže re-enabling je okamžité.

## Fáze rollout

- **P0 — doménové jádro** *(implementováno)*: agregáty, value objekty, porty, model dopadu, mapování country→symbol, zásada news-window, dvoustupňové gating, plná unit sada.
- **P1 — perzistence + jeden zdroj** *(implementováno)*: EF `calendar` schéma (vlastní tabulky, append-only, hot indexy), reader `IEconomicCalendar` read-through s point-in-time `asOf`, idempotentní append-only write service, FRED konektor za resilientním typovaným klientem, a config-gated ingestion worker; Testcontainers integrace testy (perzistence, PIT, idempotence, blackout).
- **P2 — veřejný JWT REST API + Web UI** *(implementováno)*: verzovaný, JWT-zabezpečený `/api/calendar/v1` API — client issuance, token exchange, a core read endpointy (events, history, series, surprises, next, blackout, affected-symbols, health) s scope enforcement a dvoustupňovým gating, integration-tested. Navíc mobile-first **`/economic-calendar` stránka** — gated, plně-lokalizovaná (23 jazyků) agenda nadcházejících vydání jako phone-friendly karty s barevně-seskupem impact chipy a MudBlazor **filter dialog** (měny + minimum impact + **From-date** picker k skoči na **jakýkoliv** minulý datum přes plnou historii — bez 60-denního capu, bez zdi); nav entry, smoke/mobile/a11y/E2E testováno. **Per-indicator series history stránka** (`/economic-calendar/series/{code}`, linked z každé event) vypíše kompletní print historii série. Surprise grafy + infinite-scroll prohlížeč následují.
- **P3 — více zdrojů & warm-up** *(spuštěno)*: **core-series katalog** (CPI, Core CPI, NFP, unemployment, GDP, PCE, Fed funds, retail sales → jejich FRED ids) je seedován automaticky na startup, a one-time, idempotentní, year-chunked **proaktivní backfill** tahá jejich ≥10-year historii takže běžný případ je teplý bez čekání na user miss. **Ingestion je výchozím** (`App:Calendar:IngestionEnabled`, výchozí `true`): **central-bank schedule zdroj** potřebuje **bez API klíče**, takže FOMC / ECB / BoE decision kalendář se naplní z krabice — backfill seeduje ta data setkání přes **jak nedávnout historii tak i budoucí horizont**, takže procházení *minulého měsíce* (nebo jakékoliv minulého okna) zobrazí setkání i předtím jakýkoliv FRED/BLS klíč je konfigurován; value série se zaplní jakmile jsou jejich klíče nastaveny. Workeři respektují two-tier gate kalendáře — white-label nasazení nebo vlastník zakázávající ekonomický-kalendář vlastnost zastavuje ingestion, a `App:Calendar:IngestionEnabled=false` ji vypíná explicitně. **Per-source čerstvost** je teď skutečná: worker zaznamenává každého zdroje poslední úspěšný poll, consecutive-failure počet a tripped-circuit flag (persistován v app nastavení, cross-process), a `/health` endpoint + `calendar_health` MCP nástroj hlásí pravdivý `stale` verdikt per zdroj. **BLS** (2. value zdroj) a **central-bank schedule zdroj** (FOMC / ECB / BoE decision data, backfilled přes historii a synced forward do horizon okna workerm) jsou in. Stále by mělo přijít: BEA/Census/ECB-SDW/Eurostat/OECD value zdroje a reconciliation průchod.
- **P4 — hluboká integrace**: **MCP nástroje** *(implementovány — plná read-API parita: `calendar_events`, `calendar_event`, `calendar_history`, `calendar_series`, `calendar_surprises`, `calendar_next`, `calendar_blackout`, `calendar_affected_symbols`, `calendar_health`, gated na vlastnost)* a **alerts `EconomicEvent` trigger** *(implementovány — `AlertRule`, která se spustí N minut před nadcházejícím vydáním na/výše zvolené impact, volitelně zúženo na měny; vyhodnoceno existujícím alert workerem bez AI, de-duplicated per vydání; vytvořeno přes `POST /api/alerts/rules/economic-event`)*. Prop-guard news-blackout brána **a copy-trade blackout pauza** jsou in (§5.1 — opt-in `App:Copy:NewsPauseEnabled`, výchozí off: nový zdroj otevření jehož symbol sedí v Critical-impact blackoutu je přeskočen, byte-identical hot cesta když je off). **Backtest event overlay** je in — `GET /api/calendar/v1/for-symbol` a `calendar_events_for_symbol` MCP nástroj vrátí point-in-time-correct events ovlivňující symbol v okně, a **instance/backtest report stránka** renders high-impact vydání, která padla uvnitř backtest okna pod equity křivku (takže autor vidí které obchody přistály na NFP), gated a lokalizovány. Celý plán je nyní implementován.
- **P5 — extras**: surprise analýzy, iCal/CSV export, keyword search, pluggable konsenzus.

Viz [cBot & REST API reference](calendar-cbot-api.md) pro integrace povrch.
