---
description: "cMind skaliert mit minimalem Operator-Aufwand. Zwei zustandsbehaftete Workloads – Run/Backtest-Ausführung, Copy-Trading – verwenden beide Datenbank als Koordinationspunkt, daher..."
---

# Horizontale Skalierung

cMind skaliert mit minimalem Operator-Aufwand. Zwei zustandsbehaftete Workloads – Run/Backtest-Ausführung, Copy-Trading – verwenden beide Datenbank als Koordinationspunkt, daher braucht das Hinzufügen von Replicas keinen externen Koordinator (kein ZooKeeper, keine Leader-Wahl).

## Copy-Trading (Self-Healing Lease)

Jeder Node läuft `CopyEngineSupervisor` (gated auf `App:Copy:Enabled`). Jeden Reconcile-Zyklus, Supervisor:

1. **Beansprucht** jedes laufende Profil unzugewiesen *oder* Lease abgelaufen, in einem atomaren `UPDATE` – zwei racing Supervisors beanspruchen nie das gleiche Profil, daher wird Profil von genau einem Node kopiert (keine doppelten Orders).
2. **Erneuert** Lease auf Profilen, die es hostet.
3. Hostet zugewiesene Profile, pusht Access-Token-Rotationen zu laufendem Host an Ort (kein Event-Stream-Drop).

Node Crash → stoppe Erneuern; einmal `App:Copy:LeaseTtl` vergeht, jeder überlebende Node reclaims seine Profile nächster Zyklus, rebuild State von Reconcile ohne Trades zu duplizieren. **Skalierung raus** = Replicas hinzufügen; Unzugewiesene/freie Profile automatisch aufgegriffen.

**Graceful Scale-In / Rolling Update (S1)** = auf `SIGTERM`, `CopyEngineSupervisor.StopAsync` **gibt diesen Node's Leases frei** (`AssignedNode`/`LeaseExpiresAt` → null), daher reclaims Überlebender sie den *sehr nächsten* Reconcile-Zyklus – **nicht** nach voller `LeaseTtl`. Nur harter Crash wartet auf den TTL. Copy-Agent's `terminationGracePeriodSeconds` (Standard 30) gibt Release-Zeit zu beenden, bevor Pod getötet.

### Knobs (`App:Copy`)

| Einstellung | Standard | Notizen |
|---------|---------|-------|
| `Enabled` | `false` | Schalte Copy-Hosting für den Node ein. |
| `ReconcileInterval` | `30s` | Wie oft Node Claims/Erneuern/Reconcile. |
| `LeaseTtl` | `120s` | Gnade, bevor stiller Node's Profile reclaimed. Behalte wenige Reconcile-Intervalle, daher langsamer Zyklus nicht verursacht spurious Handoff. |
| `NodeName` | Machine Name | Setze Unterschied, wenn zwei Supervisors einen Host teilen. |

Auf Kubernetes Copy-Supervisors Lauf als Deployment; setze `replicas` zu gewünscht Parallelismus. Jeder Pod bekommt stabil `NodeName` (Standard: Pod-Hostname), daher Leases attributiert pro Pod. Datenbank ist einzelne Quelle der Wahrheit – keine sticky Sessions, kein Pro-Pod-State zu migrieren.

**Balanced Distribution (S4):** setze `App:Copy:MaxProfilesPerNode` > 0, um zu cappen, wie viele laufende Profile ein Node hostet. Jeder Supervisor beansprucht dann **höchstens** sein verbleibendes Headroom über atomaren `FOR UPDATE SKIP LOCKED`-bounded Claim, daher spreaden Profile **über Replicas** statt erster Supervisor, der alles grabbt – keine einzelne heiße Pod / SPOF. Skip-Locked Claim behält "genau ein Node pro Profil"-Garantie (keine Doppel-Hosting) sogar unter concurrent Claims. `0` (Standard) = unbounded (ein Node hostet alles, Unverändert).

**Bei Skala (S7/S8):** jeder Pod jittered Reconcile bis zu 20% von `ReconcileInterval` (`CopyEngineSupervisor.JitteredInterval`), daher N Replicas nicht fire Claim/Renew `UPDATE` gleichzeitig (Postgres Thundering-Herd). Wenn `copyAgent.replicas > 1` Chart auch spreads Replicas über Nodes (`topologySpreadConstraints`) und adds `PodDisruptionBudget` (`minAvailable: 1`), daher Drain/Upgrade niemals nimmt Copy-Kapazität zu Null.

## Run/Backtest-Ausführung

`NodeScheduler` wählt am wenigsten geladen eligib Node ehrend `MaxInstances`; Remote-Node-Agents registrieren sich selbst und machen Heartbeat (`App:Discovery`), `NodeHeartbeatMonitor` markiert Node unerreichbar, wenn Heartbeat `Discovery:HeartbeatTtl` übersteigt. Addieren Node-Agents zu Ausführungs-Kapazität hinzufügen; Toter Agent automatisch umgangen.

