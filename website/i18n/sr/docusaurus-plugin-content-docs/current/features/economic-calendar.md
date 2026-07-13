# Ekonomski kalendar

cMind испоручуje **сопствени** економски календар — распоред објава, актуелне вредности, прогнозе, ревизиjе и a
data-driven модел утицаја — прибављен из **примарних извора** (централне банке и националне
статистичке агенције), са **нулном зависношћу** од ForexFactory, FXStreet, Investing.com или било ког
агрегатора. Тачан je у времену, чува ≥10 година историjе, и повезан je у трговање, јавни API, MCP, cBots, AI, упозорења и
backtest-ове. То je декпловани модул: може се онемогућити без утицаја на трговачко језгро.

> **Статус.** Domain језгро (модел утицаја, мапирање земља→симбол, политика news-window-а, point-in-time
> ланци ревизија, two-tier контрола) **и** перзистенција (PostgreSQL `calendar` шема, append-only
> read/write страна, FRED конектор и config-gated ingestion worker) су имплементирани и тестирани
> (unit + Testcontainers integration). JWT REST API, MCP алати и UI слећу у наредним
> rollout фазама описним испод.

## Шта га чини другачијим

Поновљене жалбе на водеће календаре постале су наша конструкцијска ограничења:

- **Без тихих промена rating-а утицаја.** Наш rating утицаја je **детерминистички, верзиониран и
  аудитабилан**. Свака промена je забележена ревизија са временском ознаком — никада тихо преписивање. Корисник
  може тачно да види *зашто* je догађај High.
- **Једна UTC референца по догађају.** Сваки догађај je усидрен на један UTC тренутак из званичног распореда
  примарног извора; сопствена временска зона извора се чува, и per-user рендеровање користи експлицитну IANA временску зону са
  DST рукованим од стране zone базе података — никада ручни ±1h прекидач.
- **Потпуни ланци ревизија, свуда.** Оригинална вредност и свака ревизија су првокласни, изложени
  идентично кроз API, MCP и cBot површине.
- **≥10 година историjе, без зида.** Неограничен опсег прегледања; нема 60-дневног cap-а, нема registration gate-а.
- **Point-in-time по конструкцији.** Свака чињеница носи `KnownAt` (када *смо* сазнали) и
  `EffectiveAt` (тренутак догађаја). "Као што je календар изгледао у тренутку T" je првокласни упит, тако да a
  backtest-овано правило вести понаша се тачно као живо — без look-ahead-а од коришћења ревидираних вредности у историjи.

## Модел утицаја

Score утицаја je чиста, детерминистичка функција у `[0, 100]`, груписана у Low / Medium / High /
Critical. Њени инпути су само подаци познати у тренутку scoring-а (без future leak-а):

- **Series prior** — основна тежина по класи индикатора (одлука о стопи превазилази CPI, која
  превазилази малу анкету).
- **Realized-volatility footprint** — медијана апсолутног приноса примарно погођених симбола у
  прозору након *прошлих* објава серија: "ова објава историјски помера цену оволико."
- **Surprise sensitivity** — колико снажно je апсолутно изненађење (z-score) историјски
  корелирало са post-release померајем.

Score их меша са фиксним тежинама и жигоса `ImpactModelVersion`. Рекомпутовање je
експлицитна, логована операција која производи **нову ревизију** — никада мутација — тако да score увек
остаје репродуктибилан из својих инпута.

## Мапирање Земља → валута → симбол

Најчешће цитирани algo integration papercut je решен једном, као чиста функција: земља се мапира на
своју валуту (сваки euro-area члан се fans in у EUR), и валута се мапира на watchlist симболе
котиране на било којој нози. Тако да **EURUSD je погођен и EU и US догађајима**; XAUUSD je USD-изложен;
US500 се мапира на USD. Ово покреће news filter, резолуцију погођених симбола и blackout математику.

## Политика News-window-а

`NewsWindowRule` je `{ minImpact, beforeMinutes, afterMinutes, currencies?, series? }`. Једна,
дељена, чиста имплементација одговара "je ли тренутак T унутар блокирања за симбол S?" — користи се од стране cBot
news filter-а, copy-trade паузе и AI risk guard-а, тако да никада не могу да разиђу. При неизвесности,
blackout одговор подразумева конфигурисана конзервативна вредност (fail-closed по подразумеваном) тако да празнина података
никада не омогућава трговање кроз високо-утицајну објаву.

## Point-in-time и ревизије

Actuals, forecasts и scores утицаја су **append-only**. Сваки догађај има уређени ланац
ревизија, монотоних по `KnownAt`:

- `Scheduled` — догађај je први пут заказан (претходни утицај, без actual).
- `Released` — стигла прва одштампана actual вредност.
- `Revised` — стигла каснија ревидирана вредност.
- `Rescheduled` — извор померио тренутак објаве (аудитабилан, упозорив).
- `Rescored` — score утицаја je рекомпутован под новом верзијом модела.

