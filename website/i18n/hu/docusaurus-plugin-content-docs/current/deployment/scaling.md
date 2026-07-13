---
description: "A cMind kívülről skálázódik minimális operátori erőfeszítéssel. Két állapot-gyakorló munkaterhelés — run/backtest execution, copy-trading — mindkettő adatbázist használ koordinációs pontként, így a replikák hozzáadása nem igényel külső koordinátort (nincs ZooKeeper, nincs leader election)."
---

# Horizontális skálázás

A cMind kívülről skálázódik minimális operátori erőfeszítéssel. Két állapot-gyakorló munkaterhelés — run/backtest execution, copy-trading — mindkettő adatbázist használ koordinációs pontként, így a replikák hozzáadása nem igényel külső koordinátort (nincs ZooKeeper, nincs leader election).

## Copy-trading (ön-gyógyító lease)

Minden node a `CopyEngineSupervisor`-t futtatja (`App:Copy:Enabled`-re kapuzva). Minden reconcile ciklusban a supervisor:

1. **Claim-el** minden futó profilt, amely nincs hozzárendelve *vagy* lease-lejárt, egy atomi `UPDATE`-ben — két versengő supervisor soha nem claim-eli ugyanazt a profilt, így a profil pontosan egy node által másolódik (nincs duplán order).
2. **Megújítja** a lease-t az általa hostolt profilokon.
3. Hostolja a hozzárendelt profilokat, push-olja az access-token rotációkat a futó host-ba a helyükön (nincs event-stream drop).

Node crash → megáll a megújítás; amint az `App:Copy:LeaseTtl` elmúlik, bármely túlélő node reclaim-el a profiljai következő ciklusban, újraépíti az állapotot a reconcile-ből duplikált kereskedések nélkül. **Skálázás kifelé** = replikák hozzáadása; nem hozzárendelt/szabad profilok automatikusan felvételre kerülnek.

**Graceful scale-in / rolling update (S1)** = `SIGTERM`-re a `CopyEngineSupervisor.StopAsync` **felszabadítja a node lease-eit** (`AssignedNode`/`LeaseExpiresAt` → null), így a túlélő a *nagyon következő* reconcile ciklusában reclaim-el — **nem** a teljes `LeaseTtl` után. Csak hard crash vár a TTL-re. A copy-agent `terminationGracePeriodSeconds` (alapértelmezés 30) időt ad a felszabadítás befejezésére, mielőtt a pod megöletik.

### Knob-ok (`App:Copy`)

| Beállítás | Alapértelmezés | Megjegyzések |
|---------|---------|---|
| `Enabled` | `false` | Kapcsold be a copy hosting-ot a node-on. |
| `ReconcileInterval` | `30s` | Milyen gyakran claim-el/megújít/reconcile-ol a node. |
| `LeaseTtl` | `120s` | Türelem, mielőtt egy csendes node profiljai reclaim-eltek. Tartsd néhány reconcile intervallumra, így a lassú ciklus nem okoz spurious hand-off-ot. |
| `NodeName` | machine name | Állítsd különbözőre, amikor két supervisor osztozik egy host-on. |

A Kubernetes-en a copy supervisor-ok Deployment-ként futnak; állítsd a `replica`-t a kívánt párhuzamosságra. Minden pod stabil `NodeName`-et kap (alapértelmezés: pod hostname), így a lease-ek pod-onként attributáltak. Az adatbázis az egyetlen forrása az igazságnak — nincs sticky sessions, nincs per-pod állapot a migrálásra.

**Kiegyensúlyozott eloszlás (S4):** állítsd az `App:Copy:MaxProfilesPerNode`-ot > 0-ra, hogy korlátozd, hány futó profilt hostol egy node. Minden supervisor then claim-el **legfeljebb** a fennmaradó headroom-ot atomikus `FOR UPDATE SKIP LOCKED` bounded claim-en keresztül, így a profilok **terjednek** a replikák között ahelyett, hogy az első supervisor mindent elkapna — nincs egyetlen hot pod / SPOF. A skip-locked claim megtartja az "exact egy node per profil" garanciát (nincs double-hosting) még egyidejű claim-ek alatt is. `0` (alapértelmezés) = korlátlan (egy node hostol mindent, változatlan).

**At scale (S7/S8):** minden pod jitter-eli a reconcile-t akár 20%-kal a `ReconcileInterval`-ből (`CopyEngineSupervisor.JitteredInterval`), így N replika nem egyszerre lői el a claim/renew `UPDATE`-et (Postgres thundering-herd). Amikor `copyAgent.replicas > 1`, a chart is spread-eli a replikákat a node-ok között (`topologySpreadConstraints`) és hozzáad egy `PodDisruptionBudget`-et (`minAvailable: 1`), így a drain/upgrade soha nem viszi a copy kapacitást nullára.

## Run/backtest execution

