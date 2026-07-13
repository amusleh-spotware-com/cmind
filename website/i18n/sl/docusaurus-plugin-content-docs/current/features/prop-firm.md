---
description: "Prodajalci prop firm (FTMO-stil) prodajajo evalvacijske račune: trgovalec mora zadeti cilj dobička medtem ko ostane znotraj limitov tveganja (max dnevna izguba, max…"
---

# Simulacija prop-firm izziva

Prodajalci prop firm (FTMO-stil) prodajajo **evalvacijske račune**: trgovalec mora zadeti cilj dobička medtem ko
ostane znotraj limitov tveganja (max dnevna izguba, max skupna/sledeča izguba, konsistentnost, časovni limiti) preden
je financiran. cMind omogoča uporabniku ustvarjanje **poljubnega izziva katerekoli industrijske oblike**, vezava na
`TradingAccount`, **zagon kot operacija kopiranja trgovanja** — začetek/ konec, gostovano na vozlišču,
sledeno **v živo prek cTrader Open API**. Agregat oceni vsako pravilo deterministično; ob
uspehu ali prelomu, konča izziv, označi ga, opozori uporabnika.

## Domena (omejeni kontekst: PropFirm)

`PropFirmChallenge` = agregatni koren (modul `Core.PropFirm`), referecira svoj `TradingAccount` z
močnim id (brez cross-agregat FK). Ima lastno evalvacijo pravil, fazni/strojni stroj,
lease vozlišča.

### Vrednostni objekti in nabor pravil

- **`Money`** (nenegativno), **`MoneyAmount`** (predznačeno), **`Percent`** (0–100], **`TradingDayRequirement`** (0–365).
- **`EquitySnapshot`** `(equity, balance)` — branje napajano agregatu.
- **`ActivitySnapshot`** `(openPositions, openedInNewsWindow, holdingOverWeekend)` — ne-equity dejstva.
- **`DailyLossLimit`** `(percent, basis)` — osnova `Equity` (intraday, vključuje floating P&L) ali `Balance`
  (samo realizirano).
- **`DrawdownLimit`** — `Static` (od začetne bilance), `TrailingPercent` (od vrha equity), ali
  `TrailingThresholdDollar` (sledi vrhu equity za fiksni znesek v dolarjih, nato **zaklene na začetno
  bilanco** ko equity doseže prag — stil futures).
- **`ConsistencyRule`** `(maxSingleDayShareOfProfit)` — blokira uspeh medtem ko en dan dominira skupni dobiček.
- **`ChallengeRules`** nosi zgoraj plus `MaxCalendarDays`, `MaxInactivityDays`, `MaxOpenPositions`,
  `AllowWeekendHolding`, `AllowNewsTrading`, `Kind`, `SingleStep`. Matematika pravil živi na VOs
  (`DrawdownLimit.IsBreached`, `DailyLossLimit.IsBreached`, `ConsistencyRule.IsSatisfied`); agregat
  orkestrira.

### Vrste izzivov in predloge

`ChallengeTemplates.For(kind)` zgradi veljaven prednastavljen za `OnePhase`, `TwoPhase`, `ThreePhase`,
`InstantFunding`, ali `Custom` (popoln nadzor). UI prednapolni predlogo; uporabnik lahko prilagodi katerokoli polje.

### Faze in status

- **Faze:** `Evaluation → Verification → Funded` (en korak preskoči Verification).
- **Status:** `Active`, `Passed`, `Failed`, plus življenjski cikel `Stopped` (sledenje pavzirano) — `Create` zažene
  izziv `Active`; `Stop()`/`Resume()` preklaplja `Active↔Stopped`.
- **`BreachReason`:** `DailyLoss`, `MaxDrawdown`, `Consistency`, `TimeLimit`, `Inactivity`,
  `WeekendHolding`, `NewsTrading`, `MaxExposure`.

### Evalvacija pravil

