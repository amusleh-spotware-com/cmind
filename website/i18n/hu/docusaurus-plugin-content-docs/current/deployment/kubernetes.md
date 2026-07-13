---
description: "Helm diagram: deploy/helm/cmind. Telepít Web, MCP, önregisztrálódó csomópont ügynökök, opcionális in-cluster Postgres."
---

# Kubernetes telepítés — lépésről lépésre

Helm diagram: `deploy/helm/cmind`. Telepít Web, MCP, önregisztrálódó csomópont ügynökök, opcionális in-cluster Postgres.

> **Validálva** végtelenül a helyi `kind` fürtön: mind a podok elérte a `Ready` állapotot, csomópont ügynök önregisztrálódik per-pod headless DNS névvel, `/health` + `/version` visszatér 200-al, leskálázott ügynök automatikusan megjelölt elérhetetlen. Az alatti áramlás = amit tesztelt.

## 0. Előfeltételek

- Kubernetes fürt (kezelt EKS/AKS/GKE, vagy helyi `kind`/`k3d`/`minikube`).
- `kubectl` (mutató a cél kontextre) és `helm` 3.
- Konténer regiszter, amelyből a fürt húz (kihagyat a helyi `kind` — telepítsd a képeket helyette).

## 1. Az három kép felépítése

```bash
docker build -f Dockerfile.web        -t <registry>/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t <registry>/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t <registry>/cmind-node-agent:1.0.0 .
```

Nyomás (`docker push <registry>/cmind-web:1.0.0`, stb.), **vagy** a helyi `kind` fürt számára telepít közvetlenül:

```bash
kind create cluster --name cmind
for s in web mcp node-agent; do
  docker tag <registry>/cmind-$s:1.0.0 local/cmind-$s:test
  kind load docker-image local/cmind-$s:test --name cmind
done
```

## 2. Válassz titkok

```bash
PG_PASSWORD=$(openssl rand -hex 16)
JOIN_TOKEN=$(openssl rand -hex 24)   # >= 32 chars; megosztott fürt titok csomópont auto-felfedezéshez
```

## 3. Telepítsd a diagramot

Regiszter-alapú (kezelt fürt):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --namespace cmind --create-namespace \
  --set image.registry=<registry-host> --set image.repository=<org>/cmind --set image.tag=1.0.0 \
  --set secrets.pgPassword="$PG_PASSWORD" \
  --set secrets.ownerEmail=you@example.com \
  --set secrets.ownerPassword='Change-Me-Str0ng!' \
  --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

Helyi `kind` (telepített képek, nem külső Postgres, nem kiváltságolt ügynökök):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --set image.registry=local --set image.repository=cmind --set image.tag=test --set image.pullPolicy=Never \
  --set web.dockerSocket.enabled=false --set nodeAgent.privileged=false \
  --set secrets.pgPassword="$PG_PASSWORD" --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

> A `kind`/containerd esetén nincs gazdagép Docker socket, így `web.dockerSocket.enabled=false` (alkalmazás-belsejű felépítő/LocalNode nem elérhető) és `nodeAgent.privileged=false` (ügynök még mindig **önregisztrálódik**; csak nem tud futtatni cTrader konténereket DinD nélkül). Valós munkaterhelés végrehajtásához futtass ügynökök a csomópont készleten ahol `nodeAgent.privileged=true` engedélyezve.

Nincs `helm` bináris? Renderelj és alkalmazz:

```bash
helm template cmind deploy/helm/cmind -f my-values.yaml | kubectl apply -f -
```

## 4. Várakozz a bevezetésre

```bash
kubectl -n cmind get pods -w
kubectl -n cmind rollout status deploy/cmind-web
```

Várakozz: `cmind-web`, `cmind-mcp`, `cmind-postgres` (Deployments) és `cmind-node-agent-0` (StatefulSet) mind `Ready`. Web készültség (`/health`) csak akkor halad amikor DB migrálódott (migrációk futnak indítás előtt).

## 5. Ellenőrizd az auto-felfedezést

```bash
# Csomópont ügynök megjelenik az adatbázisban egy per-pod headless DNS BaseUrl és IsReachable=true
PG=$(kubectl -n cmind get pod -l app.kubernetes.io/component=postgres -o jsonpath='{.items[0].metadata.name}')
kubectl -n cmind exec "$PG" -- psql -U postgres -d appdb -c \
  'SELECT "Name","Kind","IsReachable","BaseUrl" FROM "Nodes";'
```

Példa (ellenőrzött):

```
          Name           |    Kind     | IsReachable |                     BaseUrl
-------------------------+-------------+-------------+-------------------------------------------------
 cmind-node-agent-0      | ActiveMixed | t           | http://cmind-node-agent-0.cmind-node-agent...:8080
```

