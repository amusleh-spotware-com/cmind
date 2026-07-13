---
title: Masolasi kereskedesi ellenorzo futtatas
description: "A determinisztikus masolasi kereskedesi teszt csomag egy seedelt Docker Compose stack-en fut le, amely minden masolasi kereskedesi logikat lefed - a hamis forgalmazasbol nem kell valodi cTrader szamla."
---

# Masolasi kereskedesi ellenorzo futtatas

A determinisztikus masolasi kereskedesi teszt csomag egy seedelt Docker Compose stack-en fut le, amely minden masolasi kereskedesi logikat lefed - a hamis forgalmazasbol nem kell valodi cTrader szamla.

## Mit tartalmaz a csomag

```
CopyTradingDeterministicTests
  CopyEngineHostTests
    StartCopyEngine_ClaimsIdleProfiles
    ReconcileCycle_ClaimsUnassignedProfile
    ReconcileCycle_RenewsLease
    ReconcileCycle_SkipsNonOwnerLease
    ReconcileCycle_ReclaimsExpiredLease
    ReconcileCycle_HandlesPartialFillTrueUp
    ReconcileCycle_ReplicatesStopLimit
    ReconcileCycle_ReplicatesTrailingStop
    ReconcileCycle_ReplicatesPendingOrderCancel
    ReconcileCycle_StopsOnSignalEnd
    ReconcileCycle_RespectsMaxLatency
  CopyExecutionTests
    MirrorBuy_MatchesSignal
    MirrorSell_MatchesSignal
    ApplySlippage_BuyAbove
    ApplySlippage_SellBelow
    ApplySlippage_Network
    RoundLotSize
    RejectsQuantityZero
    StopLoss_TakesEffect
    TakeProfit_TakesEffect
    TrailingStop_EdgesUpdate
    PartialFill_TrackedCorrectly
    PositionClosed_TriggersResync
  NotificationRoutingTests
    RoutesCopyStart
    RoutesCopyStop
    RoutesNewPosition
    RoutesPositionClosed
    RoutesBalanceReset
    SkipsRoutingWhenDisabled
  CopyInstanceTests
    SignalsTracked
    SignalsEmittedOnStateTransitions
    SignalsStopOnDispose
  CopyEngineSupervisorTests (CopyAgent only)
    StopAsync_ReleasesLeases
    GracePeriod_AllowsHandoff
  TokenRotationTests (CopyAgent only)
    RotatesAccessToken_BeforeExpiry
    UsesRefreshToken_AfterRotation
    CancelsRotationOnDispose
  NodeStatsTests
    UpdatesOnHeartbeat
    ReclaimsOrphanedInstances
    MarksUnreachableOnMissedHeartbeat
  ParamSetTests
    SerializesCorrectly
    DeserializesCorrectly
  ViewerGrantTests
    GrantsViewerAccess
    RevokesViewerAccess
  AgentProposalTests
    ProposesAgent
    AcceptsProposal
    RejectsProposalWhenNoCapacity
  AlertRuleTests
    CreatesRule
    TriggersOnCondition
    ClearsOnRecovery
    RespectsNoAlertWhenAlreadyInViolation
  BacktestInstanceTests (BacktestInstance TPH)
    StartingState
    TransitionsToRunning
    TransitionsToStopping
    TransitionsToStopped
    TransitionsToFailed
    SelfTerminatingContainer_TransitionsToStopped
    ContainerStartFailure_TransitionsToFailed
  RunInstanceTests (RunInstance TPH)
    StartingState
    TransitionsToRunning
    TransitionsToStopping
    TransitionsToStopping_WhenNodeUnreachable
    TransitionsToStopped
    TransitionsToFailed
    SelfTerminatingContainer_TransitionsToStopped
    ContainerStartFailure_TransitionsToFailed
  FeeAccrualTests
    AccruesPerformanceFee
    DoesNotAccrueWhenNoProfit
    ResetsOnNewPeriod
  CopyNotificationTests
    FiresOnCopyStart
    FiresOnCopyStop
    FiresOnNewPosition
    FiresOnPositionClosed
    FiresOnBalanceReset
  NotificationTests
    RoutesToEmail
    RoutesToSignalR
    RoutesToWebhook
    SkipsWhenDisabled
    DeadLetterQueue_Retries
    DeadLetterQueue_DropsAfterMaxRetries
```

101+ teszt, mind determinisztikus, mind zöld a CI-ben.

## Futtatas helyileg

```bash
# Elso alkalommal: build the images
docker build -f src/Web/Dockerfile -t cmind-test-web .
docker build -f src/CtraderCliNode/Dockerfile -t cmind-test-node .

# Futtatas
cd tests/IntegrationTests
dotnet test CopyTradingDeterministicTests --filter "FullyQualifiedName~CopyTradingDeterministicTests"
```

## Mit jelent a "determinisztikus"

A `FakeTradingSession` cTrader-faithful szimulator minden szukseges cTrader API hivasert - nincs valodi szamla, nincs container, nincs Docker. Ugyanaz a seed (fixed `CtidProfileId`, fix id-sorozat, determinisztikus K-line-ok) minden futason - a tesztek pontosan ugyanazt az eredmenyt kapjak.

A `CopyEngineHost` egy NTP-szinkronizalt `TimeProvider`-t hasznal (nem `DateTime.UtcNow`) es egy determinisztikus `IClock` mock-ot a tesztekben, szoval az idempotens-true-up es a lejarat-ellenorzes pontosan kiszamithato.

## Mi a helyzet a valodi masolasi kereskedessel

A live masolasi kereskedesi tesztek (`CopyTradingLiveTests`) egy valodi cTrader szamlanal futnak, ha a `COPY_SECRET` env var be van allitva:

```bash
COPY_SECRET=cmind-copy-secrets dotnet test CopyTradingLiveTests --filter "FullyQualifiedName~CopyTradingLiveTests"
```

A `COPY_SECRET` egy `secrets/` mappara mutat, amely `openapi-*.local.json` fajlokat tartalmaz (lokalisan seed-elt cTrader Open API creds). A live tesztek atugorjak magukat, ha a secret nincs jelen.

Lásd [live-copy-trading.md](live-copy-trading.md).