A `NodeScheduler` választja a legkevésbé terhelt jogosult node-ot, tiszteletben tartva a `MaxInstances`-et; a távoli node agent-ek ön-regisztrálják és heartbeat-et küldenek (`App:Discovery`), a `NodeHeartbeatMonitor` jelöli a node-ot elérhetetlennek, amikor a heartbeat meghaladja a `Discovery:HeartbeatTtl`-t. Add node agent-eket a végrehajtási kapacitás hozzáadásához; a halott agent automatikusan megkerülhető.

## Migrációk skálázáskor / rolling deploy

Minden Web/MCP replika az `OwnerSeeder`-t futtatja indításkor, amely alkalmazza az EF migrációkat és seed-eli a tulajdonost. Hogy ezt biztonságossá tegye, amikor N replika egyszerre indul, a migrate + seed egy **Postgres session advisory lock** belül fut (`MigrationLock.RunExclusiveAsync`, kulcs `DatabaseDefaults.MigrationAdvisoryLockKey`): az első replika, amely megszerzi, migrál és seed-el; a többiek block-olnak a lock-on, majd megtalálják, hogy a migrációk már alkalmazva vannak (no-op) és a tulajdonos már jelen van. Nincs szükség külön migrációs job-ra vagy leader election-re. Ha hozzáadsz first-run seeding-et, put it **inside** the same guarded block so it is single-writer.

## Node-agent HTTP rugalmasság

A main node minden `CtraderCliNode` agent-tel HTTP-n keresztül beszél, három cél-szétválasztott kliensen keresztül, így egy zakkant node vagy hálózat soha nem korruptál állapotot:

- **olvasás** (`status` / `report` / `stats`) — idempotent GET-ek, újrapróbálva átmeneti hibákon (exponenciális backoff + jitter, `NodeAgentHttp.ReadRetryCount`) per-attempt és összes timeouts-okkal.
- **írás** (`start` / `stop` / `clean`) — nem-idempotent POST-ok, timeout-olt de **soha nem újrapróbálva**: egy retry-olt `start` duplán-indíthatna egy containert.
- **stream** (`logs`) — a hosszan élő `docker logs -f` stream végtelen timeout-ot kap és nincs rugalmassági pipeline, így a tailing soha nem lesz levágva.

Egy node, amely elérhetetlen marad, a heartbeat + [orphaned instance reclaim](../operations/node-discovery.md) által van kezelve; a HTTP réteg csak az átmeneti blip-eket simítja.

## Stateless tier-ek

A Web (Blazor Server + API) és az MCP server stateless az adatbázis mögött, szabadon replikálódik. Az auth cookie-alapú; skálázold a Web-et horizontálisan a load balancer mögött. Az MCP server külön process/Deployment, így a Web-től függetlenül skálázódik.

## Adatbázis kapcsolat rugalmasság

Minden host, amely megnyitja az adatbázist, egy **retrying execution strategy-t** használ, így egy átmeneti disconnect vagy egy managed-Postgres failover (RDS / Flexible Server patching) újrapróbálásra kerül, ahelyett, hogy hibaként kerülne a felhasználó elé:

- A Web és az MCP a kontextust az Aspire Npgsql komponensen keresztül regisztrálja `DisableRetry=false` és egy explicit `CommandTimeout` (`DatabaseDefaults.CommandTimeoutSeconds`) mellett.
- A CopyAgent (nem-Aspire) a `UseAppNpgsql`-en keresztül regisztrál, amely ugyanazt a `EnableRetryOnFailure(MaxRetryCount, MaxRetryDelay)` + command timeout-ot alkalmazza a `DatabaseDefaults`-ból.

Minden írás egyetlen `SaveChanges` / egyetlen `ExecuteUpdate` / egyetlen `ExecuteSql` statement, így a retrying strategy biztonságos (nincs multi-statement transaction, amelynek kézzel `strategy.ExecuteAsync`-t kellene wrappelni). Ha hozzáadsz egy manuális transaction-t vagy több `SaveChanges`-t egy logikai műveletben, wrappeld `db.Database.CreateExecutionStrategy().ExecuteAsync(...)`-ba — különben dob, ha retry alatt áll.

## Checklist a skálázáshoz

- [ ] Postgres méretezve a hozzáadott kapcsolati terhelésre (minden Web/MCP/node replika egy pool-t nyit).
- [ ] `App:Copy:Enabled=true` minden node-on, amely copy profilokat kell hogy hostoljon.
- [ ] Különböző `App:Copy:NodeName` per co-located supervisor (K8s: alapértelmezés per-pod oké).
- [ ] `LeaseTtl` ≥ 3× `ReconcileInterval`.
- [ ] Node agent-ek telepítve, ahol privilégizált Docker elérhető (AKS/EKS/EC2/VM, nem Fargate).
- [ ] Multi-replica Web: állítsd be a `signalr` connection string-et (Redis backplane) **és** engedélyezd az ingress session affinity-t (sticky sessions), így egy Blazor circuit egy élő pod-hoz reconnect-el. Egy komponens kivételt a `MainLayout` `ErrorBoundary` catches (barátságos retry, a circuit élve marad).
