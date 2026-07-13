---
description: "Le prop firm al dettaglio (stile FTMO) vendono account di valutazione: il trader deve raggiungere l'obiettivo di profitto mentre rimane entro i limiti di rischio (max perdita giornaliera, max…"
---

# Simulazione sfida prop-firm

Le prop firm al dettaglio (stile FTMO) vendono **account di valutazione**: il trader deve raggiungere l'obiettivo di profitto mentre
rimane entro i limiti di rischio (max perdita giornaliera, max drawdown totale/trailing, consistenza, limiti di tempo) prima di
essere finanziato. cMind consente all'utente di creare una **sfida personalizzata di qualsiasi forma industriale**, legarla a
`TradingAccount`, **eseguirla come un'operazione di copy-trading** — avviata/fermata, ospitata su nodo,
tracciata **live su cTrader Open API**. L'aggregato valuta ogni regola deterministicamente; al
passaggio o violazione, termina la sfida, la contrassegna, avvisa l'utente.

## Dominio (bounded context: PropFirm)

`PropFirmChallenge` = radice di aggregato (modulo `Core.PropFirm`), riferisce il suo `TradingAccount` solo
da strong id (nessuna FK cross-aggregato). Possiede valutazione delle regole, macchina a stati/fase, lease di nodi.

### Oggetti di valore e set di regole

- **`Money`** (non-negativo), **`MoneyAmount`** (firmato), **`Percent`** (0–100], **`TradingDayRequirement`** (0–365).
- **`EquitySnapshot`** `(equity, bilancio)` — lettura fornita all'aggregato.
- **`ActivitySnapshot`** `(openPositions, openedInNewsWindow, holdingOverWeekend)` — fatti non-equity.
- **`DailyLossLimit`** `(percentuale, base)` — base `Equity` (intraday, include P&L fluttuante) o `Balance`
  (solo realizzato).