Упит `as of` прошли тренутак враћа тачно ревизију познату тада — гаранција која убија
look-ahead у backtest-ованим правилима вести.

## Прогноза / консензус

Медијана анкете економиста **није** слободно објављена од стране примарних извора — то je
proprietary вредност агрегатора, и ми je нећемо фабриковати. Schema догађаја носи nullable
`Forecast`; deployment може повезати лиценцирани consensus feed кроз опциони `IForecastProvider`
port (bring-your-own key, искључен по подразумеваном). Претходне вредности и ревизије увек долазе од званичног
извора.

## Извори података

Два декплована слоја, сви примарни — никада агрегатор:

- **Распоред / временiranje:** FRED release календар; националне статистичке агенције (BLS, BEA, Census,
  Eurostat, ONS, Destatis, INSEE, e-Stat, ABS, StatCan); календари централних банака (Fed, ECB,
  BoE, BoJ, RBA, BoC, SNB, RBNZ).
- **Стварне вредности:** FRED (са vintage датумима за ревизије и point-in-time), плус BLS, BEA, Census,
  ECB SDW, Eurostat и OECD SDMX API-ови.

Мртав извор деградира покривеност **само за тај извор**; календар наставља да сервира све остало
и површину празнине kao freshness metric.

## Rate limiting и план резервне копије

Екстерни провајдери објављују rate limits-ове (FRED дозвољава ~120 захтева/minute). Календар je изграђен тако да
**никада не активира limit провајдера**, и тако да будући throttle-ован или одсечен никада не деградира читања:

- **Проактивно throttle-овање.** Сваки HTTP клијент извора пролази кроз дељену, thread-safe rate gate
  која распоређује outbound захтеве према конфигурисаном буџету (`App:Calendar:FredRequestsPerMinute`, подразумевано
  100 — намерно испод плафон провајдера). Захтеви се стављају у ред и pace-ују, никада не burst-ују.
- **Поштуј `429 Retry-After`.** Ако провајдер икада врати `429 Too Many Requests`, gate одмара
  цео извор за server-requested cooldown (или `App:Calendar:RateLimitBackoff`, подразумевано 60s)
  пре следећег позива — нема уског retry loop-а.
- **Стандардна отпорност.** Сваки source клијент такође наслеђује app-wide resilience handler (retry са
  backoff + jitter, circuit breaker, timeout-ови), тако да се транзијентни blip-ови апсорбују и перзистентно
  неуспешан извор се паркира (његова покривеност постаје stale) без утицаја на остале.
- **План резервне копије — траjни read-through cache.** Читања се **никада** не сервирају позивањем
  провајдера. Једном када се опсег добави, перзистује се append-only у Postgres и сервира одатле
  заувек (види §"On-demand load"). Тако да чак и када је извор rate-limited или пао, календар
  наставља да одговара из кешираних, point-in-time-коректних података; недостајући span једноставно остаје непокривен и
  ретрија се у следећем ingestion циклусу. Blackout одговорима се додатно fail-ује на конзервативни
  подразумевани при неизвесности, тако да празнина података никада не омогућава трговање кроз објаву.
- **Ефтино poll-овање.** Conditional fetch (ETag / If-Modified-Since / source vintage cursor-ови) и
  "fetch a span once, never again" cache држе стварни волумен захтева далеко испод било ког лимита у нормалној
  операцији — rate gate je safety net, не заједничка путања.

## Омогући / онемогући

Два независна tier-а, тачно kao друге cMind функције:

- **Tier 1 — runtime feature toggle** (`Feature.EconomicCalendar`) преврнут из Features admin UI;
  без redeploy-а, ступа на снагу live.
- **Tier 2 — white-label хард gate** (`App:Branding:EnableEconomicCalendar`, подразумевано `true`). А
  reseller постави `false` да уклони функцију потпуно; оператер тада не може да je поново омогући.

Ефективно стање je `Branding.EnableEconomicCalendar && FeatureToggle.EconomicCalendar`. Када je онемогућено,
nav entry се сакрива и `/economic-calendar`, `/api/calendar/**` и MCP calendar алати враћају
чист feature-disabled `404` — никада `500`. Перзистована историja се задржава при runtime toggle-off
тако да је реомогућавање тренутно.

## Rollout фазе

- **P0 — domain jeзгро** *(имплементирано)*: aggregates, value objects, ports, модел утицаја,
  мапирање земља→симбол, политика news-window-а, two-tier контрола, пуни unit suite.
- **P1 — перзистенција + један извор** *(имплементирано)*: EF `calendar` шема (сопствене табеле, append-only,
  hot indexes), read-through `IEconomicCalendar` reader ca point-in-time `asOf`, идемпотентни
  append-only write сервис, FRED конектор иза отпорног типизираног клијента, и config-gated
  ingestion worker; Testcontainers integration тестови (перзистенција, PIT, идемпотенција, blackout).
