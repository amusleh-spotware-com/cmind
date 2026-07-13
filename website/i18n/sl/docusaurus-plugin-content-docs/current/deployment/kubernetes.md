---
description: "Helm chart: deploy/helm/cmind. Namešča Web, MCP, samo-registrirajoče se agentske vozliške agente, izbirno znotraj-gručnega Postgres."
---

# Nameščanje Kubernetes — korak za korakom

Helm chart: `deploy/helm/cmind`. Namešča Web, MCP, samo-registrirajoče se agentske vozliške agente, izbirno
znotraj-gručnega Postgres.

> **Validirano** end-to-end na lokalnem `kind` gruči: vsi pod-i dosežejo `Ready`, agentsko vozlišče
> samo-registrira s per-pod DNS imenom brez glavnega, `/health` + `/version` vrneta 200, pomanjšano
> agentsko vozlišče avtomatsko označeno nedosegljivo. Potek spodaj = kar je testirano.

## 0. Priprave

- Kubernetes gruča (managed EKS/AKS/GKE, ali lokalni `kind`/`k3d`/`minikube`).
- `kubectl` (usmerjen na ciljni kontekst) in `helm` 3.
- Container registry gruča lahko vleče iz (preskočite za lokalni `kind` — namesto tega naložite slike).

## 1. Zgradite tri slike

```bash
docker build -f Dockerfile.web        -t <registry>/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t <registry>/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t <registry>/cmind-node-agent:1.0.0 .
```

Potisnite (`docker push <registry>/cmind-web:1.0.0`, itd.), **ali** za lokalni `kind` gruča naložite
naravnost:

```bash
kind create cluster --name cmind
for s in web mcp node-agent; do
  docker tag <registry>/cmind-$s:1.0.0 local/cmind-$s:test
  kind load docker-image local/cmind-$s:test --name cmind
done
```

## 2. Izberite skrivnosti

```bash
PG_PASSWORD=$(openssl rand -hex 16)
JOIN_TOKEN=$(openssl rand -hex 24)   # >= 32 znakov; deljena gručna skrivnost za avto-odkritje vozlišča
```

## 3. Namestite chart

Registry-based (managed gruča):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --namespace cmind --create-namespace \
  --set image.registry=<registry-host> --set image.repository=<org>/cmind --set image.tag=1.0.0 \
  --set secrets.pgPassword="$PG_PASSWORD" \
  --set secrets.ownerEmail=you@example.com \
  --set secrets.ownerPassword='Change-Me-Str0ng!' \
  --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

Lokalni `kind` (nanesene slike, zunanja Postgres, neprivilegirani agenti):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --set image.registry=local --set image.repository=cmind --set image.tag=test --set image.pullPolicy=Never \
  --set web.dockerSocket.enabled=false --set nodeAgent.privileged=false \
  --set secrets.pgPassword="$PG_PASSWORD" --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

> Na `kind`/containerd ni gostiteljskega Docker vtiča, torej `web.dockerSocket.enabled=false`
> (in-app gradilnik/LocalNode nedosegljiv) in `nodeAgent.privileged=false` (agent še vedno
> **samo-registrira**; samo ne more zagnati cTrader containerjev brez DinD). Za resnično delovno
> obremenitev zaženite agente na vozliščnem poolu kjer je `nodeAgent.privileged=true` dovoljeno.

Ni `helm` binarnega? Renderirajte in uporabite:

```bash
helm template cmind deploy/helm/cmind -f my-values.yaml | kubectl apply -f -
```

## 4. Počakajte na rollout

```bash
kubectl -n cmind get pods -w
kubectl -n cmind rollout status deploy/cmind-web
```

Pričakuj: `cmind-web`, `cmind-mcp`, `cmind-postgres` (Deployments) in `cmind-node-agent-0`
(StatefulSet) vsi `Ready`. Web readiness (`/health`) preide šele ko je zbirka podatkov migrirana (migracije
tečejo ob zagonu).

## 5. Preverite avto-odkritje

```bash
# Agentsko vozlišče bi moralo biti v zbirki podatkov s per-pod headless DNS BaseUrl in IsReachable=true
PG=$(kubectl -n cmind get pod -l app.kubernetes.io/component=postgres -o jsonpath='{.items[0].metadata.name}')
kubectl -n cmind exec "$PG" -- psql -U postgres -d appdb -c \
  'SELECT "Name","Kind","IsReachable","BaseUrl" FROM "Nodes";'
```

Primer (preverjeno):

