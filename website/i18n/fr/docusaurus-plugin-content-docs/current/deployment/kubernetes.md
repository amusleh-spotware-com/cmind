---
description: "Graphique Helm : deploy/helm/cmind. Déploie Web, MCP, agents de nœud auto-enregistrés, Postgres en cluster optionnel."
---

# Déploiement Kubernetes — pas à pas

Graphique Helm : `deploy/helm/cmind`. Déploie Web, MCP, agents de nœud auto-enregistrés,
Postgres en cluster optionnel.

> **Validé** de bout en bout sur cluster `kind` local : tous les pods atteignent `Ready`, l'agent
> de nœud s'auto-enregistre avec un nom DNS sans tête par pod, `/health` + `/version` retournent 200,
> agent réduit automatiquement marqué injoignable. Le flux ci-dessous = ce qui a été testé.

## 0. Prérequis

- Cluster Kubernetes (EKS/AKS/GKE géré, ou `kind`/`k3d`/`minikube` local).
- `kubectl` (pointé sur le contexte cible) et `helm` 3.
- Registre de conteneurs que le cluster peut extraire (skip pour `kind` local — chargez les images à la place).

## 1. Construire les trois images

```bash
docker build -f Dockerfile.web        -t <registry>/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t <registry>/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t <registry>/cmind-node-agent:1.0.0 .
```

Poussez (`docker push <registry>/cmind-web:1.0.0`, etc.), **ou** pour le cluster `kind` local chargez directement :

```bash
kind create cluster --name cmind
for s in web mcp node-agent; do
  docker tag <registry>/cmind-$s:1.0.0 local/cmind-$s:test
  kind load docker-image local/cmind-$s:test --name cmind
done
```

## 2. Choisir les secrets

```bash
PG_PASSWORD=$(openssl rand -hex 16)
JOIN_TOKEN=$(openssl rand -hex 24)   # >= 32 caractères ; secret de cluster partagé pour l'auto-découverte des nœuds
```

## 3. Installer le graphique

Basé sur le registre (cluster géré) :

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --namespace cmind --create-namespace \
  --set image.registry=<registry-host> --set image.repository=<org>/cmind --set image.tag=1.0.0 \
  --set secrets.pgPassword="$PG_PASSWORD" \
  --set secrets.ownerEmail=you@example.com \
  --set secrets.ownerPassword='Change-Me-Str0ng!' \
  --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

`kind` local (images chargées, pas de Postgres externe, agents sans privilèges) :

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --set image.registry=local --set image.repository=cmind --set image.tag=test --set image.pullPolicy=Never \
  --set web.dockerSocket.enabled=false --set nodeAgent.privileged=false \
  --set secrets.pgPassword="$PG_PASSWORD" --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

> Sur `kind`/containerd pas de socket Docker hôte, donc `web.dockerSocket.enabled=false`
> (générateur d'application/LocalNode non disponible) et `nodeAgent.privileged=false` (l'agent s'auto-enregistre
> toujours ; juste ne peut pas exécuter les conteneurs cTrader sans DinD). Pour l'exécution de charges de travail réelles,
> exécutez les agents sur un pool de nœuds où `nodeAgent.privileged=true` est autorisé.

Pas de binaire `helm` ? Rendre et appliquer :

```bash
helm template cmind deploy/helm/cmind -f my-values.yaml | kubectl apply -f -
```

## 4. Attendre le déploiement

```bash
kubectl -n cmind get pods -w
kubectl -n cmind rollout status deploy/cmind-web
```

