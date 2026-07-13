---
description: "cMind scale-out avec un effort opérateur minimal. Deux workloads avec état — exécution run/backtest, copy-trading — utilisent tous deux la base de données comme point de coordination, donc…"
---

# Mise à l'échelle horizontale

cMind scale-out avec un effort opérateur minimal. Deux workloads avec état — exécution run/backtest, copy-trading — utilisent tous deux la base de données comme point de coordination, donc ajouter des réplicas ne nécessite aucun coordinateur externe (pas de ZooKeeper, pas d'élection de leader).

## Copy-trading (bail auto-réparant)

Chaque nœud exécute `CopyEngineSupervisor` (controlé par `App:Copy:Enabled`). Chaque cycle de réconciliation, le supervisor :

1. **Réclame** chaque profil en cours non assigné *ou* dont le bail a expiré, dans un `UPDATE` atomique —
   deux supervisors en course ne réclament jamais le même profil, donc le profil est copié par exactement un
   nœud (pas de doubles ordres).
2. **Renouvèle** le bail sur les profils qu'il héberge.
3. Héberge les profils assignés, pousse les rotations de token d'accès vers le host en cours en place (pas
   de drop du flux d'événements).

Nœud en panne → cesse de renouveler ; une fois `App:Copy:LeaseTtl` passé, n'importe quel nœud survivant réclame
ses profils au prochain cycle, reconstitue l'état depuis la réconciliation sans dupliquer les trades. **Scale-out**
= ajouter des réplicas ; les profils libres/non assignés sontpickés automatiquement.

**Scale-in gracieux / mise à jour rolling (S1)** = sur `SIGTERM`, `CopyEngineSupervisor.StopAsync`
**libère les baux de ce nœud** (`AssignedNode`/`LeaseExpiresAt` → null) ainsi le survivant les réclame
à son **très prochain** cycle de réconciliation — **pas** après le `LeaseTtl` complet. Seul un crash dur attend le TTL.
`terminationGracePeriodSeconds` du copy-agent (défaut 30) donne le temps de libération avant que le pod soit tué.

### Boutons (`App:Copy`)

| Paramètre | Défaut | Notes |
|---------|--------|-------|
| `Enabled` | `false` | Active l'hébergement copy pour le nœud. |
| `ReconcileInterval` | `30s` | Fréquence de réclamation/renouvellement/réconciliation du nœud. |
| `LeaseTtl` | `120s` | Délai avant réclamation silencieuse des profils d'un nœud en panne. Garder quelques intervalles de réconciliation pour qu'un cycle lent ne cause pas de transfert indu. |
| `NodeName` | nom machine | Définir distinctement quand deux supervisors partagent un host. |

Sur Kubernetes les copy supervisors s'exécutent en Deployment ; définissez `replicas` à la parallélisation désirée. Chaque
pod obtient un `NodeName` stable (défaut : hostname pod), ainsi les baux sont attribués par pod. La base de données est
la seule source de vérité — pas de sessions collantes, pas d'état par pod à migrer.

**Distribution équilibrée (S4) :** définissez `App:Copy:MaxProfilesPerNode` > 0 pour limiter le nombre de profils
en cours qu'un nœud héberge. Chaque supervisor réclame alors **au plus** sa capacité résiduelle via une réclamation
atomique `FOR UPDATE SKIP LOCKED`, ainsi les profils se **répartissent** à travers les réplicas au lieu du premier
supervisor qui saisit tout — pas de pod hot / SPOF unique. La réclamation skip-locked garde la garantie "exactement
un nœud par profil" (pas de double-hébergement) même sous réclamations concurrentes. `0` (défaut) = non borné
(un nœud héberge tout, inchangé).

**À l'échelle (S7/S8) :** chaque pod jitter la réconciliation jusqu'à 20% de `ReconcileInterval`
(`CopyEngineSupervisor.JitteredInterval`) ainsi N réplicas ne déclenchent pas simultanément les `UPDATE`
de réclamation/renouvellement (thunderinging-herd Postgres). Quand `copyAgent.replicas > 1` le chart répartit aussi
les réplicas entre nœuds (`topologySpreadConstraints`) et ajoute un `PodDisruptionBudget` (`minAvailable: 1`)
ainsi drain/upgrade ne prend jamais la capacité copy à zéro.

## Exécution run/backtest

`NodeScheduler` choisit le nœud éligible le moins chargé en honorant `MaxInstances` ; les agents de nœud distants
s'auto-enregistrent et envoient des heartbeats (`App:Discovery`), `NodeHeartbeatMonitor` marque le nœud inaccessible
quand le heartbeat dépasse `Discovery:HeartbeatTtl`. Ajouter des agents de nœud ajoute de la capacité d'exécution ;
un agent mort est contourné automatiquement.

## Migrations sur scale-out / déploiement rolling

Chaque réplica Web/MCP exécute `OwnerSeeder` au démarrage, qui applique les migrations EF et seed le propriétaire.
Pour rendre cela sûr quand N réplicas démarrent à la fois, la migration + seed s'exécutent à l'intérieur d'un
**verrou advisory de session Postgres** (`MigrationLock.RunExclusiveAsync`, clé `DatabaseDefaults.MigrationAdvisoryLockKey`) :
le premier réplica à l'acquérir migre et seed ; les autres se bloquent sur le verrou, puis trouvent les migrations
déjà appliquées (no-op) et le propriétaire déjà présent. Aucun job de migration séparé ni election de leader n'est
nécessaire. Si vous ajoutez du seed au premier-run, mettez-le **à l'intérieur** du même bloc gardé ainsi c'est
single-writer.

## Résilience HTTP des agents de nœud

Le nœud principal parle à chaque agent `CtraderCliNode` sur HTTP à travers trois clients à purpose-split ainsi un
nœud ou réseau flaky ne corrompt jamais l'état :

- **lecture** (`status` / `report` / `stats`) — GET idempotents, réessayés sur échecs transitoires
  (backoff exponentiel + jitter, `NodeAgentHttp.ReadRetryCount`) avec timeout par tentative et total.
- **écriture** (`start` / `stop` / `clean`) — POST non-idempotents, timeout mais **jamais réessayés** : un
  `start` réessayé pourrait lancer un container en double.
- **stream** (`logs`) — le flux long `docker logs -f` obtient un timeout infini et aucun pipeline de résilience,
  ainsi le tail n'est jamais coupé.

Un nœud qui reste inaccessible est géré par heartbeat + [réclamation d'instance orpheline](../operations/node-discovery.md) ;
la couche HTTP ne fait que lisser les à-coups transitoires.

## Tiers sans état

Web (Blazor Server + API) et serveur MCP sont sans état derrière la base de données, se répliquent librement.
L'auth est basée sur les cookies ; scale Web horizontalement derrière le load balancer. Le serveur MCP est un
processus/Déploiement séparé ainsi il scale indépendamment de Web.

## Résilience de connexion à la base de données

Chaque host qui ouvre la base de données utilise une **stratégie d'exécution avec retry** ainsi une
déconnexion transitoire ou un failover PostgreSQL géré (patching RDS / Flexible Server) est réessayé au lieu
de remonter comme erreur à l'utilisateur :

- Web et MCP enregistrent le contexte via le composant Npgsql Aspire avec `DisableRetry=false`
  et un `CommandTimeout` explicite (`DatabaseDefaults.CommandTimeoutSeconds`).
- CopyAgent (non-Aspire) enregistre via `UseAppNpgsql`, qui applique le même
  `EnableRetryOnFailure(MaxRetryCount, MaxRetryDelay)` + timeout de commande depuis `DatabaseDefaults`.

Tous les writes sont des instructions `SaveChanges` / `ExecuteUpdate` / `ExecuteSql` uniques, ainsi la stratégie
de retry est sûre (pas de transaction multi-instructions nécessitant un wrapper `strategy.ExecuteAsync`
manuel — sinon ça throw sous retry). Si vous ajoutez une transaction manuelle ou plusieurs `SaveChanges` dans
une opération logique, wrappez-la dans `db.Database.CreateExecutionStrategy().ExecuteAsync(...)` — sinon
ça throw sous retry.

## Checklist pour le scale-out

- [ ] PostgreSQL dimensionné pour la charge de connexion ajoutée (chaque réplica Web/MCP/nœud ouvre un pool).
- [ ] `App:Copy:Enabled=true` sur chaque nœud qui doit héberger des profils copy.
- [ ] `App:Copy:NodeName` distinct par supervisor colocalisé (K8s : défaut par pod OK).
- [ ] `LeaseTtl` ≥ 3× `ReconcileInterval`.
- [ ] Agents de nœud déployés là où Docker privilégié est disponible (AKS/EKS/EC2/VM, pas Fargate).
- [ ] Multi-réplica Web : définissez la chaîne de connexion `signalr` (backplane Redis) **et** activez
      l'affinité de session ingress (sessions collantes) ainsi un circuit Blazor se reconnecte à un pod live.
      Une exception de composant est catchée par le `ErrorBoundary` du `MainLayout` (retry friendly, circuit
      reste alive).