Méretez kapacitás replikák hozzáadásával — minden új pod önregisztrálódik egy szívverés intervallumban:

```bash
kubectl -n cmind scale statefulset/cmind-node-agent --replicas=3
```

Éltelen egyeztetés (ellenőrzött): méretez ügynök lefelé, csatlakozik az `IsReachable=f`-hez az után `discovery.heartbeatTtl`; méretez vissza felfelé, visszatér online-ra.

## 6. Érj el a UI-hoz

```bash
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# http://localhost:8080  — bejelentkezz a vetett tulajdonosként
```

Külső hozzáférés: állítsd be `web.ingress.enabled=true`, `web.ingress.host`, és TLS.

## Miért csomópont ügynökök egy StatefulSet

A fő csomópont küldi a munkát egy **specifikus** ügynöknek az URL-en, így minden ügynöknek stabil, egyedileg-címezhető DNS neve kell lennie. A diagram StatefulSet + headless Service használ; minden pod meghirdeti `http://<pod>.<svc>.<ns>.svc.cluster.local:8080` és önregisztrálódik a pod név alatt. Ugyanez a felfedezési mechanizmus, amit az eredeti cTrader CLI csomópontok használnak — lásd [../operations/node-discovery.md](../operations/node-discovery.md).

## Web skalázódás (SignalR backplane, S6)

Web alkalmazás = Blazor Server + SignalR (élő irányítópult, naplók hub). **Több, mint egy Web replika** futtatásához, állítsd be a `signalr` csatlakozási karakterláncot a Redis végpontra — az alkalmazás ezután regisztrál egy **SignalR Redis backplane** (`AddStackExchangeRedis`) így a hub üzenetek és áramkör egyeztetés fan-ek az összes replikához és egy újracsatlakozás egy másik pod-ra leszálló maradjon élő. Nincs `signalr` csatlakozási karakterlánc = egy replika memória (megváltozatlan). Párosítsd a munkamenet affinitás-sal az ingress-ben a legsimasabb Blazor Server áramkörökhöz.

## Másolat-ügynök automatikus-méretezés és reziliencia

A másolat-ügynök hosszú élettartamú kereskedelem socketeket üzemeltet, így méretez **munka alapján, nem CPU alapján**. A `copyAgent.keda.enabled=true` diagrammal telepít KEDA `ScaledObject`-et, amely lekérdez Postgres-ot futó másolat-profil számbavételhez és skaláz replika így minden pod üzemeltet körülbelül `copyAgent.keda.profilesPerPod` (alapértelmezés 25), közötti `minReplicas`/`maxReplicas`. A KEDA az adatbázis-t olvas `TriggerAuthentication` által kötött `copyAgent.keda.connectionSecretKey` titkoskulcshoz. Amikor `copyAgent.replicas > 1` (vagy KEDA skálázódik 1-nél több) a diagram is hozzáad `topologySpreadConstraints` (terjedj csomópontok között) és `PodDisruptionBudget` (`minAvailable: 1`); a leskálázódás / gördülő frissítés során minden pod kiadja a lízingeket az `SIGTERM` bejelentésén (`terminationGracePeriodSeconds`, alapértelmezés 30) így a túlélő azonnal visszaigényli — lásd [scaling.md](scaling.md).

## Kulcs értékek

| Érték | Cél |
|-------|---------|
| `image.registry` / `.repository` / `.tag` / `.pullPolicy` | Kép koordináták (`local` + `Never` a kind-hez). |
| `secrets.existingSecret` | Használ külső/lezárt Secret helyett a diagram-kezelt értékek. |
| `postgres.enabled` | `true` = in-cluster Postgres (dev). `false` + `externalDatabase.connectionString` a kezelt adatbázishoz (prod). |
| `web.ingress.*` / `web.autoscaling` / `mcp.autoscaling` | Ingress + TLS, HPA a CPU-n. |
| `nodeAgent.replicas` / `.privileged` / `.mode` / `.maxInstances` | Ügynök szám, DinD kiváltság, mód, kapacitás. |
| `web.dockerSocket.enabled` | hostPath `/var/run/docker.sock` a Web felépítő/LocalNode-hoz (Docker-futási csomópontok csak). |
| `observability.otlpEndpoint` | Szállít naplókat+nyomokat+metrikákat az OTLP gyűjtőhöz. |

## Szondák

liveness `/alive`, readiness `/health` (Web) · `/version` (MCP) · `/health` (ügynök) — képezett az összes környezetben.

## In-cluster teszt csomag

