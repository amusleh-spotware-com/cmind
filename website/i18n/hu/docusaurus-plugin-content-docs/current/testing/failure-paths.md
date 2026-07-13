---
title: Hibautak tesztelese
description: "A cMind tartalmaz explicit hibatesting Coverage-t a leggyakoribb futasi hibaokokra - kontener osszebomlasa, token lejarat, szamla lekapcsolas, hálózati szétkapcsolódás, szabály megsértés, node leallás."
---

# Hibautak tesztelese

A cMind tartalmaz explicit hibatesting Coverage-t a leggyakoribb futasi hibaokokra - kontener összeomlása, token lejárat, számla lekapcsolás, hálózati szétkapcsolódás, szabály megsértés, csomópont leállás.

## CopyEngineHost hibautak

| Teszt | Esemény | Várt viselkedés |
|-------|---------|----------------|
| `ReconcileCycle_ReclaimsExpiredLease` | Lease lejár, mert a csomópont összeomlott | Lease reclaim-elt, profil újra kiosztásra |
| `ReconcileCycle_SkipsNonOwnerLease` | Idegen csomópont lease-ét próbálja meghosszabbítani | Figyelmen kívül hagyva |
| `StopAsync_ReleasesLeases` | Graceful shutdown | Lease felszabadítva SIGTERM-en |
| `GracePeriod_AllowsHandoff` | Lease közeleg, de van túlélő | Lease átadás a túlélőnek |
| `UpdatesOnHeartbeat` | Csomópont szívverés frissítése | Stats frissül |
| `ReclaimsOrphanedInstances` | Csomópont elérhetetlen | Orphaned instance-ok Failed-ra állítva |
| `MarksUnreachableOnMissedHeartbeat` | Szívverés kimarad | Csomópont IsReachable=false |

## CopyExecution hibautak

| Teszt | Esemény | Várt viselkedés |
|-------|---------|----------------|
| `ApplySlippage_Network` | Hálózati késleltetés szimulálva | Csúszás alkalmazva a szabott értékre |
| `PartialFill_TrackedCorrectly` | Részleges kitöltés | Méret és ár helyesen követve |
| `PositionClosed_TriggersResync` | Pozíció bezárása | Resync trigger-elve |
| `MirrorBuy_MatchesSignal` | Jelsignal buy | Buy megbízás a szignalon |
| `MirrorSell_MatchesSignal` | Jelsignal sell | Sell megbízás a szignalon |

## Token hibautak

| Teszt | Esemény | Várt viselkedés |
|-------|---------|----------------|
| `RotatesAccessToken_BeforeExpiry` | Access token közeleg a lejáráshoz | Proaktív rotáció |
| `UsesRefreshToken_AfterRotation` | Access token lejárt | Refresh token használata |
| `CancelsRotationOnDispose` | Dispose rotáció közben | Rotáció megszakítva |

## Instance hibautak

| Teszt | Esemény | Várt viselkedés |
|-------|---------|----------------|
| `ContainerStartFailure_TransitionsToFailed` | Konténer indulási hiba | Instance Failed állapotba |
| `SelfTerminatingContainer_TransitionsToStopped` | Konténer önmagát leállítja | Instance Stopped állapotba |
| `TransitionsToStopping_WhenNodeUnreachable` | Csomópont elérhetetlen futás közben | Instance Stopping állapotba |

## Szabály hibautak (Prop-Firm)

| Teszt | Esemény | Várt viselkedés |
|-------|---------|----------------|
| `TriggersOnCondition` | Szabályfeltétel teljesül | Riasztás triggerelve |
| `ClearsOnRecovery` | Feltétel helyreáll | Riasztás törölve |
| `RespectsNoAlertWhenAlreadyInViolation` | Már szabályszegés alatt | Nincs duplán riasztás |

## RunInstance hibautak

- **Konténer összeomlás:** `ContainerStartFailure_TransitionsToFailed` → Instance állapot = Failed, `FailureReason` beállítva.
- **Önmagát leállító konténer:** `SelfTerminatingContainer_TransitionsToStopped` → Instance állapot = Stopped, exit code 0.
- **Csomópont elérhetetlenség futás közben:** `TransitionsToStopping_WhenNodeUnreachable` → Instance Stopping, csomópont `MarkUnreachable()` hívva.
- **Konténer ID instabilitás:** A `ContainerId` perzisztált, de a konténer indítása előtt null - a `ContainerStartFailure` kezeli.

## Hogyan futtatni

```bash
# Csak a hibautak
dotnet test CopyTradingDeterministicTests --filter "Category=FailurePaths"

# Az összes determinisztikus teszt
dotnet test CopyTradingDeterministicTests
```
