---
description: "Мањане проп фирме (FTMO-стил) продај оцена налога: трговац мора ударити профит циљ док остајући унутар риск лимити (макс дневни лос, макс…"
---

# Проп-фирме изазов симулација

Мањане проп фирме (FTMO-стил) продај **оцена налога**: трговац мора ударити профит циљ док
остајући унутар риск лимити (макс дневни лос, макс укупна/завршног преклон, конзистенција, време лимити) пред
финансиран. cMind дозволи корисник направи **прилагођени изазов од свака индустрија облик**, везаност
`TradingAccount`, **трчи као копирање-трговање операција** — почети/стопа, домаћин на чвор,
праћен **живо преко cTrader Open API**. Агрегат оцењује сваки правило детерминистичка; на
проход или брешњ, завршава изазов, означава то, упозорава корисник.

## Домена (ограничена контекст: PropFirm)

`PropFirmChallenge` = агрегат корен (модула `Core.PropFirm`), референце то `TradingAccount` од
јачина ID само (не крос-агрегат FK). Власнички правило оцена, фаза/стање машина, чвор
лизе.

### Вредност објекти & правило скуп

- **`Money`** (не-негативна), **`MoneyAmount`** (потписана), **`Percent`** (0–100], **`TradingDayRequirement`** (0–365).
- **`EquitySnapshot`** `(капитал, биланс)` — читање наведено агрегат.
- **`ActivitySnapshot`** `(openPositions, openedInNewsWindow, holdingOverWeekend)` — не-капитал чињенице.
- **`DailyLossLimit`** `(проценат, основа)` — основа `Equity` (иудан, укључује плутајући P&L) или `Balance`
  (реализована само).
- **`DrawdownLimit`** — `Static` (од почетни биланс), `TrailingPercent` (од врхо капитал), или
  `TrailingThresholdDollar` (траила капитал врхо од фиксна долар износ, тада **браве на почетни
  биланс** једнапут капитал достигне граница — будући-стил).
- **`ConsistencyRule`** `(maxSingleDayShareOfProfit)` — блокира проход док једна дана доминантна укупна профит.
- **`ChallengeRules`** носи горе плус `MaxCalendarDays`, `MaxInactivityDays`, `MaxOpenPositions`,
  `AllowWeekendHolding`, `AllowNewsTrading`, `Kind`, `SingleStep`. Правило математика живи на VOs
  (`DrawdownLimit.IsBreached`, `DailyLossLimit.IsBreached`, `ConsistencyRule.IsSatisfied`); агрегат
  оркестрира.

### Изазов врста & шаблона

`ChallengeTemplates.For(kind)` градови важећи пресет за `OnePhase`, `TwoPhase`, `ThreePhase`,
`InstantFunding`, или `Custom` (пуна контрола). UI пре-пуни шаблон; корисник могу прилагодити сваки поље.

### Фаза & статус

- **Фаза:** `Evaluation → Verification → Funded` (једна-корак пропусти Verification).
- **Статус:** `Active`, `Passed`, `Failed`, плус животни циклус `Stopped` (праћење паузирана) — `Create` почињу
  изазов `Active`; `Stop()`/`Resume()` укључивање/искључивање `Active↔Stopped`.
- **`BreachReason`:** `DailyLoss`, `MaxDrawdown`, `Consistency`, `TimeLimit`, `Inactivity`,
  `WeekendHolding`, `NewsTrading`, `MaxExposure`.

### Правило оцена

- **`RecordEquity(EquitySnapshot, now)`** — роли трговачка дана при дана граница (хвата претходни
  дна профит за конзистенција правило), освежи врхо/дневни врхо, тада **неуспех на прво брешњ**
  (дневни лос → преклон → време лимит → неактивност, у ред) или напредни фаза када профит циљ,
  минимум-трговачка-дна, конзистенција захтевци сви исплаћени. Ван-ред снимци и записи на
  терминала изазов хвата `DomainException`.
- **`RecordActivity(ActivitySnapshot, now)`** — оцењује однос правила (макс отворена позиција, викенд
  задршка, новости трговања), печата активност за неактивност правило.
- Меко **`PropFirmDrawdownWarning`** пали једнапут када капитал употреба скупови подешавања граница.

Домена ferdinand: `PropFirmChallengeStarted`, `PropFirmChallengeStopped`, `PropFirmPhasePassed`,
`PropFirmChallengePassed`, `PropFirmChallengeBreached`, `PropFirmDrawdownWarning`.

## Живо праћење (Execution) — чвор-домаћин, само-оздрављивање

Праћење огледала копирање-трговање домаћин стек управо; проп трочерак = **само-читање** септеник од
копирање мотора.

- **`PropFirmTrackingSupervisor`** (`src/Nodes/PropFirm`) — `BackgroundService` на сваки чвор, гатирано на
  `App:PropFirm:Enabled`. Свака цикл **захтева** активна изазови на само-оздрављивање лизе
  (`AssignedNode` + `LeaseExpiresAt`; мртав чвор изазови захтева једнапут лизе lapses —
  исти атомског `ExecuteUpdate` захтева као копирање трговања, тако два чвора никад двоструко-трачук), обнови лизе,
  шаља ротирана токени на месту, стопа домаћин чији изазов остави `Active`).
- **`PropFirmTrackingHost`** (`src/Nodes/PropFirm`) — једна по изазову. Отворава `IOpenApiTradingSession`
  за налог и, на `App:PropFirm:EquityPollInterval`, реком живо капитал, храни
  агрегат. Замене приступ токен на месту на ротације (не сеансе пада). Излаза када изазов
  дакле дубина `Active`.
- **`PropFirmEquityCalculator`** (`src/CTraderOpenApi/Client`) — cTrader-верна капитал математика.
  Капитал **не** доставли од Open API, тако дериван: `equity = balance + Σ(unrealized P&L)`,
  где свака позицију P&L је `priceDifference × units × quote→deposit стопа + замена + комисија`
  (`units = жица том / 100`; дугачак преречуна на понуда, кратак на спој). Биланс од
  `ProtoOATrader`; позиција (унос цена, замена, комисија) од помирба; живо понуда/спој од место
  претплате. Чист и изолирано — валута-конверзија врућа место јединица-тестирано на то сопствено.

## Упозорења

`PropFirmAlertNotifier` (`src/Infrastructure/PropFirm`) чита даљи/брешњ/упозорење домена ferdinand
(регистрирано као `IDomainEventHandler<>`, опход после успешан `SaveChanges`), обавештава корисник
кроз структурирано упозорење/аудит трајним (`LogMessages`). Живо UI одражава исти статус промена. Ово
= крос-контекст реакција — никад мутира изазов агрегат.

## API (`/api/prop-firm`, функција `PropFirm`, улога User+)

| Метода | Путања | Намена |
|--------|-------|---------|
| GET | `/challenges` | листа корисник изазови (врста, фаза, статус, живо капитал, лизе) |
| GET | `/challenges/{id}` | једна изазов |
| GET | `/templates` | индустрија пресети за направи дијалог |
| POST | `/challenges` | направи од шаблон **или** потпуно прилагођени правило скуп |
| POST | `/challenges/{id}/start` | наставити праћење (Stopped → Active) |
| POST | `/challenges/{id}/stop` | стоп праћење (Active → Stopped, отпусти лизе) |