- **`RecordEquity(EquitySnapshot, now)`** — zavrti trgovalni dan na mejah dneva (zajame prejšnjega
  dneva dobiček za konsistentnost pravilo), posodobi vrh/dnevne vrhove, nato **propade ob prvem prelomu**
  (dnevna izguba → drawdown → časovni limit → neaktivnost, po vrstnem redu) ali napreduje fazo ko so cilj dobička,
  minimalni-trgovalni-dnevi, konsistentnost zahteve vse izpolnjene. Out-of-order posnetki in zapisi na
  terminalnem izzivu vržejo `DomainException`.
- **`RecordActivity(ActivitySnapshot, now)`** — oceni vedenjska pravila (max odprte pozicije, vikend
  držanje, novinarstvo), žig activity za neaktivnost pravilo.
- Mehki **`PropFirmDrawdownWarning`** sproži enkrat ko equity uporaba prečka konfigurabilni prag.

Domenski dogodki: `PropFirmChallengeStarted`, `PropFirmChallengeStopped`, `PropFirmPhasePassed`,
`PropFirmChallengePassed`, `PropFirmChallengeBreached`, `PropFirmDrawdownWarning`.

## Sledenje v živo (Execution) — vozlišče-gostovano, samozdravljeno

Sledenje zrcali kopiraj-trgovanje gostiteljskega sklada natančno; prop sledilnik = **samo-branje**
sorodnik kopiraj motorja.

- **`PropFirmTrackingSupervisor`** (`src/Nodes/PropFirm`) — `BackgroundService` na vsakem vozlišču, na vratih
  `App:PropFirm:Enabled`. Vsak cikel **zahteva** aktivne izzive na samozdravljivem lease
  (`AssignedNode` + `LeaseExpiresAt`; mrtvo vozlišče's izzivi pridobljeni ko lease mine —
  isto atomsko `ExecuteUpdate` zahtevek kot kopiranje trgovanja, torej dve vozlišči nikoli dvojno-sledita), obnavlja lease,
  potiska zavrnjene žetone na mestu, ustavi gostitelje katerih izziv je zapustil `Active`.
- **`PropFirmTrackingHost`** (`src/Nodes/PropFirm`) — eden na izziv. Odpre `IOpenApiTradingSession`
  za račun in, na `App:PropFirm:EquityPollInterval`, preračuna živo equity, pošlje agregatu. Zamenja
  dostopovni žeton na mestu ob rotaciji (brez padca seje). Izhaja ko izziv ni več `Active`.
- **`PropFirmEquityCalculator`** (`src/CTraderOpenApi/Client`) — cTrader-verna equity matematika.
  Equity **ni** dostavljena od Open API, torej izpeljana: `equity = balance + Σ(unrealized P&L)`,
  kjer je P&L vsake pozicije `priceDifference × units × quote→deposit rate + swap + commission`
  (`units = wire volume / 100`; dolg revalvira na bid, kratek na ask). Balance od
  `ProtoOATrader`; pozicije (vstopna cena, swap, provizija) od uskladitve; živi bid/ask od spot naročnin. Čist
  in izoliran — vroča točka konverzije valut enot-testirana zase.

## Opozorila

`PropFirmAlertNotifier` (`src/Infrastructure/PropFirm`) se naroči na pass/breach/warning domenske dogodke
(registrirani kot `IDomainEventHandler<>`, dispaccirani po uspešnem `SaveChanges`), obvesti uporabnika
skozi strukturirano opozorilo/audit sled (`LogMessages`). Živi UI odseva isto spremembo stanja. To
= cross-context reakcija — nikoli ne mutira agregata izziva.

## API (`/api/prop-firm`, funkcija `PropFirm`, vloga User+)

