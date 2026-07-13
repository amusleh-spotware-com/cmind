---
description: "Retail prop firmy (FTMO-style) predaj ohodnocovania účtov: obchodník musí dosiahnuť profit cieľ, zatiaľ čo ostáva vnútri riziko limitov (max denný strata, max…"
---

# Prop-firm výzva simulácia

Retail prop firmy (FTMO-style) predaj **ohodnocovania účtov**: obchodník musí dosiahnuť profit cieľ, zatiaľ
ostáva vnútri riziko limitov (max denný strata, max totálny/trailing drawdown, konzistencia, čas limitov) pred
financovaný. cMind umožňuje užívateľ vytvoriť **vlastný výzva žiadny priemysel tvar**, viažu na
`TradingAccount`, **spustiť ako copy-trading operácia** — spustené/zastavené, hostovaný na uzol,
sledované **live cez cTrader Open API**. Agregát vyhodnocuje každý pravidlo deterministicky; na
prejsť alebo porušenie, koniec výzva, značka to upozorní užívateľa.

## Doména (ohraničená kontext: PropFirm)

`PropFirmChallenge` = agregát root (modul `Core.PropFirm`), odkazy Its `TradingAccount` podľa
silný id iba (žádny cross-aggregate FK). Vlastní pravidlo vyhodnotenie, fáza/stav stroj, uzol
lease.

### Objekty hodnôt & pravidlo nastavene

- **`Money`** (non-negative), **`MoneyAmount`** (podpísané), **`Percent`** (0–100], **`TradingDayRequirement`** (0–365).
- **`EquitySnapshot`** `(equity, balance)` — čítanie podávané agregátu.
- **`ActivitySnapshot`** `(openPositions, openedInNewsWindow, holdingOverWeekend)` — non-equity faktom.
- **`DailyLossLimit`** `(percent, basis)` — základ `Equity` (intraday, zahŕňa plávajúce P&L) alebo `Balance`
  (realizované iba).
- **`DrawdownLimit`** — `Static` (z počiatočného balance), `TrailingPercent` (z peak equity), alebo
  `TrailingThresholdDollar` (cesty equity peak fixným dolár sumu, potom **zámky na začiatku
  balance** raz equity dosiahne prah — futures-style).
- **`ConsistencyRule`** `(maxSingleDayShareOfProfit)` — bloky prejsť zatiaľ jeden deň ovláda celku zisku.
- **`ChallengeRules`** niesol vyššie plus `MaxCalendarDays`, `MaxInactivityDays`, `MaxOpenPositions`,
  `AllowWeekendHolding`, `AllowNewsTrading`, `Kind`, `SingleStep`. Pravidlo maths žijú na VOs
  (`DrawdownLimit.IsBreached`, `DailyLossLimit.IsBreached`, `ConsistencyRule.IsSatisfied`); agregát
  orchestruje.

### Výzva druhy & šablón

`ChallengeTemplates.For(kind)` stavy platný predvoľba na `OnePhase`, `TwoPhase`, `ThreePhase`,
`InstantFunding` alebo `Custom` (úplná kontrola). UI pre-vyplnenia šablón; užívateľ môžu upraviť žádne pole.

### Fázy & status

- **Fázy:** `Evaluation → Verification → Funded` (single-step preskakuje Verification).
- **Status:** `Active`, `Passed`, `Failed`, plus životný cyklus `Stopped` (sledovanie pozastavené) — `Create` počíná
  výzva `Active`; `Stop()`/`Resume()` toggle `Active↔Stopped`.
- **`BreachReason`:** `DailyLoss`, `MaxDrawdown`, `Consistency`, `TimeLimit`, `Inactivity`,
  `WeekendHolding`, `NewsTrading`, `MaxExposure`.

### Pravidlo vyhodnotenie

- **`RecordEquity(EquitySnapshot, now)`** — valcov obchodný deň na deň hranice (zachytí predchádzajúce
  deň zisku za pravidlo konzistencia), aktualizácie peak/daily vrcholy, potom **zlyhá na prvý porušenie**
  (denný strata → drawdown → limit času → nečinnosť, v poradí) alebo pokroku fázu keď zisku cieľ,
  minimálne-trading-day, konzistencia požiadavky všetci splnili. Out-of-order snímke a záznamy na
  terminál výzva hodiť `DomainException`.
- **`RecordActivity(ActivitySnapshot, now)`** — vyhodnocuje správanie pravidlá (max otvorené pozície, weekend
  držby, news obchodovania), pečiatok činnosti na nečinnosť pravidla.
- Soft **`PropFirmDrawdownWarning`** požáry raz keď equity používanie kríži konfigurovateľný prah.

Doménové udalosti: `PropFirmChallengeStarted`, `PropFirmChallengeStopped`, `PropFirmPhasePassed`,
`PropFirmChallengePassed`, `PropFirmChallengeBreached`, `PropFirmDrawdownWarning`.

## Live sledovanie (vykonávanie) — uzol-hostovaný, samoreparácia

Sledovanie zrkadiel copy-trading hosting stack presne; prop tracker = **read-only** bratranca
copy engine.

- **`PropFirmTrackingSupervisor`** (`src/Nodes/PropFirm`) — `BackgroundService` na každom uzle, gated na
  `App:PropFirm:Enabled`. Každý cyklus **nároky** aktívny výzvy na samoreparácia lease
  (`AssignedNode` + `LeaseExpiresAt`; mŕtvy uzol výzvy reclaimed raz lease lapses —
  rovnaký atómový `ExecuteUpdate` nároku ako copy trading, takže dva uzly nikdy double-track), obnovuje leases,
  tlačí rotované tokeny v mieste, zastaví hostiteľa ktorého výzva ľavá `Active`).
