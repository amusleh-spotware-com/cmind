---
title: Kubernetes telepites - lepesrol lepesre
description: "Helm chart: deploy/helm/cmind. Telepiti a Web-et, MCP-t, önregisztráló csomópont ügynököket, opcionalis in-cluster Postgres-t."
---

# Kubernetes telepites - lepesrol lepesre

Helm chart: `deploy/helm/cmind`. Telepiti a Web-et, MCP-t, önregisztráló csomópont ügynököket, opcionalis in-cluster Postgres-t.

> **Validálva** end-to-end a helyi `kind` klaszteren: minden pod `Ready`-t ér el, a csomópont ügynök önregisztrálja magát per-pod headless DNS névvel, `/health` + `/version` 200-at ad vissza, a skálázáscsökkentett ügynök automatikusan `IsReachable=f`-re vált. Az alábbi folyamat = ami tesztelt.

## 0. Előfeltételek

- Kubernetes klaszter (managed EKS/AKS/GKE, vagy helyi `kind`/`k3d`/`minikube`).
- `kubectl` (a cél context-re mutat) és `helm` 3.
- Container registry, ahonnét a klaszter húzni tud (helyi `kind` - töltsd be a képeket inkább).

## 1. Építsd a három képet

```bash
docker build -f Dockerfile.web        -t <registry>/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t <registry>/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t <registry>/cmind-node-agent:1.0.0 .
```

Push (`docker push <registry>/cmind-web:1.0.0`, stb.), **vagy** helyi `kind` klaszterhez töltsd be közvetlenül:

```bash
kind create cluster --name cmind
for s in web mcp node-agent; do
  docker tag <registry>/cmind-$s:1.0.0 local/cmind-$s:test
  kind load docker-image local/cmind-$s:test --name cmind
done
```

## 2. Válassz titkokat

```bash
PG_PASSWORD=$(openssl rand -hex 16)
JOIN_TOKEN=$(openssl rand -hex 24)   # >= 32 chars; shared cluster secret for node auto-discovery
```

## 3. Telepítsd a chartot

Registry-alapú (managed klaszter):

```bash
helm upgrade --install cmind deploy/helm/cmind   --namespace cmind --create-namespace   --set image.registry=<registry-host> --set image.repository=<org>/cmind --set image.tag=1.0.0   --set secrets.pgPassword="$PG_PASSWORD"   --set secrets.ownerEmail=you@example.com   --set secrets.ownerPassword='Change-Me-Str0ng!'   --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

Helyi `kind` (betöltött képek, külső Postgres nélkül, nem-privilegizált ügynökök):

```bash
helm upgrade --install cmind deploy/helm/cmind   --set image.registry=local --set image.repository=cmind --set image.tag=test --set image.pullPolicy=Never   --set web.dockerSocket.enabled=false --set nodeAgent.privileged=false   --set secrets.pgPassword="$PG_PASSWORD" --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

> `kind`/containerd-en nincs host Docker socket, szóval `web.dockerSocket.enabled=false` (in-app builder/LocalNode nem elérhető) és `nodeAgent.privileged=false` (ügynök még mindig **önregisztrálja magát**; csak nem tud cTrader konténereket futtatni DinD nélkül). Valódi munkaterhelés-futtatáshoz az ügynököket olyan node pool-on futtasd, ahol `nodeAgent.privileged=true` engedélyezett.

Nincs `helm` bináris? Renderelj és alkalmazd:

```bash
helm template cmind deploy/helm/cmind -f my-values.yaml | kubectl apply -f -
```

## 4. Várd a rollout-t

```bash
kubectl -n cmind get pods -w
kubectl -n cmind rollout status deploy/cmind-web
```

Elvárás: `cmind-web`, `cmind-mcp`, `cmind-postgres` (Deployment-ök) és `cmind-node-agent-0` (StatefulSet) mind `Ready`. A Web readiness (`/health`) csak az DB migrálása után megy át (migrációk az indításkor futnak).

## 5. Ellenőrizd az ön-felfedezést

```bash
# A csomópont ügynöknek meg kell jelennie a DB-ben per-pod headless DNS névvel és IsReachable=true-val
PG=$(kubectl -n cmind get pod -l app.kubernetes.io/component=postgres -o jsonpath='{.items[0].metadata.name}')
kubectl -n cmind exec "$PG" -- psql -U postgres -d appdb -c   'SELECT "Name","Kind","IsReachable","BaseUrl" FROM "Nodes";'
```

Példa (ellenőrizve):

```
          Name           |    Kind     | IsReachable |                     BaseUrl
-------------------------+-------------+-------------+-------------------------------------------------
 cmind-node-agent-0      | ActiveMixed | t           | http://cmind-node-agent-0.cmind-node-agent...:8080
```

