---
description: "Les nœuds cTrader CLI rejoignent le cluster par auto-registration + battement de cœur — pas d'entrée manuelle. Le même modèle que les agents Consul/Nomad/kubeadm : l'agent démarre en connaissant la localisation du nœud principal…"
---

# Auto-découverte des nœuds

Les nœuds cTrader CLI rejoignent le cluster par **auto-registration + battement de cœur** — pas d'entrée manuelle. Le même modèle que les agents Consul/Nomad/kubeadm : l'agent démarre en connaissant la localisation du nœud principal + secret de cluster partagé, puis s'annonce continuellement.

> Vérifiée de bout en bout sur Docker Compose et cluster `kind` Kubernetes : les agents s'auto-enregistrent, apparaissent dans la DB injoignables, automatiquement marqués injoignables quand les battements de cœur s'arrêtent au-delà du TTL, reviennent en ligne quand ils reprennent.

## Comment ça marche

```
Agent CtraderCliNode                         Principal (Web)
------------------                         ----------
POST /api/nodes/register  ── join token ──▶ vérifier le jeton (temps constant)
  { name, baseUrl, mode,                    vérifier la version du protocole
    maxInstances, dataDir,                   upsert CtraderCliNode par nom
    protocolVersion }                        estampille LastHeartbeatAt, IsReachable=true
        ▲                                     └─ CtraderCliNode.SelfRegister / RecordHeartbeat
        │  tous les HeartbeatInterval        NodeHeartbeatMonitor (arrière-plan) :
        └──────────────────────────────────── si maintenant - LastHeartbeatAt > HeartbeatTtl
                                                 → CtraderCliNode.MarkUnreachable() (NodeWentOffline)
```

- **Registration == battement de cœur.** L'agent re-POST sur `HeartbeatIntervalSeconds`. Le premier appel crée un nœud (`NodeRegistered` événement) ; les appels ultérieurs actualisent la vivacité. Le battement de cœur repris après une panne retourne le nœud injoignable (`NodeCameOnline`).
- **Réconciliation de vivacité.** `NodeHeartbeatMonitor` marque les nœuds dont le dernier battement de cœur dépasse `HeartbeatTtl` injoignables. Le planificateur (`IsActive`/`AcceptsRun`/`AcceptsBacktest` gated sur l'injoignabilité) arrête le placement du travail jusqu'à ce qu'ils rapportent à nouveau.
- **Reclaim d'instance orpheline.** `NodeInstanceReclaimer` (arrière-plan) transition n'importe quelle instance non-terminale abandonnée sur un nœud injoignable à **Failed** (`FailureReason = "Node unreachable - instance reclaimed"`, événement de domaine `InstanceFailed` → notification utilisateur), afin qu'un nœud crashé/partitionné ne puisse jamais laisser une instance bloquée "Running" pour toujours. Reclaim ne tire que une fois le dernier battement de cœur du nœud est rassis au-delà de `HeartbeatTtl + InstanceReclaimGrace`, donnant une chance à une brève-pépite de se rétablir en premier. Les **lancements reclamés ne sont pas re-planifiés automatiquement** : un nœud partitionné-mais-vivant peut toujours exécuter le conteneur et il n'y a pas de clôture au niveau du conteneur, donc re-lancer risquerait l'exécution double — l'utilisateur relance délibérément un lancement reclamé. Les backtests self-exit, donc un backtest reclamé est simplement re-exécuté.
- **L'identité est le nom du nœud.** Main upsert par `NodeName`, donc le pod dont l'IP/URL change au redémarrage garde l'identité, re-register la nouvelle `AdvertiseUrl`.
- **Mode fixé à la première registration.** Le mode du nœud (`Run`/`Backtest`/`Mixed`) est un type persisté, ne peut pas changer sur le battement de cœur ; la re-registration avec un mode différent est honorée pour la vivacité mais le changement de mode est ignoré (enregistré en tant qu'avertissement). Pour changer le mode : supprimer le nœud, le laisser se re-register.

## Configuration

Principal (Web) — `App:Discovery` :

| Clé | Par défaut | Signification |
|-----|---------|---------|
| `Enabled` | `false` | Maître switch pour le point de terminaison register + monitor. |
| `JoinToken` | — | Secret de cluster partagé (≥ 32 caractères) les agents doivent présenter. |
| `HeartbeatTtl` | `00:01:30` | Grâce avant que le nœud silencieux soit marqué injoignable. |
| `InstanceReclaimGrace` | `00:01:00` | Marge supplémentaire au-delà de `HeartbeatTtl` avant qu'une instance abandonnée sur un nœud injoignable soit reclamée (failed). |
| `MonitorInterval` | `00:00:30` | Fréquence de la sweepde monitor et instance-reclaimer. |
| `HeartbeatInterval` | `00:00:30` | Valeur retournée aux agents comme cadence suggérée. |

Agent (CtraderCliNode) — `NodeAgent` :

| Clé | Signification |
|-----|---------|
| `MainUrl` | URL de base du nœud principal. Vide = mode registration manuelle (boucle no-op). |
| `AdvertiseUrl` | URL que main utilise pour atteindre **ce** agent. |
| `NodeName` | Nom unique ; defaults au nom de la machine s'il est vide. |
| `Mode` | `Run` / `Backtest` / `Mixed`. |
| `MaxInstances` | Conseil de capacité honoré par le planificateur. |
| `HeartbeatIntervalSeconds` | Cadence de re-register. |
| `JwtSecret` | Doit égaler le `JoinToken` du principal — à la fois porteur d'enregistrement et clé de signature JWT de dispatch. |

## Modèle de sécurité (v1)

Les nœuds auto-registered partagent **un secret de cluster** (`JoinToken` == `JwtSecret` de chaque agent). Main signe chaque requête de dispatch en tant que JWT HS256 5 minutes avec ce secret ; l'agent valide. Exigences :

- Garder `JoinToken` ≥ 32 caractères et le faire pivoter (mettre à jour `App:Discovery:JoinToken` du principal et `NodeAgent:JwtSecret` de chaque agent ensemble).
- Terminer TLS devant le principal et les agents en production (proxy inverse / ingress).
- L'agent continue à ne exécuter que les images correspondant à `AllowedImagePrefix`.

**Durcissement suivi (pas v1)** : émettre un secret unique par nœud à la registration (kubeadm-style bootstrap → identifiant par nœud) afin qu'un seul agent compromis ne puisse pas forger les jetons de dispatch pour les pairs. Le flux de registration retourne déjà le corps de réponse — endroit naturel pour remettre le secret par nœud menthe.

## Les nœuds manuels fonctionnent toujours

`POST /api/nodes` (interface utilisateur d'administration) continue à enregistrer les nœuds épinglés avec son propre secret par nœud. La découverte est additive.

Un déploiement white-label peut **masquer les contrôles manuels** (ou l'ensemble de la surface Nœuds) et compter uniquement sur la découverte automatique : `App:Branding:NodesUi=Monitor` supprime ajouter/supprimer manuel, `Hidden` supprime la nav, la page et l'API manuelle, et `App:Branding:RestrictNodesToOwner` planchers la surface à propriétaire-seulement. Le point de terminaison self-register + battement de cœur ici est inaffecté dans chaque mode. Voir [White-label → Visibilité de l'interface utilisateur des nœuds](../features/white-label.md#nodes-ui-visibility).