Attendez : `cmind-web`, `cmind-mcp`, `cmind-postgres` (Deployments) et `cmind-node-agent-0` (StatefulSet) tous `Ready`. La disponibilité Web (`/health`) ne passe que une fois la DB migrée (les migrations s'exécutent au démarrage).

## 5. Vérifier l'auto-découverte

```bash
# L'agent de nœud doit apparaître dans la DB avec une URL de base DNS sans tête par pod et IsReachable=true
PG=$(kubectl -n cmind get pod -l app.kubernetes.io/component=postgres -o jsonpath='{.items[0].metadata.name}')
kubectl -n cmind exec "$PG" -- psql -U postgres -d appdb -c \
  'SELECT "Name","Kind","IsReachable","BaseUrl" FROM "Nodes";'
```

Exemple (vérifié) :

```
          Name           |    Kind     | IsReachable |                     BaseUrl
-------------------------+-------------+-------------+-------------------------------------------------
 cmind-node-agent-0      | ActiveMixed | t           | http://cmind-node-agent-0.cmind-node-agent...:8080
```

Augmentez la capacité en ajoutant des répliques — chaque nouveau pod s'auto-enregistre dans un intervalle de battement de cœur :

```bash
kubectl -n cmind scale statefulset/cmind-node-agent --replicas=3
```

Réconciliation de fraîcheur (vérifiée) : réduisez l'agent, basculez à `IsReachable=f` après `discovery.heartbeatTtl` ; augmentez, revient en ligne.

## 6. Accéder à l'interface utilisateur

```bash
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# http://localhost:8080  — connectez-vous avec le propriétaire amorcé
```

Accès externe : définir `web.ingress.enabled=true`, `web.ingress.host`, et TLS.

## Pourquoi les agents de nœud sont un StatefulSet

Main dispatch du travail **spécifiquement** à l'agent par URL, donc chaque agent a besoin d'un nom DNS stable et individuellement adressable. Le graphique utilise StatefulSet + Service sans tête ; chaque pod publie `http://<pod>.<svc>.<ns>.svc.cluster.local:8080` et s'auto-enregistre sous le nom du pod. Même mécanisme de découverte que les nœuds cTrader CLI nu — voir [../operations/node-discovery.md](../operations/node-discovery.md).

## Mise à l'échelle du Web (plan arrière-plan SignalR, S6)

L'application Web = Blazor Server + SignalR (tableau de bord en direct, hub de journaux). Pour exécuter **plus d'une réplique Web**, définir la chaîne de connexion `signalr` sur le point de terminaison Redis — l'application s'enregistre alors **plan arrière-plan SignalR Redis** (`AddStackExchangeRedis`) afin que les messages du hub et la négociation des circuits se propagent entre les répliques et une reconnexion s'atterrisse sur un pod différent reste active. Pas de chaîne de connexion `signalr` = réplique unique en mémoire (inchangée). Appariez avec l'affinité de session à l'entrée pour les circuits Blazor Server les plus fluides.

## Auto-mise à l'échelle des agents de copie et résilience

Les hôtes des agents de copie hébergent des sockets commerciales longue durée, donc se mettent à l'échelle sur **le travail, non le CPU**. Avec `copyAgent.keda.enabled=true`, le graphique installe KEDA `ScaledObject` qui interroge Postgres pour le nombre de profils de copie en cours d'exécution et met à l'échelle les répliques pour que chaque pod héberge environ `copyAgent.keda.profilesPerPod` (25 par défaut), entre `minReplicas`/`maxReplicas`. KEDA lit la DB via `TriggerAuthentication` lié à la clé secrète `copyAgent.keda.connectionSecretKey`. Quand `copyAgent.replicas > 1` (ou KEDA met à l'échelle au-delà de 1), le graphique ajoute également `topologySpreadConstraints` (répartir entre les nœuds) et `PodDisruptionBudget` (`minAvailable: 1`) ; lors de la réduction/mise à jour progressive, chaque pod libère les baux sur `SIGTERM` (`terminationGracePeriodSeconds`, 30 par défaut) afin que le survivant reprenne immédiatement — voir [scaling.md](scaling.md).

## Valeurs clés

| Valeur | Objectif |
|-------|---------|
| `image.registry` / `.repository` / `.tag` / `.pullPolicy` | Coordonnées de l'image (`local` + `Never` pour kind). |
| `secrets.existingSecret` | Utilisez un Secret externe/scellé au lieu des valeurs gérées par le graphique. |
| `postgres.enabled` | `true` = Postgres en cluster (dev). `false` + `externalDatabase.connectionString` pour la DB gérée (prod). |
| `web.ingress.*` / `web.autoscaling` / `mcp.autoscaling` | Entrée + TLS, HPA sur CPU. |
| `nodeAgent.replicas` / `.privileged` / `.mode` / `.maxInstances` | Nombre d'agents, privilège DinD, mode, capacité. |
| `web.dockerSocket.enabled` | hostPath `/var/run/docker.sock` pour le générateur Web/LocalNode (nœuds uniquement avec runtime Docker). |
| `observability.otlpEndpoint` | Envoyer les logs+traces+métriques au collecteur OTLP. |

## Sondes

liveness `/alive`, readiness `/health` (Web) · `/version` (MCP) · `/health` (agent) — mappés dans tous les environnements.

## Suite de tests en cluster

