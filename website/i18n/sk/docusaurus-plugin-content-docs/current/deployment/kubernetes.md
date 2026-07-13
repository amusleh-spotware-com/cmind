---
description: "Helm chart: deploy/helm/cmind. Nasaďuje Web, MCP, samoreg stratujúce agenti uzlov, voliteľný in-cluster Postgres."
---

# Kubernetes nasadenie — krok za krokom

Helm chart: `deploy/helm/cmind`. Nasaďuje Web, MCP, samoreg stratujúce agenti uzlov, voliteľný
in-cluster Postgres.

> **Overené** end-to-end na lokálnom `kind` klastri: všetky pody dosahujú `Ready`, agent uzla
> samoreg stratuje s per-pod headless DNS menom, `/health` + `/version` vrátia 200, agent zmenšený
> automaticky označený nedosiahniteľný. Tok nižšie = čo bolo testované.

## 0. Predpoklady

- Kubernetes klaster (spravovaný EKS/AKS/GKE, alebo lokálny `kind`/`k3d`/`minikube`).
- `kubectl` (nasmerovaný na cieľový kontext) a `helm` 3.
- Registra kontajnerov klaster môže ťahať (preskočiť pre lokálny `kind` — namiesto toho načítajte obrázky).

## 1. Vytvorte tri obrázky

```bash
docker build -f Dockerfile.web        -t <registry>/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t <registry>/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t <registry>/cmind-node-agent:1.0.0 .
```

Zatlačte (`docker push <registry>/cmind-web:1.0.0`, atď.), **alebo** pre lokálny `kind` klaster načítajte
priamo:

```bash
kind create cluster --name cmind
for s in web mcp node-agent; do
  docker tag <registry>/cmind-$s:1.0.0 local/cmind-$s:test
  kind load docker-image local/cmind-$s:test --name cmind
done
```

## 2. Vyberte tajomstvá

```bash
PG_PASSWORD=$(openssl rand -hex 16)
JOIN_TOKEN=$(openssl rand -hex 24)   # >= 32 znakov; zdieľané tajomstvo klastra pre auto-discovery uzla
```

## 3. Nainštalujte chart

Registra-based (spravovaný klaster):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --namespace cmind --create-namespace \
  --set image.registry=<registry-host> --set image.repository=<org>/cmind --set image.tag=1.0.0 \
  --set secrets.pgPassword="$PG_PASSWORD" \
  --set secrets.ownerEmail=you@example.com \
  --set secrets.ownerPassword='Change-Me-Str0ng!' \
  --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

Lokálny `kind` (načítané obrázky, bez externého Postgres, bez privilegovaných agentov):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --set image.registry=local --set image.repository=cmind --set image.tag=test --set image.pullPolicy=Never \
  --set web.dockerSocket.enabled=false --set nodeAgent.privileged=false \
  --set secrets.pgPassword="$PG_PASSWORD" --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

> Na `kind`/containerd bez hostitieľskej Docker zásuvky, takže `web.dockerSocket.enabled=false`
> (builder/LocalNode v aplikácii nedostupné) a `nodeAgent.privileged=false` (agent stále
> **samoreg stratuje**; len nemôže spustiť kontajnery cTrader bez DinD). Pre reálne vykonávanie úloh
> spustite agenti na pool uzlov, kde `nodeAgent.privileged=true` je povolené.

Bez binárneho `helm`? Vykresliť a aplikovať:

```bash
helm template cmind deploy/helm/cmind -f my-values.yaml | kubectl apply -f -
```

## 4. Čakajte na rollout

```bash
kubectl -n cmind get pods -w
kubectl -n cmind rollout status deploy/cmind-web
```

Očakávajte: `cmind-web`, `cmind-mcp`, `cmind-postgres` (Deployments) a `cmind-node-agent-0`
(StatefulSet) všetko `Ready`. Web readiness (`/health`) prechádza iba po migrácii DB (migrácie
spustené pri spustení).

## 5. Overujte auto-objavovanie

```bash
# Agent uzla by sa mal objaviť v DB s per-pod headless DNS BaseUrl a IsReachable=true
PG=$(kubectl -n cmind get pod -l app.kubernetes.io/component=postgres -o jsonpath='{.items[0].metadata.name}')
kubectl -n cmind exec "$PG" -- psql -U postgres -d appdb -c \
  'SELECT "Name","Kind","IsReachable","BaseUrl" FROM "Nodes";'
```

Príklad (overený):

