---
description: "cMind skaliert mit minimalem Operator-Aufwand nach außen. Zwei zustandsbehaftete Workloads – Run/Backtest-Ausführung, Copy-Trading – verwenden beide die Datenbank als Koordinationspunkt, sodass…"
---

# Horizontale Skalierung

cMind skaliert mit minimalem Operator-Aufwand nach außen. Zwei zustandsbehaftete Workloads –
Run/Backtest-Ausführung, Copy-Trading – verwenden beide die Datenbank als Koordinationspunkt, sodass
das Hinzufügen von Replicas keinen externen Koordinator erfordert (kein ZooKeeper, keine
Leader-Wahl).

## Copy-Trading (selbstheilender Lease)

Jeder Node führt `CopyEngineSupervisor` aus (gesteuert durch `App:Copy:Enabled`). In jedem
Abgleichzyklus:

1. **Beansprucht** der Supervisor jedes laufende Profil, das nicht zugewiesen *oder* dessen Lease
   abgelaufen ist, in einer atomaren `UPDATE` — zwei konkurrierende Supervisoren beanspruchen nie
   dasselbe Profil, sodass das Profil von genau einem Node kopiert wird (keine Doppelbestellungen).
2. **Erneuert** den Lease auf Profilen, die er hostet.
3. Hostet zugewiesene Profile, pusht Access-Token-Rotationen in den laufenden Host in-place (kein
   Event-Stream-Drop).

Node-Absturz → hört auf zu erneuern; sobald `App:Copy:LeaseTtl` vergeht, beansprucht ein
überlebender Node seine Profile im nächsten Zyklus, baut den Zustand aus dem Abgleich wieder auf, ohne
Trades zu duplizieren. **Horizontales Skalieren** = Replicas hinzufügen; nicht zugewiesene/freie
Profile werden automatisch aufgenommen.

**Graceful Scale-In / Rolling Update (S1)** = bei `SIGTERM` gibt
`CopyEngineSupervisor.StopAsync` **die Leases dieses Nodes frei**
(`AssignedNode`/`LeaseExpiresAt` → null), sodass der Überlebende sie **sehr nächsten**
Abgleichzyklus beansprucht — **nicht** nach dem vollen `LeaseTtl`. Nur ein Hard-Crash wartet das TTL.
Die `terminationGracePeriodSeconds` des Copy-Agents (Standard 30) gibt der Freigabezeit, um zu
finishen, bevor der Pod gekillt wird.

### Knobs (`App:Copy`)

| Einstellung | Standard | Anmerkungen |
|---------|---------|---------|
| `Enabled` | `false` | Copy-Hosting für den Node einschalten. |
| `ReconcileInterval` | `30s` | Wie oft der Node beansprucht/erneuert/abgleicht. |
| `LeaseTtl` | `120s` | Gnade, bevor stille Node-Profile zurückgefordert werden. Mehrere Abgleichintervalle halten, damit ein langsamer Zyklus keine falsche Übergabe verursacht. |
| `NodeName` | Maschinenname | Distinct setzen, wenn zwei Supervisoren sich einen Host teilen. |

Auf Kubernetes laufen Copy-Supervisoren als Deployment; setzen Sie `replicas` auf gewünschte
Parallelität. Jeder Pod bekommt einen stabilen `NodeName` (Standard: Pod-Hostname), sodass Leases
pro Pod zugeschrieben werden. Die Datenbank ist einzige Quelle der Wahrheit — keine Sticky
Sessions, kein pro-Pod-Zustand zu migrieren.

**Ausgewogene Verteilung (S4):** setzen Sie `App:Copy:MaxProfilesPerNode` > 0, um zu begrenzen,
wie viele laufende Profile ein Node hostet. Jeder Supervisor beansprucht dann **höchstens** seine
verbleibende Kapazität über atomare `FOR UPDATE SKIP LOCKED`-beanspruchte Beanspruchung, sodass Profile
sich **verteilen** statt dass der erste Supervisor alle schnappt — kein einzelner heißer Pod / SPOF.
Skip-locked beansprucht behält die „genau ein Node pro Profil"-Garantie (keine Doppel-Hostung) selbst
unter gleichzeitigen Beanspruchungen. `0` (Standard) = unbegrenzt (ein Node hostet alles,
unverändert).

**Bei Skalierung (S7/S8):** jeder Pod jittert den Abgleich um bis zu 20% von
`ReconcileInterval` (`CopyEngineSupervisor.JitteredInterval`), sodass N Replicas nicht gleichzeitig
Claim/Renew-`UPDATE` feuern (Postgres Thundering-Herds). Wenn `copyAgent.replicas > 1`, verteilt das
Chart auch Replicas über Nodes (`topologySpreadConstraints`) und fügt `PodDisruptionBudget`
(`minAvailable: 1`) hinzu, sodass Drain/Upgrade nie die Copy-Kapazität auf Null bringt.

## Run/Backtest-Ausführung

`NodeScheduler` wählt den am wenigsten ausgelasteten geeigneten Node und ehrt `MaxInstances`; Remote
Node-Agenten registrieren sich selbst und senden Heartbeat (`App:Discovery`),
`NodeHeartbeatMonitor` markiert Node als unerreichbar, wenn Heartbeat `Discovery:HeartbeatTtl`
überschreitet. Node-Agenten hinzufügen, um Ausführungskapazität hinzuzufügen; ein toter Agent wird
automatisch umgeleitet.