```
          Name           |    Kind     | IsReachable |                     BaseUrl
-------------------------+-------------+-------------+-------------------------------------------------
 cmind-node-agent-0      | ActiveMixed | t           | http://cmind-node-agent-0.cmind-node-agent...:8080
```

Povečajte kapaciteto z dodajanjem replik — vsak nov pod se samo-registrira znotraj enega intervala srčnega utripa:

```bash
kubectl -n cmind scale statefulset/cmind-node-agent --replicas=3
```

Rekonsiliacija zastarelosti (preverjeno): pomanjšajte agent, preklopi na `IsReachable=f` po
`discovery.heartbeatTtl`; povečaj nazaj, se vrne v spletu.

## 6. Dosedite UI

```bash
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# http://localhost:8080  — prijavite se z natančeno lastnikom
```

Zunanja dostopnost: nastavite `web.ingress.enabled=true`, `web.ingress.host`, in TLS.

## Zakaj so agentska vozlišča StatefulSet

Glavno vozlišče pošilja delo na **specifičnega** agenta po URL, torej vsak agent potrebuje stabilno,
posamično naslovljivo DNS ime. Chart uporablja StatefulSet + headless Service; vsak pod
oglašuje `http://<pod>.<svc>.<ns>.svc.cluster.local:8080` in se samo-registrira pod imenom pod.
Identčen mehanizem odkritja kot ga uporabljajo goli cTrader CLI vozlišča —
glej [../operations/node-discovery.md](../operations/node-discovery.md).

## Web scale-out (SignalR backplane, S6)

Web aplikacija = Blazor Server + SignalR (živa nadzorna plošča, logs hub). Za zagon **več kot enega Web replica**,
nastavite `signalr` connection string na Redis endpoint — app nato registrira **SignalR Redis
backplane** (`AddStackExchangeRedis`) tako da hub sporočila in pogajanja vezij fan-out čez replike in
ponovna povezava ki pristane na drugem pod-u ostane živa. Brez `signalr` connection string = eno-replica
in-memory (nespremenjeno). Združite s afiniteto seje pri ingressu za najglajše Blazor Server vezije.

## Copy-agent avtomatsko skaliranje in odpornost

Copy-agent gostuje dolgožive trgovalne vtiče, torej skalira na **delo, ne CPU**. Z
`copyAgent.keda.enabled=true` chart namesti KEDA `ScaledObject` ki poizveduje Postgres za
števco tekočih profilov kopiranja in skalira replike tako da vsak pod gostuje okoli `copyAgent.keda.profilesPerPod`
(privzeto 25), med `minReplicas`/`maxReplicas`. KEDA bere DB prek `TriggerAuthentication` vezane na
`copyAgent.keda.connectionSecretKey` skrivnostni ključ. Ko `copyAgent.replicas > 1` (ali KEDA skalira čez 1)
chart prav tako doda `topologySpreadConstraints` (razporejanje čez vozlišča) in `PodDisruptionBudget`
(`minAvailable: 1`); ob scale-in / rolling update vsak pod sprosti lease na `SIGTERM`
(`terminationGracePeriodSeconds`, privzeto 30) tako da preživeli nemudoma prevzame — glej
[scaling.md](scaling.md).

## Ključne vrednosti

| Vrednost | Namen |
|---------|-------|
| `image.registry` / `.repository` / `.tag` / `.pullPolicy` | Koordinate slike (`local` + `Never` za kind). |
| `secrets.existingSecret` | Uporabi zunanjo/zapečateno Secret namesto chart-upravljanih vrednosti. |
| `postgres.enabled` | `true` = znotraj-gručni Postgres (dev). `false` + `externalDatabase.connectionString` za upravljano DB (prod). |
| `web.ingress.*` / `web.autoscaling` / `mcp.autoscaling` | Ingress + TLS, HPA na CPU. |
| `nodeAgent.replicas` / `.privileged` / `.mode` / `.maxInstances` | Število agentov, DinD privilegij, način, kapaciteta. |
| `web.dockerSocket.enabled` | hostPath `/var/run/docker.sock` za Web gradilnik/LocalNode (Docker-runtime vozlišča samo). |
| `observability.otlpEndpoint` | Ladi dnevnike+sledi+metrike v OTLP zbiralec. |

## Sonde

liveness `/alive`, readiness `/health` (Web) · `/version` (MCP) · `/health` (agent) — preslikane v vseh
okoljih.

## Znotraj-gručni test suite