```
          Name           |    Kind     | IsReachable |                     BaseUrl
-------------------------+-------------+-------------+-------------------------------------------------
 cmind-node-agent-0      | ActiveMixed | t           | http://cmind-node-agent-0.cmind-node-agent...:8080
```

Škálujte kapacitu pridaním replík — každý nový pod samoreg stratuje v rámci jedného intervalu pulzov:

```bash
kubectl -n cmind scale statefulset/cmind-node-agent --replicas=3
```

Rekoncilácia zastaralosti (overená): agent škálujte nadol, preklopí sa na `IsReachable=f` po
`discovery.heartbeatTtl`; škálujte späť hore, vráti sa online.

## 6. Dosiahnite UI

```bash
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# http://localhost:8080  — prihláste sa s osemením vlastníkom
```

Externý prístup: nastavte `web.ingress.enabled=true`, `web.ingress.host` a TLS.

## Prečo sú agenti uzlov StatefulSet

Hlavný uzol odosielá prácu na **špecifického** agenta podľa URL, takže každý agent potrebuje stabilný,
individuálne adresovateľný DNS názov. Chart používa StatefulSet + headless Service; každý pod
inzeruje `http://<pod>.<svc>.<ns>.svc.cluster.local:8080` a samoreg stratuje pod názvom podu.
Rovnaký mechanizmus objavovania používajú bare cTrader CLI uzly —
pozrite si [../operations/node-discovery.md](../operations/node-discovery.md).

## Web scale-out (SignalR backplane, S6)

Web app = Blazor Server + SignalR (živý dashboard, logy hub). Na spustenie **viac než jednej Web repliky**,
nastavte connection string `signalr` na Redis koncový bod — aplikácia potom registruje **SignalR Redis
backplane** (`AddStackExchangeRedis`) takže hub správy a vyjednávanie okruhu sa fanajú cez repliky a
opätovné pripojenie pristane na inom pode, zostane živé. Žiadny connection string `signalr` = jednoreplicový
v pamäti (nezmenené). Spárujte s afinitou sedenia na ingress pre hladšie Blazor Server okruhy.

## Copy-agent autoscaling & odolnosť

Copy-agent hostuje dlhodobé obchodné zásuvky, takže sa škáluje na **prácu, nie CPU**. S
`copyAgent.keda.enabled=true` chart inštaluje KEDA `ScaledObject`, ktorý sa pýta Postgres na
bežiaci počet profilu kópií a škáluje repliky, takže každý pod hostuje asi `copyAgent.keda.profilesPerPod`
(štandardne 25), medzi `minReplicas`/`maxReplicas`. KEDA číta DB cez `TriggerAuthentication` viazané na
`copyAgent.keda.connectionSecretKey` tajný kľúč. Keď `copyAgent.replicas > 1` (alebo KEDA škáluje nad 1)
chart tiež pridáva `topologySpreadConstraints` (rozprestrite cez uzly) a `PodDisruptionBudget`
(`minAvailable: 1`); na scale-in / rolling update každý pod uvoľní leasy na `SIGTERM`
(`terminationGracePeriodSeconds`, štandardne 30) takže prežívajúci si prihlásia okamžite — pozrite si
[scaling.md](scaling.md).

## Kľúčové hodnoty

| Hodnota | Účel |
|-------|---------|
| `image.registry` / `.repository` / `.tag` / `.pullPolicy` | Súradnice obrázka (`local` + `Never` pre kind). |
| `secrets.existingSecret` | Použite externé/zapečatené Secret namiesto hodnôt spravovaných chartom. |
| `postgres.enabled` | `true` = in-cluster Postgres (vývoj). `false` + `externalDatabase.connectionString` pre spravovanú DB (prod). |
| `web.ingress.*` / `web.autoscaling` / `mcp.autoscaling` | Ingress + TLS, HPA na CPU. |
| `nodeAgent.replicas` / `.privileged` / `.mode` / `.maxInstances` | Počet agentov, privilégia DinD, režim, kapacita. |
| `web.dockerSocket.enabled` | hostPath `/var/run/docker.sock` pre Web builder/LocalNode (iba Docker-runtime uzly). |
| `observability.otlpEndpoint` | Poslať logs+traces+metriky do OTLP kolektora. |

## Sondy

liveness `/alive`, readiness `/health` (Web) · `/version` (MCP) · `/health` (agent) — mapované vo všetkých
prostrediach.

## In-cluster test suite