## Migrationen auf Scale-Out / Rolling Deploy

Jede Web/MCP-Replica läuft `OwnerSeeder` beim Start, die EF-Migrationen anwendet und den Owner seedet. Um das sicher zu machen, wenn N Replicas gleichzeitig starten, Migrate + Seed laufen innerhalb eines **Postgres Session Advisory Lock** (`MigrationLock.RunExclusiveAsync`, Schlüssel `DatabaseDefaults.MigrationAdvisoryLockKey`): die erste Replica zum Erwerben es migriert und seedet; die Rest blocken auf dem Lock, dann finden Migrationen bereits angewendet (No-Op) und Owner bereits vorhanden. Nein separate Migration Job oder Leader-Wahl braucht. Wenn du Erste-Run-Seeding hinzufügst, setzte es **innerhalb** des gleichen bewachten Blocks, daher ist es Single-Writer.

## Node-Agent HTTP-Resilienz

Der Haupt-Node spricht mit jedem `CtraderCliNode`-Agent über HTTP durch drei Zweck-Split-Clients, daher ein fehlerhaft Node oder Netzwerk nie corrupts State:

- **Read** (`status` / `report` / `stats`) – idempotent GETs, retried auf transiente Fehler (exponential Backoff + Jitter, `NodeAgentHttp.ReadRetryCount`) mit Pro-Attempt und Total Timeouts.
- **Write** (`start` / `stop` / `clean`) – nicht-idempotent POSTs, timed out aber **never retried**: ein retried `start` könnte Double-Launch eines Containers.
- **Stream** (`logs`) – der langlebige `docker logs -f`-Stream bekommt einen unendlichen Timeout und keine Resilienz-Pipeline, daher Tailing wird never geschnitten.

Ein Node, der unerreichbar bleibt, wird durch Heartbeat + [Verwaiste-Instance Reclaim](../operations/node-discovery.md) behandelt; die HTTP-Schicht smooths nur transiente Blips.

## Zustandslose Ebenen

Web (Blazor Server + API) und MCP-Server sind zustandslos hinter Datenbank, replicate frei. Auth ist Cookie-basiert; Skaliere Web horizontal hinter Load Balancer. MCP-Server ist separater Prozess/Deployment, daher skaliert unabhängig von Web.

## Datenbank-Verbindungs-Resilienz

Jeder Host, der die Datenbank öffnet, verwendet eine **Wiederversuch-Ausführungs-Strategie**, daher ein transienter Disconnect oder ein verwaltete-Postgres-Failover (RDS / Flexible Server Patching) wird retried, statt als Fehler für den Benutzer zu surfacen:

- Web und MCP registrieren den Context über die Aspire Npgsql-Komponente mit `DisableRetry=false` und einem expliziten `CommandTimeout` (`DatabaseDefaults.CommandTimeoutSeconds`).
- CopyAgent (Nicht-Aspire) registriert über `UseAppNpgsql`, die appliziert das gleiche `EnableRetryOnFailure(MaxRetryCount, MaxRetryDelay)` + Command-Timeout von `DatabaseDefaults`.

Alle Writes sind einzeln `SaveChanges` / einzeln `ExecuteUpdate` / einzeln `ExecuteSql`-Statements, daher ist die Wiederversuch-Strategie sicher (keine Multi-Statement-Transaktion braucht manuelles `strategy.ExecuteAsync`-Wrapping). Wenn du ein manuelles Transaction oder mehrfach `SaveChanges` in einer logischen Operation hinzufügst, wrappe es in `db.Database.CreateExecutionStrategy().ExecuteAsync(...)` – sonst wirft es unter Retry.

## Checkliste für Scale-Out

- [ ] Postgres größt für hinzugefügte Verbindungs-Last (jede Web/MCP/Node-Replica öffnet einen Pool).
- [ ] `App:Copy:Enabled=true` auf jedem Node, der Copy-Profile hosten sollte.
- [ ] Unterschied `App:Copy:NodeName` pro Co-Located Supervisor (K8s: Standard Pro-Pod fine).
- [ ] `LeaseTtl` ≥ 3× `ReconcileInterval`.
- [ ] Node-Agents bereitgestellt, wo privilegiertes Docker verfügbar (AKS/EKS/EC2/VM, nicht Fargate).
- [ ] Multi-Replica Web: setze die `signalr`-Verbindungsstring (Redis Backplane) **und** aktiviere Ingress-Session-Affinität (Sticky Sessions), daher Blazor-Circuit reconnects zu Live-Pod. Eine Component-Exception wird von der `MainLayout` `ErrorBoundary` gefangen (Freundlich Retry, Circuit bleibt lebendig).