## Migrationen bei Scale-Out / Rolling Deploy

Jede Web/MCP-Replica führt beim Startup `OwnerSeeder` aus, das EF-Migrationen anwendet und den
Eigentümer seeded. Um das sicher zu machen, wenn N Replicas gleichzeitig starten, laufen Migrate +
Seed innerhalb eines **Postgres Session Advisory Lock**
(`MigrationLock.RunExclusiveAsync`, Key `DatabaseDefaults.MigrationAdvisoryLockKey`): die erste
Replica, die ihn acquiriert, migriert und seeded; die anderen blockieren auf dem Lock, finden dann
aber bereits angewendete Migrationen (No-Op) und den bereits vorhandenen Eigentümer. Kein separater
Migration-Job oder Leader-Wahl nötig. Wenn Sie ein First-Run-Seeding hinzufügen, setzen Sie es
**innerhalb** desselben bewachten Blocks, damit es Single-Writer ist.

## Node-Agent-HTTP-Resilienz

Der Haupt-Node spricht mit jedem `CtraderCliNode`-Agenten über HTTP durch drei zweckgetrennte
Clients, sodass ein wackeliger Node oder Netzwerk nie Zustand korrumpiert:

- **Read** (`status` / `report` / `stats`) — idempotente GETs, wiederholt bei vorübergehenden
  Fehlern (exponentielles Backoff + Jitter, `NodeAgentHttp.ReadRetryCount`) mit Per-Attempt- und
  Total-Timeouts.
- **Write** (`start` / `stop` / `clean`) — nicht-idempotente POSTs, mit Timeout, aber **nie
  wiederholt**: ein wiederholtes `start` könnte einen Container doppelt starten.
- **Stream** (`logs`) — der langlebige `docker logs -f`-Stream bekommt ein unendliches Timeout
  und keine Resilienz-Pipeline, sodass das Tailen nie abgeschnitten wird.

Ein Node, der unerreichbar bleibt, wird durch Heartbeat + [verwaiste Instanz-Rückforderung](../operations/node-discovery.md)
behandelt; die HTTP-Schicht glättet nur vorübergehende Aussetzer.

## Stateless Tiers

Web (Blazor Server + API) und MCP-Server sind stateless hinter der Datenbank, replizieren frei.
Auth ist Cookie-basiert; Web horizontal hinter Load Balancer skalieren. MCP-Server ist ein
separater Prozess/Deployment, sodass er unabhängig von Web skaliert.

## Datenbankverbindungs-Resilienz

Jeder Host, der die Datenbank öffnet, verwendet eine **wiederholende Ausführungsstrategie**, sodass
eine vorübergehende Trennung oder ein Managed-Postgres-Failover (RDS / Flexible Server Patching)
wiederholt statt als Fehler an den Benutzer zurückgegeben wird:

- Web und MCP registrieren den Kontext durch die Aspire Npgsql-Komponente mit `DisableRetry=false`
  und einem expliziten `CommandTimeout` (`DatabaseDefaults.CommandTimeoutSeconds`).
- CopyAgent (non-Aspire) registriert sich über `UseAppNpgsql`, das dieselbe
  `EnableRetryOnFailure(MaxRetryCount, MaxRetryDelay)` + Command-Timeout von `DatabaseDefaults`
  anwendet.

Alle Writes sind einzelne `SaveChanges` / einzelne `ExecuteUpdate` / einzelne `ExecuteSql`-Statements,
sodass die Wiederholungsstrategie sicher ist (kein Multi-Statement-Transaction braucht manuelles
`strategy.ExecuteAsync`-Wrapping). Wenn Sie eine manuelle Transaktion oder mehrere `SaveChanges` in
einer logischen Operation hinzufügen, wrappen Sie es in
`db.Database.CreateExecutionStrategy().ExecuteAsync(...)` — sonst wirft es unter Retry.

## Checkliste für horizontale Skalierung

- [ ] Postgres für zusätzliche Verbindungs-last dimensionieren (jede Web/MCP/Node-Replica öffnet
  einen Pool).
- [ ] `App:Copy:Enabled=true` auf jedem Node, der Copy-Profile hosten soll.
- [ ] Distinct `App:Copy:NodeName` pro ko-lokalisierter Supervisor (K8s: Standard per-Pod fine).
- [ ] `LeaseTtl` ≥ 3× `ReconcileInterval`.
- [ ] Node-Agenten dort deployen, wo privilegierter Docker verfügbar (AKS/EKS/EC2/VM, nicht
  Fargate).
- [ ] Multi-Replica-Web: die `signalr`-Verbindungszeichenfolge (Redis Backplane) setzen **und**
  Ingress-Session-Affinity (Sticky Sessions) aktivieren, damit ein Blazor-Circuit auf einem
  Live-Pod wiederhergestellt wird. Eine Komponenten-Ausnahme wird vom `MainLayout`
  `ErrorBoundary` aufgefangen (freundliche Wiederholung, Circuit bleibt am Leben).