- **`PropFirmTrackingHost`** (`src/Nodes/PropFirm`) — jeden na výzvu. Otvára `IOpenApiTradingSession`
  na konto a, na `App:PropFirm:EquityPollInterval`, precomputes live equity, podajů na
  agregátu. Vymeňuje prístupový token na mieste rotácia (bez session drop). Výstupy keď výzva
  žádny dlhšie `Active`.
- **`PropFirmEquityCalculator`** (`src/CTraderOpenApi/Client`) — cTrader-verný equity maths.
  Equity **nie** dodané podľa Open API, takže odvodené: `equity = balance + Σ(unrealized P&L)`,
  kde každý pozícia P&L je `priceDifference × jednotiek × quote→deposit sadzba + swap + provízií`
  (`units = drôt objem / 100`; dlhé revalues na ponuke, krátke na ásk). Balance z
  `ProtoOATrader`; pozície (vstupná cena, swap, provízií) z rekoncilácia; live ponuka/ásk z spot
  predplatí. Čisté a izolované — mena-konverzii hot miesto unit-testované na Its vlastný.

## Upozornenia

`PropFirmAlertNotifier` (`src/Infrastructure/PropFirm`) zdieľajú na prejsť/porušenie/upozornenie doménové udalosti
(zaregistrovaný ako `IDomainEventHandler<>`, odposlaný po úspešný `SaveChanges`), upozorniť užívateľa
cez štruktúrovaný alert/audit trať (`LogMessages`). Live UI odrazí rovnaký status zmena. Toto
= cross-context reakcia — nikdy mutuje výzva agregátu.

## API (`/api/prop-firm`, funkcii `PropFirm`, rola User+)

| Metódu | Trasa | Účel |
|--------|-------|---------|
| GET | `/challenges` | zoznam užívateľ výzvy (druh, fáza, status, live equity, lease) |
| GET | `/challenges/{id}` | jeden výzva |
| GET | `/templates` | priemysel predvoľby na vytvoriť dialóg |
| POST | `/challenges` | vytvoriť z šablóny **alebo** plne vlastný pravidlo nastavené |
| POST | `/challenges/{id}/start` | obnovuje sledovanie (Stopped → Active) |
| POST | `/challenges/{id}/stop` | zastaviť sledovanie (Active → Stopped, uvoľniť lease) |
| POST | `/challenges/{id}/equity` | záznam equity snapshot → re-vyhodnocuje (manuálny/bez-live-feed cesta) |
| DELETE | `/challenges/{id}` | soft-delete (zablokovaný kým je Active) |

MCP: `Mcp/Tools/PropFirmTools.cs` vystavuje zoznam/vytvoriť(od šablóny)/record-equity/start/stop, gated na
`PropFirm` funkcie.

UI: `/prop-firm` (nav *Prop Firm*, gated podľa `PropFirm` vlajka) zoznam výzvy s **Start/Stop/Delete**
riadok akcie (Start keď Stopped, Stop keď Active, Delete zablokovaný kým je Active), vytvára ich cez
`NewPropFirmChallengeDialog` (šablóna výber + úplný pravidlo editor). Všetky vytvoriť/upraviť cez MudBlazor dialóg.

## Live equity feed — vyriešené

Skorý "žádny live účet P&L feed" medzera zavretá: keď `App:PropFirm:Enabled` nastaviť, uzly sledovať
účet live cez Open API, podajů equity automaticky. Bez to (štandardne), doména a
**manual-equity** cesta (`POST …/equity`) spustiť nezmenené — žádny cTrader poverenia potrebný na build/test/E2E.

## Testy

- **Jednotka** — `UnitTests/PropFirm/`: `PropFirmChallengeTests` (fáza pokrok, min-dni, statický/trailing
  drawdown, denný strata, terminál/out-of-order ochrana); `PropFirmChallengeRulesTests` (balance vs equity
  denný-strata základ, trailing-threshold-dollar trail+lock, konzistencia blok/povolí, čas-limit, nečinnosť,
  max-exposure, weekend, news, stop/resume, lease hranica, prejsť uvoľní lease, drawdown upozornenie);
  `PropFirmValueObjectTests` (VO rozsahy + pravidlo-VO maths); `PropFirmEquityCalculatorTests` (dlhé/krátke P&L,
  swap/provízií, quote→deposit konverzii, chýbajúce ceny); `PropFirmTrackingHostTests` (live equity
  pohány prejsť/zlyhanie voči rozšírený falošný relác); `PropFirmAlertNotifierTests`. Čas explicitný /
  `FakeTimeProvider` — žádny wall-clock čítané.
- **Integrácia** — `IntegrationTests/`: `PropFirmChallengePersistenceTests` (round-trip + record-equity +
  soft-delete, obohatené-pravidlá + lease round-trip) a `PropFirmTrackingLeaseTests` (nároku, spore lease,
  reclaim po lapse naprieč dva uzol identity) na reálny Postgres.
- **E2E** — `E2ETests/PropFirmTests.cs`: vytvoriť + record-equity na `Passed`; stop→start→breach tok;
  šablóny koncový bod.
- **Stress / DST** — `StressTests/PropFirm/PropFirmChallengeDstTests.cs`: osemené randomizovaný equity/aktivita
  prietok (deň valcov, kolíky, havárií, duplikát + out-of-order snímek, expozície/weekend/news) naprieč
  veľa zmiešané-pravidlo výzvy, tvrdením lepkavý presne-raz terminál stavy, peak-bounds-current invariant,
  dôvodi zlyhania.

## Konfigurácia (`App:PropFirm`)

`Enabled` (vypnutý štandardne), `ReconcileInterval`, `EquityPollInterval`, `LeaseTtl`,
`DrawdownWarnThresholdPercent`, `NodeName`.
