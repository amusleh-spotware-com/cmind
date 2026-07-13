---
description: "A retail prop firm-ek (FTMO-stílusú) értékelési számlákat adnak el: a kereskedőnek el kell érnie a profit célértéket, miközben kockázati limiteken belül marad (max napi veszteség, max…"
---

# Prop-firm challenge szimuláció

A retail prop firm-ek (FTMO-stílusú) **értékelési számlákat** adnak el: a kereskedőnek el kell érnie a profit célértéket, miközben a kockázati limiteken belül marad (max napi veszteség, max teljes/követő drawdown, konzisztencia, idő limitek), mielőtt funded lesz. A cMind lehetővé teszi a felhasználó számára **egyedi challenge létrehozását bármely ipari formában**, kötést a `TradingAccount`-hoz, **copy-trading működésként való futtatást** — elindítva/leállítva, node-on hostolva, **élőben követve a cTrader Open API felett**. Az aggregátum minden szabályt determinisztikusan értékel; pass vagy breach esetén befejezi a challenge-et, megjelöli, riasztja a felhasználót.

## Domain (bounded context: PropFirm)

`PropFirmChallenge` = agregátum gyökér (modul `Core.PropFirm`), erősen ID-val hivatkozik a `TradingAccount`-jára (nincs cross-aggregátum FK). Birttokolja a szabály értékelést, fázis/állapotgépet, node lease-t.

### Value object-ek & szabály készlet

- **`Money`** (nem-negatív), **`MoneyAmount`** (előjeles), **`Percent`** (0–100], **`TradingDayRequirement`** (0–365).
- **`EquitySnapshot`** `(equity, balance)` — az aggregátumhoz táplált olvasás.
- **`ActivitySnapshot`** `(openPositions, openedInNewsWindow, holdingOverWeekend)` — nem-equity tények.
- **`DailyLossLimit`** `(percent, basis)` — basis `Equity` (intraday, tartalmazza a floating P&L-t) vagy `Balance` (csak realizált).
- **`DrawdownLimit`** — `Static` (a kezdő egyenlegből), `TrailingPercent` (a csúcs equity-ből követő), vagy `TrailingThresholdDollar` (a csúcs equity-től fix dollár összeggel követő, majd **zárol a kezdő egyenlegre**, amint az equity eléri a küszöböt — futures-stílusú).
- **`ConsistencyRule`** `(maxSingleDayShareOfProfit)` — blokkolja a pass-t, amíg egy nap uralja a teljes profitot.
- **`ChallengeRules`** hordozza a fenti plusz `MaxCalendarDays`, `MaxInactivityDays`, `MaxOpenPositions`, `AllowWeekendHolding`, `AllowNewsTrading`, `Kind`, `SingleStep`. A szabály matek a VO-kon él (`DrawdownLimit.IsBreached`, `DailyLossLimit.IsBreached`, `ConsistencyRule.IsSatisfied`); az aggregátum orchestrálja.

### Challenge fajták & sablonok

`ChallengeTemplates.For(kind)` épít egy érvényes presetet `OnePhase`, `TwoPhase`, `ThreePhase`, `InstantFunding`, vagy `Custom` számára (teljes kontroll). Az UI pre-fill-el egy sablont; a felhasználó bármely mezőt állíthatja.

### Fázisok & státusz

- **Fázisok:** `Evaluation → Verification → Funded` (single-step átugorja a Verification-t).
- **Státusz:** `Active`, `Passed`, `Failed`, plusz lifecycle `Stopped` (a követés szüneteltetve) — `Create` elindítja a challenge-et `Active`-ként; `Stop()`/`Resume()` toggle-öli az `Active↔Stopped`-öt.
- **`BreachReason`:** `DailyLoss`, `MaxDrawdown`, `Consistency`, `TimeLimit`, `Inactivity`, `WeekendHolding`, `NewsTrading`, `MaxExposure`.

### Szabály értékelés

- **`RecordEquity(EquitySnapshot, now)`** — görgeti a kereskedési napot a nap határain (elkészíti az előző napi profitot a konzisztencia szabályhoz), frissíti a csúcsokat/napi csúcsokat, majd **első breach-en fail-el** (napi veszteség → drawdown → időlimit → inaktivitás, sorrendben) vagy előreviszi a fázist, amikor a profit cél, a minimum kereskedési nap, a konzisztencia követelmények mind teljesülnek. A sorrend nélküli snapshot-ok és rekordok terminál challenge-en `DomainException`-t dobnak.
- **`RecordActivity(ActivitySnapshot, now)`** — értékeli a viselkedési szabályokat (max nyitott pozíciók, hétvégi tartás, news kereskedés), bélyegzi az aktivitást az inaktivitás szabályhoz.
- Lágy **`PropFirmDrawdownWarning`** egyszer tüzel, amikor az equity használat keresztezi a konfigurálható küszöböt.

Domain események: `PropFirmChallengeStarted`, `PropFirmChallengeStopped`, `PropFirmPhasePassed`, `PropFirmChallengePassed`, `PropFirmChallengeBreached`, `PropFirmDrawdownWarning`.

## Élő követés (Execution) — node-hostolt, ön-gyógyító

A követés tükrözi a copy-trading hosting stack-et pontosan; a prop tracker = **read-only** unokatestvére a copy engine-nek.