- **P2 — јавни JWT REST API + Web UI** *(имплементирано)*: верзионирани, JWT-обезбеђени `/api/calendar/v1`
  API — издавање клијента, размена токена, и основни read ендпоинти (events, history, series,
  surprises, next, blackout, affected-symbols, health) ca scope enforcement и two-tier контролом,
  integration-tested. Плус mobile-first **`/economic-calendar` страница** — gated, потпуно локализован
  (23 jeзика) agenda предстојећих објава kao phone-friendly картице ca colour-banded impact chips
  и MudBlazor **filter дијалог** (валуте + минимални утицај + **From-date** бирач да прескочи на
  **било koji** прошли датум преко пуне историjе — нема 60-дневног cap-а, нема зида); nav entry, smoke/mobile/a11y/E2E
  тестирано. **Per-indicator series history страница** (`/economic-calendar/series/{code}`, повезана ca сваког
  догађаја) наводи пуну историjу штампања серија. Surprise графикони + infinite-scroll browser следе.
- **P3 — више извора и загревање** *(започето)*: **core-series каталог** (CPI, Core CPI, NFP,
  незапосленост, GDP, PCE, Fed funds, малопродаja → њихови FRED id-ови) се семљи аутоматски при покретању,
  и једнократно, идемпотентно, year-chunked **проактивно попуњавање** повлачи њихову ≥10-годишњу историju тако да
  уобичајен случај je загреан без чекања да корисник пропусти. **Ingestion je укључен по подразумевању**
  (`App:Calendar:IngestionEnabled`, подразумевано `true`): **central-bank schedule извор** треба **без API
  кључа**, тако да се FOMC / ECB / BoE календар одлука попуњава out of the box — попуњавање семљи те
  састанак датуме преко **и недавне историjе и будућег хоризонта**, тако да прегледање *прошлог месеца* (или било ког
  прошлог прозора) показује састанке чак и пре него што je било koji FRED/BLS кључ конфигурисан; серија вредности се попуњава
  једном када се њихови кључevi поставе. Workers частвају two-tier gate календара — white-label deployment или
  власник koji онемогућава economic-calendar функцију зауставља ingestion, и `App:Calendar:IngestionEnabled=false`
  га експлицитно искључуje. **Per-source freshness** je сада реално: worker бележи последње успешно poll-овање сваког извора,
  број узастопних неуспеха и tripped-circuit заставица (перзистовано у app settings, cross-process), и
  `/health` ендпоинт + `calendar_health` MCP алат пријављују истинит `stale` verdict по извору. **BLS**
  (2nd извор вредности) и **central-bank schedule извор** (FOMC / ECB / BoE датуми одлука, попуњени преко историjе и
  синхронизовани унапред у хоризонт window од стране worker-а) су у. Још увек предстоji: BEA/Census/ECB-SDW/Eurostat/OECD извори вредности и
  реконцилиова pass.
- **P4 — дубoka интеграција**: **MCP алати** *(имплементирано — пуна read-API паралелност: `calendar_events`,
  `calendar_event`, `calendar_history`, `calendar_series`, `calendar_surprises`, `calendar_next`,
  `calendar_blackout`, `calendar_affected_symbols`, `calendar_health`, gated on the feature)* и
  **alerts `EconomicEvent` trigger** *(имплементирано — `AlertRule` koji пали N минута пре
  предстојеће објаве at/above изабраног утицаја, опционо сужено на валуте; процењено од стране
  постојећег alert worker-а без AI, де-дупликовано per објава; креирано преко
  `POST /api/alerts/rules/economic-event`)*. Prop-guard news-blackout gate **и
  copy-trade blackout пауза** су у (§5.1 — opt-in `App:Copy:NewsPauseEnabled`, подразумевано искljučeno: a source
  open чији симбол седи у Critical-impact блокирању се прескаче, byte-identical hot path када је искljučeno). **Backtest event overlay** je у — `GET /api/calendar/v1/for-symbol` и
  `calendar_events_for_symbol` MCP алат враћају point-in-time-коректне догађаје koji утичу на симбол у прозору,
  и **instance/backtest извештај страница** рендерује високо-утицајне објаве koje су пале унутар
  backtest прозора испод equity криве (тако да аутор види које трговине су слетеле на NFP), gated и
  локализовано. Цео план je сада имплементиран.
- **P5 — додаци**: surprise analytics, iCal/CSV export, претраживање кључних речи, plugin consensus.

Види [cBot & REST API референца](calendar-cbot-api.md) за интеграциону површину.

## Извор података је потребан (функција је сакривена без њега)

Календар површинује actual/forecast/previous вредности само из конфигурисаног извора вредности (FRED или
BLS). Без `App:Calendar:FredApiKey` или `App:Calendar:BlsApiKey` функција je **сакривена** од
навигације; ako се форсира омогући (white-label/власник) без кључа, страница приказуje actionable
"configure a data source" обавештење уместо празних вредности, и filter акција остаје сакривена док
извор не буде постављен. Редови догађаја приказују **назив** серија (из каталога), не raw серијски код.