Skálázd a kapacitást replikák hozzáadásával - minden új pod önregisztrálja magát egy szívverési intervallumon belül:

```bash
kubectl -n cmind scale statefulset/cmind-node-agent --replicas=3
```

Staleness egyeztetés (ellenőrizve): skálázd az ügynököt lefelé, `IsReachable=f`-re vált `discovery.heartbeatTtl` után; skálázd vissza, visszatér online.

## 6. Érd el a UI-t

```bash
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# http://localhost:8080  - jelentkezz be a seedelt tulajdonossal
```

Külső hozzáférés: állítsd be `web.ingress.enabled=true`, `web.ingress.host` és TLS-t.

## Miért a csomópont ügynökök StatefulSet-ként

A fő csomópont munkát küld **specifikus** ügynöknek URL alapján, szóval minden ügynöknek stabil, egyénileg címezhető DNS neve kell. A chart StatefulSet + headless Service-t használ; minden pod meghirdeti magát `http://<pod>.<svc>.<ns>.svc.cluster.local:8080`-ként és önregisztrálja magát pod név alatt. Ugyanaz a felfedezési mechanizmus, amit a cTrader CLI node-ok használnak - lásd [../operations/node-discovery.md](../operations/node-discovery.md).

## Web skálázás (SignalR backplane, S6)

Web alkalmazás = Blazor Server + SignalR (élő műszerfal, logs hub). Ha **több mint egy Web replikát** futatsz, állítsd be a `signalr` kapcsolati karakterláncot a Redis végpontra - az alkalmazás ekkor regisztrálja a **SignalR Redis backplane**-t (`AddStackExchangeRedis`), igy a hub üzenetek és a circuit egyeztetések szétterjednek a replikák között és egy reconnect egy másik pod-ra élőben marad. Nincs `signalr` kapcsolati karakterlánc = egyetlen replika in-memory (változatlan). Párosítsd session affinity-vel az ingress-nél a legsimább Blazor Server körökért.

## Copy-agent autoscaling és rugalmasság

A copy-agent hosszan élő trading socketeket gazdagép, szóval a skálázás a **munka, nem CPU** alapján történik. Az `copyAgent.keda.enabled=true` chart telepíti a KEDA `ScaledObject`-et, amely Postgres-t kérdez a futó másolási profilok számáról és skáláz a replikákat, hogy minden pod körülbelül `copyAgent.keda.profilesPerPod` (alapértelemzés 25) profilt gazdagépjen, a `minReplicas`/`maxReplicas` között. A KEDA a DB-t a `TriggerAuthentication` révén olvassa, ami a `copyAgent.keda.connectionSecretKey` secret kulcsához van kötve. Amikor `copyAgent.replicas > 1` (vagy KEDA 1 fölé skáláz), a chart hozzáadja a `topologySpreadConstraints`-t (csomópontok közötti szétterítés) és a `PodDisruptionBudget`-et (`minAvailable: 1`); skálázáscsökkentés / rolling update alatt minden pod felszabadítja a lease-eit SIGTERM-en (`terminationGracePeriodSeconds`, alapértelemzés 30), igy a túlélő azonnal reclaim-eli - lásd [scaling.md](scaling.md).

## Fontos értékek

| Érték | Cél |
|-------|-----|
| `image.registry` / `.repository` / `.tag` / `.pullPolicy` | Kép koordináták (`local` + `Never` kind-hoz). |
| `secrets.existingSecret` | Külső/sealed Secret használata a chart által kezelt értékek helyett. |
| `postgres.enabled` | `true` = in-cluster Postgres (dev). `false` + `externalDatabase.connectionString` managed DB-hez (prod). |
| `web.ingress.*` / `web.autoscaling` / `mcp.autoscaling` | Ingress + TLS, HPA CPU-n. |
| `nodeAgent.replicas` / `.privileged` / `.mode` / `.maxInstances` | Ügynök darabszám, DinD privilégium, mód, kapacitás. |
| `web.dockerSocket.enabled` | hostPath `/var/run/docker.sock` Web builder/LocalNode-hoz (csak Docker-runtime csomópontokon). |
| `observability.otlpEndpoint` | Logs+traces+metrics hajózása OTLP gyűjtőnek. |

## Probe-ok

Liveness `/alive`, readiness `/health` (Web) · `/version` (MCP) · `/health` (ügynök) - leképezve minden környezetben.

## In-cluster tesztcsomag