Exécutez la suite de copie-trading en tant que Kubernetes `Job` par rapport à l'application déployée, la régression est donc capturée dans le cluster de la même façon que localement. Les tests de copie ne nécessitent que Web + Postgres + cache de jetons — **pas** d'agents de nœud privilégiés.

Une seule fois, reproductible (kind up → build+load images → deploy → run Job → assert exit 0 → tear down) :

```bash
scripts/k8s-e2e.sh                                   # suite de copie déterministe (pas de secrets)
USE_EXISTING_CLUSTER=1 scripts/k8s-e2e.sh            # réutiliser le contexte kube actuel
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # live
```

Câblage manuel/CI — **déterministe (par défaut, pas de secrets) :**

```bash
docker build -f Dockerfile.tests -t cmind-tests:e2e .          # image du coureur (SDK + projets de test construits)
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true
kubectl -n cmind wait --for=condition=complete --timeout=15m job/cmind-cmind-tests
kubectl -n cmind logs job/cmind-cmind-tests
```

**Suite Live** a en plus besoin du cache de jetons. Les jetons d'actualisation cTrader sont à **usage unique**, donc le cache doit être **inscriptible** : Job copie Secret dans emptyDir à `/app/secrets` via init-container.

```bash
kubectl -n cmind create secret generic cmind-copy-secrets --from-file=secrets/   # jamais cuit dans l'image
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true \
  --set tests.project='tests/IntegrationTests/IntegrationTests.csproj' \
  --set tests.filter='FullyQualifiedName~CopyTradingLiveTests' \
  --set tests.copySecret=cmind-copy-secrets
```

| Valeur | Objectif |
|-------|---------|
| `tests.enabled` | Rendre Job de test (par défaut `false`). |
| `tests.project` / `tests.filter` | Quel projet + `dotnet test --filter` exécuter (par défaut : déterministe). |
| `tests.copySecret` | Secret optionnel avec `openapi-*.local.json` gitignored ; copié dans emptyDir **inscriptible** à `/app/secrets` pour la suite live. Vide ⇒ pas de montage de secret. |
| `tests.backoffLimit` | Nombre de tentatives de Job (par défaut `0`). |

`LiveCopySecrets` remonte de `/app` pour trouver `secrets/` ; les tests live sautent proprement quand le cache est absent. `Dockerfile.tests` basé sur SDK donc exécute les mêmes assertions que `dotnet test` local — les deux suites déterministe (`101 passed`) et live complète (`8 passed`) vérifiées exécutant cette image localement par rapport à Docker avant l'expédition.

## Démontage

```bash
helm -n cmind uninstall cmind        # ou : kubectl delete -f <rendered>.yaml
kind delete cluster --name cmind     # local uniquement
```

## Exécution de la suite en cluster multiplateforme (Linux / macOS / Windows / WSL)

`scripts/k8s-e2e.sh` indépendant du système d'exploitation. Convertit le chemin du repo en forme native (`cygpath -m`) afin que Docker, helm et kubectl le résolvent sur **Windows/git-bash** ainsi que Linux/macOS — vérifié de bout en bout sur Windows (kind cluster up → images built+loaded → chart deployed → in-cluster test Job green → teardown).

| Environnement | Commande |
|-------------|---------|
| Linux / macOS | `scripts/k8s-e2e.sh` |
| Windows (git-bash) | `bash scripts/k8s-e2e.sh` **ou** `pwsh scripts/k8s-e2e.ps1` |
| Windows → **WSL (préféré)** | `pwsh scripts/k8s-e2e.ps1 -Wsl` |

**Préférez WSL sur Windows.** L'exécution dans WSL utilise les chemins Linux natifs et l'intégration WSL de Docker Desktop, évitant tous les cas limites de translation de chemins — option la plus robuste. Nécessite `docker`, `kind`, `helm`, `kubectl` et le SDK .NET sur WSL PATH (Docker Desktop fournit `docker` ; installez le reste dans la distro, p. ex. `go install sigs.k8s.io/kind@latest`, les binaires de sortie helm/kubectl). Le wrapper `scripts/k8s-e2e.ps1` choisit WSL avec `-Wsl`, revient à git-bash sinon.

`kind` + `helm` auto-installables s'ils sont absents (binaires de sortie ou `choco install kind kubernetes-helm`) ; ne pas traiter comme indisponible. Voir aussi [../testing/live-copy-trading.md](../testing/live-copy-trading.md).