Zaženi copy-trading suite kot Kubernetes `Job` proti nameščeni aplikaciji, torej se regresija ujame
znotraj-gručno enako kot lokalno. Copy testi potrebujejo samo Web + Postgres + predpomnilnik žetona — **ni**
privilegiranih agentskih vozlišč.

Enkratno, ponovljivo (kind gor → zgradi+nanesi slike → namešča → zaženi Job → trdi exit 0 → razstavi):

```bash
scripts/k8s-e2e.sh                                   # deterministični copy suite (brez skrivnosti)
USE_EXISTING_CLUSTER=1 scripts/k8s-e2e.sh            # znova uporabi trenutni kube kontekst
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # live
```

Ročna / CI ožičenost — **deterministični (privzeto, brez skrivnosti):**

```bash
docker build -f Dockerfile.tests -t cmind-tests:e2e .          # runner slika (SDK + zgrajeni testni projekti)
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true
kubectl -n cmind wait --for=condition=complete --timeout=15m job/cmind-cmind-tests
kubectl -n cmind logs job/cmind-cmind-tests
```

**Live suite** dodatno potrebuje predpomnilnik žetona. cTrader **osveževalni žetoni so enojne-rabe**, torej predpomnilnik
mora biti **spremenljiv**: Job kopira Secret v emptyDir na `/app/secrets` prek init-container.

```bash
kubectl -n cmind create secret generic cmind-copy-secrets --from-file=secrets/   # nikoli ne pečeno v sliko
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true \
  --set tests.project='tests/IntegrationTests/IntegrationTests.csproj' \
  --set tests.filter='FullyQualifiedName~CopyTradingLiveTests' \
  --set tests.copySecret=cmind-copy-secrets
```

| Vrednost | Namen |
|---------|-------|
| `tests.enabled` | Renderiraj testni `Job` (privzeto `false`). |
| `tests.project` / `tests.filter` | Kateri projekt + `dotnet test --filter` za zagnati (privzeto: deterministični). |
| `tests.copySecret` | Izbirna Secret z gitignored `openapi-*.local.json`; kopirano v **spremenljiv** emptyDir na `/app/secrets` za live suite. Prazen ⇒ brez skrivnostnega mounta. |
| `tests.backoffLimit` | Število ponovitev opravila (privzeto `0`). |

`LiveCopySecrets` hodi navzgor od `/app` da najde `secrets/`; live testi preskočijo gladko ko predpomnilnik
manjka. `Dockerfile.tests` SDK-based torej teče iste trditve kot lokalni `dotnet test` — oba
deterministični (`101 passed`) in polni live (`8 passed`) suite preverjena da tečeta znotraj te
slike lokalno proti Docker preden ladi.

## Razstavljanje

```bash
helm -n cmind uninstall cmind        # ali: kubectl delete -f <rendered>.yaml
kind delete cluster --name cmind     # samo lokalno
```

## Zagon znotraj-gručnega suite na več platformah (Linux / macOS / Windows / WSL)

`scripts/k8s-e2e.sh` OS-neodvisen. Pretvori repo pot v nativo obliko (`cygpath -m`) tako Docker,
helm in kubectl razrešijo na **Windows/git-bash** kot Linux/macOS — preverjeno end-to-end na Windows
(kind gruča gor → slike zgrajene+nanesene → chart nameščen → znotraj-gručni test Job zelen → razstavi).

| Okolje | Ukaz |
|---------|------|
| Linux / macOS | `scripts/k8s-e2e.sh` |
| Windows (git-bash) | `bash scripts/k8s-e2e.sh` **ali** `pwsh scripts/k8s-e2e.ps1` |
| Windows → **WSL (priporočeno)** | `pwsh scripts/k8s-e2e.ps1 -Wsl` |

**Raje WSL na Windows.** Zagon znotraj WSL uporablja native Linux poti in Docker Desktop's WSL integracijo,
izogibanje vsem robnim primerom prevajanja poti — najrobustnejša opcija. Potrebuje `docker`, `kind`, `helm`,
`kubectl` in .NET SDK na WSL PATH (Docker Desktop ponudi `docker`; namesti ostalo v distribuciji,
npr. `go install sigs.k8s.io/kind@latest`, helm/kubectl release binarne datoteke). `scripts/k8s-e2e.ps1`
wrapper izbere WSL z `-Wsl`, sicer pade nazaj na git-bash.

`kind` + `helm` samo-namestljiva če manjkata (release binarne datoteke ali `choco install kind kubernetes-helm`);
ne obravnavajta kot nedosegljiva. Glej tud [../testing/live-copy-trading.md](../testing/live-copy-trading.md).