Futtass másolat-kereskedelem csomag mint Kubernetes `Job` a telepített alkalmazás ellen, így regresszió elkapva in-cluster ugyanúgy mint helyileg. Másolat tesztek szükségletek csak Web + Postgres + token cache — **nincs** kiváltságolt csomópont ügynök.

Egy-lövés, reprodukálható (kind felfelé → felépítés+telepítés képek → telepítés → futtass Job → állítsd szerzője exit 0 → szétszedés):

```bash
scripts/k8s-e2e.sh                                   # determinisztikus másolat csomag (nincs titok)
USE_EXISTING_CLUSTER=1 scripts/k8s-e2e.sh            # újrahasznosít jelenlegi kube kontextus
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # élő
```

Kézi / CI vezetékezés — **determinisztikus (alapértelmezés, nincs titok):**

```bash
docker build -f Dockerfile.tests -t cmind-tests:e2e .          # futó kép (SDK + felépített teszt projektek)
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true
kubectl -n cmind wait --for=condition=complete --timeout=15m job/cmind-cmind-tests
kubectl -n cmind logs job/cmind-cmind-tests
```

Az **élő csomag** valamint szükségletei token cache. A cTrader **frissítés tokenek egy-felhasználatú**, így a cache **kell írható legyen**: Job másolat Secret az emptyDir-ből a `/app/secrets` init-container segítségével.

```bash
kubectl -n cmind create secret generic cmind-copy-secrets --from-file=secrets/   # soha nem sütött az képbe
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true \
  --set tests.project='tests/IntegrationTests/IntegrationTests.csproj' \
  --set tests.filter='FullyQualifiedName~CopyTradingLiveTests' \
  --set tests.copySecret=cmind-copy-secrets
```

| Érték | Cél |
|-------|---------|
| `tests.enabled` | Renderelj teszt `Job` (alapértelmezés `false`). |
| `tests.project` / `tests.filter` | Mely projekt + `dotnet test --filter` futtat (alapértelmezés: determinisztikus). |
| `tests.copySecret` | Opcionális Secret a gitignored `openapi-*.local.json`; másolva az **írható** emptyDir-be a `/app/secrets`-hez az élő csomag-hoz. Üres ⇒ nincs titkos hozzácsatlakoztatás. |
| `tests.backoffLimit` | Job újrapróbálkozás szám (alapértelmezés `0`). |

A `LiveCopySecrets` felfelé sétál az `/app`-ből, hogy megleld a `secrets/`-t; élő tesztek kihagyva tisztán amikor cache hiányzik. A `Dockerfile.tests` SDK-alapú így futtat ugyanazok az állítások mint helyi `dotnet test` — mind determinisztikus (`101 passed`) és teljes élő (`8 passed`) csomagok ellenőrzött futási ezt az képet helyileg Docker ellen szállítás előtt.

## Szétszedés

```bash
helm -n cmind uninstall cmind        # vagy: kubectl delete -f <rendered>.yaml
kind delete cluster --name cmind     # helyi csak
```

## Az in-cluster csomag futtatása kereszt-platform (Linux / macOS / Windows / WSL)

A `scripts/k8s-e2e.sh` OS-független. Konvertál repo útvonalat a natív formára (`cygpath -m`) így Docker, helm és kubectl feloldódik a **Windows/git-bash** úgy mint Linux/macOS — ellenőrzött végtelenül a Windowson (kind fürt felfelé → képek felépít+telepít → diagram telepítve → in-cluster teszt Job zöld → szétszedés).

| Környezet | Parancs |
|-------------|---------|
| Linux / macOS | `scripts/k8s-e2e.sh` |
| Windows (git-bash) | `bash scripts/k8s-e2e.sh` **vagy** `pwsh scripts/k8s-e2e.ps1` |
| Windows → **WSL (javasolt)** | `pwsh scripts/k8s-e2e.ps1 -Wsl` |

**Javasolt WSL a Windowson.** Futás az WSL-ben natív Linux útvonalak és Docker Desktop WSL integrációs használ, elkerülve mind az útvonal-fordítás határeseteit — legrobusztus lehetőség. Szükségletei `docker`, `kind`, `helm`, `kubectl` és .NET SDK a WSL PATH-en (Docker Desktop biztosít `docker`; telepítsd a többi a distro-ban, pl. `go install sigs.k8s.io/kind@latest`, a helm/kubectl kiadás binárisok). A `scripts/k8s-e2e.ps1` burkoló felvesz WSL a `-Wsl`-kel, vissza a git-bash-hez egyébként.

A `kind` + `helm` öntelepíthető ha hiányzik (kiadás binárisok vagy `choco install kind kubernetes-helm`); nem kezel mint nem elérhető. Lásd is [../testing/live-copy-trading.md](../testing/live-copy-trading.md).