Spustite copy-trading suite ako Kubernetes `Job` voči nasadenej aplikácii, takže regresia sa chytí
in-cluster rovnako ako lokálne. Kopírovať testy potrebujú iba Web + Postgres + token cache — **žiadny**
privilegovaný agent uzla.

One-shot, opakovateľný (kind up → build+load images → deploy → run Job → assert exit 0 → tear down):

```bash
scripts/k8s-e2e.sh                                   # determinický copy suite (bez tajomstiev)
USE_EXISTING_CLUSTER=1 scripts/k8s-e2e.sh            # opätovne použite aktuálny kube kontext
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # live
```

Manuálne / CI zapojenie — **determinický (štandardne, bez tajomstiev):**

```bash
docker build -f Dockerfile.tests -t cmind-tests:e2e .          # runner obrázok (SDK + postavené projektov testov)
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true
kubectl -n cmind wait --for=condition=complete --timeout=15m job/cmind-cmind-tests
kubectl -n cmind logs job/cmind-cmind-tests
```

**Live suite** dodatočne potrebuje token cache. cTrader **refresh tokeny jednorazové**, takže cache
musí byť **zapisovateľný**: Job kopíruje Secret do emptyDir na `/app/secrets` cez init-container.

```bash
kubectl -n cmind create secret generic cmind-copy-secrets --from-file=secrets/   # nikdy nebolo pečené do obrázka
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true \
  --set tests.project='tests/IntegrationTests/IntegrationTests.csproj' \
  --set tests.filter='FullyQualifiedName~CopyTradingLiveTests' \
  --set tests.copySecret=cmind-copy-secrets
```

| Hodnota | Účel |
|-------|---------|
| `tests.enabled` | Vykresliť test `Job` (štandardne `false`). |
| `tests.project` / `tests.filter` | Ktorý projekt + `dotnet test --filter` spustiť (štandardne: determinický). |
| `tests.copySecret` | Voliteľné Secret s gitignored `openapi-*.local.json`; kopírované do **zapisovateľného** emptyDir na `/app/secrets` pre live suite. Prázdne ⇒ žiadny secret mount. |
| `tests.backoffLimit` | Počet pokusov Job (štandardne `0`). |

`LiveCopySecrets` ide nahor od `/app` na nájdenie `secrets/`; live testy preskakujú čisto keď cache
absentný. `Dockerfile.tests` SDK-based takže spúšťa rovnaké tvrdenia ako lokálny `dotnet test` — oba
determinický (`101 passed`) a plný live (`8 passed`) suites overený spúšťajúce sa v tomto
obrázka lokálne voči Docker pred dodaním.

## Teardown

```bash
helm -n cmind uninstall cmind        # alebo: kubectl delete -f <rendered>.yaml
kind delete cluster --name cmind     # iba lokálne
```

## Spustenie in-cluster suite cross-platform (Linux / macOS / Windows / WSL)

`scripts/k8s-e2e.sh` OS-nezávislý. Konvertuje cestu repo na natívnu formu (`cygpath -m`) takže Docker,
helm a kubectl ju riešia na **Windows/git-bash** tak ako Linux/macOS — overeno end to end na Windows
(kind cluster up → images built+loaded → chart deployed → in-cluster test Job green → teardown).

| Prostredie | Príkaz |
|-------------|---------|
| Linux / macOS | `scripts/k8s-e2e.sh` |
| Windows (git-bash) | `bash scripts/k8s-e2e.sh` **alebo** `pwsh scripts/k8s-e2e.ps1` |
| Windows → **WSL (uprednostňujú)** | `pwsh scripts/k8s-e2e.ps1 -Wsl` |

**Uprednostňujte WSL na Windows.** Spustenie vnútri WSL používa natívne Linux cesty a Docker Desktop integrácia WSL,
vyhnete sa všetkým hranám prekladu cesty — najrobustnejšia možnosť. Potrebuje `docker`, `kind`, `helm`,
`kubectl` a .NET SDK na WSL PATH (Docker Desktop poskytuje `docker`; nainštalujte zvyšok v distro,
napr. `go install sigs.k8s.io/kind@latest`, binárne vydania helm/kubectl). Obaľovač `scripts/k8s-e2e.ps1`
vyberá WSL s `-Wsl`, vraciť sa do git-bash inak.

`kind` + `helm` self-inštalovateľný ak chýba (binárne vydania alebo `choco install kind kubernetes-helm`);
neuznávajte ako nedostupný. Pozrite si tiež [../testing/live-copy-trading.md](../testing/live-copy-trading.md).