| Metoda | Pot | Namen |
|--------|-------|-------|
| GET | `/challenges` | seznam uporabnikovih izzivov (kind, faza, status, živa equity, lease) |
| GET | `/challenges/{id}` | en izziv |
| GET | `/templates` | industrijske predloge za dialog ustvarjanja |
| POST | `/challenges` | ustvari iz predloge **ali** popolnoma custom nabor pravil |
| POST | `/challenges/{id}/start` | nadaljuj sledenje (Stopped → Active) |
| POST | `/challenges/{id}/stop` | ustavi sledenje (Active → Stopped, sprosti lease) |
| POST | `/challenges/{id}/equity` | zabeleži equity snapshot → ponovno oceni (ročno/ni-feed-pot) |
| DELETE | `/challenges/{id}` | mehko brisanje (blokirano med Active) |

MCP: `Mcp/Tools/PropFirmTools.cs` razkriva seznam/ustvari(iz predloge)/zabeleži-equity/start/stop, na vratih
`PropFirm` funkcija.

UI: `/prop-firm` (nav *Prop Firm*, na vratih `PropFirm` zastavica) seznam izzivov z **Start/Stop/Delete**
vrstičnimi akcijami (Start ko Stopped, Stop ko Active, Delete onemogočeno med Active), ustvarja jih skozi
`NewPropFirmChallengeDialog` (izbirnik predloge + poln urejevalnik pravil). Vsi ustvari/uredi prek MudBlazor dialoga.

## Živi equity feed — razrešeno

Prejšnja vrzel "ni živega feed-a P&L računa" zaprta: ko je `App:PropFirm:Enabled` nastavljeno, vozlišča sledijo
račun v živo prek Open API, pošiljajo equity avtomatsko. Brez nje (privzeto), domena in
**ročna-equity** pot (`POST …/equity`) tečeta nespremenjeno — nobene cTrader poverilnice potrebne za build/test/E2E.

## Testi

- **Enote** — `UnitTests/PropFirm/`: `PropFirmChallengeTests` (fazni napredek, min-dnevi, statični/sledeči
  drawdown, dnevna izguba, terminal/out-of-order varovalke); `PropFirmChallengeRulesTests` (balance vs equity
  dnevna-izguba osnova, trailing-threshold-dollar sled+zaklep, konsistentnost blok/dovoli, časovni limit, neaktivnost,
  max-exposure, vikend, novice, stop/resume, meja lease, pass sprosti lease, drawdown opozorilo);
  `PropFirmValueObjectTests` (VO obsegi + pravilo-VO matematika); `PropFirmEquityCalculatorTests` (dolg/kratek P&L,
  swap/provizija, quote→deposit konverzija, manjkajoče cene); `PropFirmTrackingHostTests` (živa equity
  poganja pass/fail proti razširjenemu fake seji); `PropFirmAlertNotifierTests`. Čas ekspliciten /
  `FakeTimeProvider` — brez branje realne ure.
- **Integracija** — `IntegrationTests/`: `PropFirmChallengePersistenceTests` (round-trip + zabeleži-equity +
  mehko-brisanje, obogateni-pravila + lease round-trip) in `PropFirmTrackingLeaseTests` (zahtevek, izpodbijani lease,
  povrnitev po izteku čez dve vozlišči identiteti) na resničnem Postgres.
- **E2E** — `E2ETests/PropFirmTests.cs`: ustvari + zabeleži equity do `Passed`; stop→start→breach potek;
  predloge končna točka.
- **Stress / DST** — `StressTests/PropFirm/PropFirmChallengeDstTests.cs`: sejane naključne equity/activity
  streame (dnevni zavoji, vrhovi, zrušitve, podvojieni + out-of-order posnetki, exposure/vikend/novice) čez
  mešane- pravila izzive, trdijo natanko-enkratne končne stvari, peak-bounds-current invariant,
  utemeljene napake.
- **Konfiguracija** (`App:PropFirm`)

`Enabled` (privzeto off), `ReconcileInterval`, `EquityPollInterval`, `LeaseTtl`,
`DrawdownWarnThresholdPercent`, `NodeName`.