- **`DrawdownLimit`** — `Static` (dal bilancio iniziale), `TrailingPercent` (dal picco di equity), o
  `TrailingThresholdDollar` (insegue il picco di equity di un importo fisso in dollari, quindi **si blocca al bilancio iniziale**
  una volta che l'equity raggiunge la soglia — stile futures).
- **`ConsistencyRule`** `(maxSingleDayShareOfProfit)` — blocca il passaggio mentre un giorno domina il profitto totale.
- **`ChallengeRules`** trasporta quanto sopra più `MaxCalendarDays`, `MaxInactivityDays`, `MaxOpenPositions`,
  `AllowWeekendHolding`, `AllowNewsTrading`, `Kind`, `SingleStep`. La matematica delle regole vive su VO
  (`DrawdownLimit.IsBreached`, `DailyLossLimit.IsBreached`, `ConsistencyRule.IsSatisfied`); l'aggregato
  orchestra.

### Tipi di sfida e modelli

`ChallengeTemplates.For(kind)` costruisce preset valido per `OnePhase`, `TwoPhase`, `ThreePhase`,
`InstantFunding`, o `Custom` (controllo completo). L'UI pre-riempie il modello; l'utente può regolare qualsiasi campo.

### Fasi e stato

- **Fasi:** `Evaluation → Verification → Funded` (single-step salta Verification).
- **Stato:** `Active`, `Passed`, `Failed`, più ciclo di vita `Stopped` (tracciamento in pausa) — `Create` avvia
  sfida `Active`; `Stop()`/`Resume()` toggle `Active↔Stopped`.
- **`BreachReason`:** `DailyLoss`, `MaxDrawdown`, `Consistency`, `TimeLimit`, `Inactivity`,
  `WeekendHolding`, `NewsTrading`, `MaxExposure`.

### Valutazione delle regole

- **`RecordEquity(EquitySnapshot, now)`** — rotola il giorno di trading ai confini del giorno (cattura il profitto del giorno
  precedente per la regola di consistenza), aggiorna picchi/picchi giornalieri, quindi **fallisce alla prima violazione**
  (perdita giornaliera → drawdown → limite di tempo → inattività, in ordine) o avanza fase quando il target di profitto,
  giorno di trading minimo, i requisiti di consistenza sono tutti soddisfatti. Le istantanee fuori ordine e i record su
  sfida terminale lanciano `DomainException`.
- **`RecordActivity(ActivitySnapshot, now)`** — valuta le regole di comportamento (max posizioni aperte, holding weekend,
  news trading), timbra l'attività per la regola di inattività.
- Soft **`PropFirmDrawdownWarning`** si accende una volta quando l'utilizzo dell'equity supera la soglia configurabile.

Eventi di dominio: `PropFirmChallengeStarted`, `PropFirmChallengeStopped`, `PropFirmPhasePassed`,
`PropFirmChallengePassed`, `PropFirmChallengeBreached`, `PropFirmDrawdownWarning`.

## Tracciamento live (Esecuzione) — ospitato su nodo, auto-guaribile

Il tracciamento specchia esattamente lo stack di hosting di copy-trading; prop tracker = **cugino di sola lettura** del
motore di copia.

- **`PropFirmTrackingSupervisor`** (`src/Nodes/PropFirm`) — `BackgroundService` su ogni nodo, gated su
  `App:PropFirm:Enabled`. Ogni ciclo **rivendica** le sfide attive su lease auto-guaribile
  (`AssignedNode` + `LeaseExpiresAt`; le sfide del nodo morto vengono rivendicate una volta che il lease scade —
  stessa rivendicazione atomica `ExecuteUpdate` del copy trading, quindi due nodi non tracciano mai doppiamente), rinnova i lease,
  spinge i token ruotati in posizione, ferma gli host le cui sfide hanno lasciato `Active`.
- **`PropFirmTrackingHost`** (`src/Nodes/PropFirm`) — uno per sfida. Apre `IOpenApiTradingSession`
  per account e, su `App:PropFirm:EquityPollInterval`, ricalcola l'equity live, lo fornisce all'
  aggregato. Scambia il token di accesso in posizione su rotazione (nessun rilascio della sessione). Esce quando la sfida
  non è più `Active`.
- **`PropFirmEquityCalculator`** (`src/CTraderOpenApi/Client`) — matematica dell'equity fedele a cTrader.
  L'equity **non** è consegnato da Open API, quindi derivato: `equity = bilancio + Σ(P&L non realizzato)`,
  dove il P&L di ogni posizione è `differenzaPrezzo × unità × tasso quote→deposito + swap + commissione`
  (`unità = volume wire / 100`; long rivaluta a bid, short a ask). Bilancio da
  `ProtoOATrader`; posizioni (prezzo di ingresso, swap, commissione) da riconciliazione; bid/ask live da sottoscrizioni spot.
  Puro e isolato — spot di conversione di valuta testato unitariamente per conto suo.

## Alert

`PropFirmAlertNotifier` (`src/Infrastructure/PropFirm`) si iscrive agli eventi di dominio pass/breach/warning
(registrati come `IDomainEventHandler<>`, inviati dopo `SaveChanges` riuscito), notifica l'utente
tramite alert strutturato/audit trail (`LogMessages`). L'UI live riflette lo stesso cambiamento di stato. Questo
= reazione cross-context — non mutua mai l'aggregato di sfida.

## API (`/api/prop-firm`, feature `PropFirm`, ruolo User+)

| Metodo | Rotta | Scopo |
|--------|-------|---------|
| GET | `/challenges` | elenca le sfide dell'utente (tipo, fase, stato, equity live, lease) |
| GET | `/challenges/{id}` | una sfida |
| GET | `/templates` | preset industriali per la finestra di creazione |
| POST | `/challenges` | crea da modello **o** set di regole completamente personalizzato |
| POST | `/challenges/{id}/start` | riprendi il tracciamento (Stopped → Active) |
| POST | `/challenges/{id}/stop` | ferma il tracciamento (Active → Stopped, rilascia lease) |
| POST | `/challenges/{id}/equity` | registra istantanea di equity → rivaluta (percorso manuale/no-live-feed) |
| DELETE | `/challenges/{id}` | soft-delete (bloccato mentre Active) |

MCP: `Mcp/Tools/PropFirmTools.cs` espone list/create(da modello)/record-equity/start/stop, gated su
feature `PropFirm`.

UI: `/prop-firm` (nav *Prop Firm*, gated da flag `PropFirm`) elenca le sfide con azioni di riga **Start/Stop/Delete**
(Start quando Stopped, Stop quando Active, Delete disabilitato mentre Active), le crea attraverso
`NewPropFirmChallengeDialog` (selettore modello + editor regole completo). Tutta la creazione/modifica tramite dialogo MudBlazor.

## Feed di equity live — risolto

Il precedente gap "nessun feed P&L di account live" chiuso: quando `App:PropFirm:Enabled` impostato, i nodi tracciano
l'account live su Open API, forniscono l'equity automaticamente. Senza di esso (predefinito), il dominio e il
percorso **equity manuale** (`POST …/equity`) funzionano inalterati — nessuna credenziale cTrader necessaria per build/test/E2E.

## Test

- **Unità** — `UnitTests/PropFirm/`: `PropFirmChallengeTests` (avanzamento fase, min-giorni, drawdown statico/trailing,
  perdita giornaliera, guardie terminali/fuori ordine); `PropFirmChallengeRulesTests` (base bilancio vs equity di
  perdita giornaliera, trail di soglia trailing-dollar + lock, blocco di consistenza/consenti, limite di tempo, inattività,
  max-esposizione, weekend, news, stop/resume, confine lease, passaggio rilascia lease, avviso drawdown);
  `PropFirmValueObjectTests` (intervalli VO + matematica rule-VO); `PropFirmEquityCalculatorTests` (P&L lungo/corto,
  swap/commissione, conversione quote→deposito, pricing mancante); `PropFirmTrackingHostTests` (equity live
  guida pass/fail su sessione fake estesa); `PropFirmAlertNotifierTests`. Tempo esplicito /
  `FakeTimeProvider` — nessuna lettura wall-clock.
- **Integrazione** — `IntegrationTests/`: `PropFirmChallengePersistenceTests` (round-trip + record-equity +
  soft-delete, regole arricchite + round-trip lease) e `PropFirmTrackingLeaseTests` (rivendicazione, lease contestato,
  rivendicazione dopo scadenza su due identità di nodo) su Postgres reale.
- **E2E** — `E2ETests/PropFirmTests.cs`: crea + record-equity a `Passed`; flusso stop→start→breach;
  endpoint modelli.
- **Stress / DST** — `StressTests/PropFirm/PropFirmChallengeDstTests.cs`: flussi di equity/attività randomizzati seed
  (rotoli del giorno, picchi, arresti, istantanee duplicate + fuori ordine, esposizione/weekend/news) attraverso
  molte sfide con regole miste, asserendo stati terminali esattamente una volta appiccicaticci, invariante di limite di picco-corrente,
  fallimenti ragionati.

## Configurazione (`App:PropFirm`)

`Enabled` (disattivato per impostazione predefinita), `ReconcileInterval`, `EquityPollInterval`, `LeaseTtl`,
`DrawdownWarnThresholdPercent`, `NodeName`.