Futtasd a másolási kereskedési csomagot Kubernetes `Job`-ként a telepített alkalmazás ellen, igy a regresszió el van kapva in-cluster, ugyanúgy, mint lokálisan. A másolási tesztekhez csak Web + Postgres + token cache kell - **nincs** privilégizált csomópont ügynökök.

Egyhuzású, reprodukálható (kind fel -> képek építése+betöltés -> chart telepítése -> teszt Job futtatása -> assert exit 0 -> takarítás):

```bash
scripts/k8s-e2e.sh                                   # determinisztikus másolási csomag (nincs titok)
USE_EXISTING_CLUSTER=1 scripts/k8s-e2e.sh            # jelenlegi kube context újbóli használata
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # élő
```

Manuális / CI huzalozás - **determinisztikus (alapértelemzés, nincs titok):**

```bash
docker build -f Dockerfile.tests -t cmind-tests:e2e .          # runner kép (SDK + épített teszt projektek)
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true
kubectl -n cmind wait --for=condition=complete --timeout=15m job/cmind-cmind-tests
kubectl -n cmind logs job/cmind-cmind-tests
```

**Élő csomag** hozzá kell token cache-t. A cTrader **refresh token-ek single-use**, szóval a cache **írható**: Job másolja a Secret-et egy üres emptyDir-be `/app/secrets`-ként init-container révén.

```bash
kubectl -n cmind create secret generic cmind-copy-secrets --from-file=secrets/   # soha ne legyen a képbe sütve
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true   --set tests.project='tests/IntegrationTests/IntegrationTests.csproj'   --set tests.filter='FullyQualifiedName~CopyTradingLiveTests'   --set tests.copySecret=cmind-copy-secrets
```

| Érték | Cél |
|-------|-----|
| `tests.enabled` | Teszt `Job` renderelése (alapértelemzés `false`). |
| `tests.project` / `tests.filter` | Melyik projekt + `dotnet test --filter` fusson (alapértelemzés: determinisztikus). |
| `tests.copySecret` | Opcionális Secret gitignolt `openapi-*.local.json`-nal; bemásolva **írható** emptyDir-be `/app/secrets`-ként az élő csomaghoz. Üres => nincs secret mount. |
| `tests.backoffLimit` | Job újrapróbálkozási szám (alapértelemzés `0`). |

`LiveCopySecrets` feljebb sétál `/app`-tól a `secrets/`-ig; élő tesztek tiszta átugranak, ha a cache hiányzik. `Dockerfile.tests` SDK-alapú, szóval ugyanazokat az állításokat futtatja, mint a lokális `dotnet test` - mind a determinisztikus (`101 passed`), mind a teljes élő (`8 passed`) csomagok ellenőrizve, hogy helyi Docker elleni futtatás előtt szállítva.

## Leszerelés

```bash
helm -n cmind uninstall cmind        # vagy: kubectl delete -f <rendered>.yaml
kind delete cluster --name cmind     # csak helyi
```

## Az in-cluster csomag futtatása cross-platform (Linux / macOS / Windows / WSL)

`scripts/k8s-e2e.sh` OS-független. A repo útvonalat natív formára konvertálja (`cygpath -m`), szóval a Docker, helm és kubectl megoldja **Windows/git-bash**-en is, valamint Linux/macOS-en - end-to-end ellenőrizve Windowson (kind klaszter fel -> képek építve+betöltve -> chart telepítve -> in-cluster teszt Job zöld -> takarítás).

| Környezet | Parancs |
|------------|---------|
| Linux / macOS | `scripts/k8s-e2e.sh` |
| Windows (git-bash) | `bash scripts/k8s-e2e.sh` **vagy** `pwsh scripts/k8s-e2e.ps1` |
| Windows → **WSL (preferált)** | `pwsh scripts/k8s-e2e.ps1 -Wsl` |

**WSL preferált Windowson.** WSL-en belül futtatva natív Linux útvonalakat használ és a Docker Desktop WSL integrációját, elkerülve minden útvonal-fordítási edge case-et - a legrobusztusabb opció. `docker`, `kind`, `helm`, `kubectl` és .NET SDK kell a WSL PATH-ában (Docker Desktop biztosítja a `docker`-t; a többiet a disztribúcióból telepítsd, pl. `go install sigs.k8s.io/kind@latest`, a helm/kubectl release binárisokat). `scripts/k8s-e2e.ps1` wrapper WSL-t választ `-Wsl`-lel, különben git-bash-re esik vissza.

`kind` + `helm` ön-telepíthető, ha hiányzik (release binárisok vagy `choco install kind kubernetes-helm`); ne kezeld elérhetetlenként. Lásd még [../testing/live-copy-trading.md](../testing/live-copy-trading.md).