- **`PropFirmTrackingSupervisor`** (`src/Nodes/PropFirm`) — `BackgroundService` minden node-on, `App:PropFirm:Enabled`-re kapuzva. Minden ciklus **claim-el** active challenge-eket ön-gyógyító lease-en (`AssignedNode` + `LeaseExpiresAt`; halott node challenge-ei reclaim-elve, amint a lease lejár — ugyanaz az atomikus `ExecuteUpdate` claim mint a copy trading, így két node soha nem double-track-el), megújítja a lease-eket, push-olja a rotált tokeneket a helyükön, leállítja azokat a host-okat, amelyeknek a challenge-e `Active`-t hagyott el.
- **`PropFirmTrackingHost`** (`src/Nodes/PropFirm`) — egy per challenge. Megnyitja az `IOpenApiTradingSession`-t a számlához és az `App:PropFirm:EquityPollInterval`-en újraszámolja az élő equity-t, táplálja az aggregátumnak. Swap-olja az access token-t a helyén rotáción (nincs session drop). Kipróbál, amikor a challenge már nem `Active`.
- **`PropFirmEquityCalculator`** (`src/CTraderOpenApi/Client`) — cTrader-hű equity matek. Az equity **nem** a Open API-tól jön, tehát származtatva: `equity = balance + Σ(unrealized P&L)`, ahol minden pozíció P&L-je `priceDifference × units × quote→deposit rate + swap + commission` (`units = wire volume / 100`; long az ask-on, short a bid-en értékelődik át). A balance a `ProtoOATrader`-ből; a pozíciók (entry price, swap, commission) a reconcile-ből; élő bid/ask a spot subscriptions-ből. Tiszta és izolált — a currency-conversion hot spot egységtesztelve önállóan.

## Riasztások

A `PropFirmAlertNotifier` (`src/Infrastructure/PropFirm`) feliratkozik a pass/breach/warning domain eseményekre (regisztrálva mint `IDomainEventHandler<>`, dispatch-elve sikeres `SaveChanges` után), értesíti a felhasználót strukturált riasztási/audit nyomvonalon (`LogMessages`). Az élő UI ugyanazt a státusz változást tükrözi. Ez = cross-context reakció — soha nem mutálja a challenge aggregátumot.

## API (`/api/prop-firm`, feature `PropFirm`, role User+)

| Metódus | Útvonal | Cél |
|---------|---------|-----|
| GET | `/challenges` | list user's challenges (kind, phase, status, live equity, lease) |
| GET | `/challenges/{id}` | one challenge |
| GET | `/templates` | industry presets for create dialog |
| POST | `/challenges` | create from template **or** fully custom rule set |
| POST | `/challenges/{id}/start` | resume tracking (Stopped → Active) |
| POST | `/challenges/{id}/stop` | stop tracking (Active → Stopped, release lease) |
| POST | `/challenges/{id}/equity` | record equity snapshot → re-evaluate (manual/no-live-feed path) |
| DELETE | `/challenges/{id}` | soft-delete (blocked while Active) |

MCP: `Mcp/Tools/PropFirmTools.cs` ki van téve list/create(from template)/record-equity/start/stop, `PropFirm` feature-re kapuzva.

UI: `/prop-firm` (nav *Prop Firm*, `PropFirm` flag-re kapuzva) listázza a challenge-eket **Start/Stop/Delete** sor akciókkal (Start ha Stopped, Stop ha Active, Delete le van tiltva míg Active), létrehozza őket a `NewPropFirmChallengeDialog`-on keresztül (sablon választó + teljes szabály szerkesztő). Minden create/edit MudBlazor dialogon át.

## Élő equity feed — megoldva

A korábbi "nincs élő számla P&L feed" rés betöltve: amikor az `App:PropFirm:Enabled` be van állítva, a node-ok a számlát élőben követik az Open API felett, automatikusan táplálják az equity-t. Nélküle (alapértelmezés) a domain és a **manual-equity** útvonal (`POST …/equity`) változatlanul fut — nincs cTrader hitelesítés kell a build/test/E2E-hez.

## Tesztek

- **Egység** — `UnitTests/PropFirm/`: `PropFirmChallengeTests` (fázis előrehaladás, min-days, static/trailing drawdown, napi veszteség, terminál/sorrend nélküli őrök); `PropFirmChallengeRulesTests` (balance vs equity napi-loss basis, trailing-threshold-dollar trail+lock, konzisztencia blokk/engedélyezés, idő-limit, inaktivitás, max-exposure, hétvége, news, stop/resume, lease határ, pass release lease, drawdown warning); `PropFirmValueObjectTests` (VO tartományok + rule-VO matek); `PropFirmEquityCalculatorTests` (long/short P&L, swap/commission, quote→deposit konverzió, hiányzó pricing); `PropFirmTrackingHostTests` (az élő equity hajtja a pass/fail-t az extended fake session ellen); `PropFirmAlertNotifierTests`. Idő explicit / `FakeTimeProvider` — nincs wall-clock olvasás.
- **Integráció** — `IntegrationTests/`: `PropFirmChallengePersistenceTests` (round-trip + record-equity + soft-delete, enriched-rules + lease round-trip) és `PropFirmTrackingLeaseTests` (claim, contested lease, reclaim after lapse across two node identities) valódi Postgres-en.
- **E2E** — `E2ETests/PropFirmTests.cs`: create + record-equity to `Passed`; stop→start→breach flow; templates endpoint.
- **Stress / DST** — `StressTests/PropFirm/PropFirmChallengeDstTests.cs`: seedelt randomizált equity/aktivitás stream-ek (nap görgetések, spike-ok, crash-ek, duplikált + sorrend nélküli snapshot-ok, exposure/hétvége/news) sok vegyes szabály challenge-en keresztül, assert-elve a sticky exactly-once terminál állapotokat, peak-bounds-current invariánst, indokolt hibákat.

## Konfiguráció (`App:PropFirm`)

`Enabled` (alapértelmezés ki), `ReconcileInterval`, `EquityPollInterval`, `LeaseTtl`, `DrawdownWarnThresholdPercent`, `NodeName`.
