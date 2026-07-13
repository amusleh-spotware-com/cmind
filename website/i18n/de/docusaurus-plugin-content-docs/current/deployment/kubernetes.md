---
description: "Helm-Chart: deploy/helm/cmind. Stellt Web, MCP, selbst-registrierende Node-Agents bereit, optional In-Cluster Postgres."
---

# Kubernetes-Bereitstellung – Schritt für Schritt

Helm-Chart: `deploy/helm/cmind`. Stellt Web, MCP, selbst-registrierende Node-Agents bereit, optional In-Cluster Postgres.

> **Validiert** End-to-End auf lokalem `kind`-Cluster: alle Pods erreichen `Ready`, Node-Agent registriert sich selbst mit per-Pod Headless DNS-Name, `/health` + `/version` geben 200 zurück, herunterskalierter Agent automatisch markiert unerreichbar. Flow unten = was getestet wurde.

## 0. Voraussetzungen

- Kubernetes-Cluster (verwaltete EKS/AKS/GKE, oder lokale `kind`/`k3d`/`minikube`).
- `kubectl` (auf Ziel-Kontext zeigen) und `helm` 3.
- Container-Registrierung, die Cluster ziehen kann (Skip für lokale `kind` – lade Bilder stattdessen).

## 1. Baue die drei Bilder

```bash
docker build -f Dockerfile.web        -t <registry>/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t <registry>/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t <registry>/cmind-node-agent:1.0.0 .
```

Push (`docker push <registry>/cmind-web:1.0.0`, etc.), **oder** für lokale `kind` Cluster lade direkt:

```bash
kind create cluster --name cmind
for s in web mcp node-agent; do
  docker tag <registry>/cmind-$s:1.0.0 local/cmind-$s:test
  kind load docker-image local/cmind-$s:test --name cmind
done
```

## 2. Wähle Secrets

```bash
PG_PASSWORD=$(openssl rand -hex 16)
JOIN_TOKEN=$(openssl rand -hex 24)   # >= 32 chars; gemeinsames Cluster-Secret für Node Auto-Discovery
```

## 3. Installiere den Chart

Registrierungs-basiert (verwaltete Cluster):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --namespace cmind --create-namespace \
  --set image.registry=<registry-host> --set image.repository=<org>/cmind --set image.tag=1.0.0 \
  --set secrets.pgPassword="$PG_PASSWORD" \
  --set secrets.ownerEmail=you@example.com \
  --set secrets.ownerPassword='Change-Me-Str0ng!' \
  --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

Lokal `kind` (geladene Bilder, keine externe Postgres, unprivilegierte Agents):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --set image.registry=local --set image.repository=cmind --set image.tag=test --set image.pullPolicy=Never \
  --set web.dockerSocket.enabled=false --set nodeAgent.privileged=false \
  --set secrets.pgPassword="$PG_PASSWORD" --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

> Auf `kind`/containerd kein Host Docker-Socket, daher `web.dockerSocket.enabled=false` (In-App Builder/LocalNode unerreichbar) und `nodeAgent.privileged=false` (Agent **registriert sich immer noch selbst**; kann nur cTrader-Container ohne DinD nicht ausführen). Für echte Workload-Ausführung, führe Agents auf Node-Pool aus, wo `nodeAgent.privileged=true` erlaubt ist.

Kein `helm`-Binary? Render und anwenden:

```bash
helm template cmind deploy/helm/cmind -f my-values.yaml | kubectl apply -f -
```

## 4. Warte auf Rollout

```bash
kubectl -n cmind get pods -w
kubectl -n cmind rollout status deploy/cmind-web
```

Erwarte: `cmind-web`, `cmind-mcp`, `cmind-postgres` (Deployments) und `cmind-node-agent-0` (StatefulSet) alle `Ready`. Web Readiness (`/health`) geht nur durch, wenn DB migriert (Migrationen laufen beim Start).

## 5. Überprüfe Auto-Discovery

```bash
# Node-Agent sollte in der DB mit per-Pod Headless DNS BaseUrl und IsReachable=true erscheinen
PG=$(kubectl -n cmind get pod -l app.kubernetes.io/component=postgres -o jsonpath='{.items[0].metadata.name}')
kubectl -n cmind exec "$PG" -- psql -U postgres -d appdb -c \
  'SELECT "Name","Kind","IsReachable","BaseUrl" FROM "Nodes";'
```

Beispiel (Verifiziert):

```
          Name           |    Kind     | IsReachable |                     BaseUrl
-------------------------+-------------+-------------+-------------------------------------------------
 cmind-node-agent-0      | ActiveMixed | t           | http://cmind-node-agent-0.cmind-node-agent...:8080
```

Skaliere Kapazität durch Hinzufügen von Replicas – jeder neue Pod registriert sich selbst innerhalb eines Heartbeat-Intervalls:

```bash
kubectl -n cmind scale statefulset/cmind-node-agent --replicas=3
```

Staleness-Reconciliation (Verifiziert): Skaliere Agent herunter, schaltet zu `IsReachable=f` nach `discovery.heartbeatTtl`; skaliere zurück, kommt online.

## 6. Erreiche die UI

```bash
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# http://localhost:8080  – sign in mit dem gesät Owner
```

Externer Zugriff: setze `web.ingress.enabled=true`, `web.ingress.host` und TLS.

## Warum Node-Agents ein StatefulSet sind

Main Node versendet Arbeit an **spezifischen** Agent nach URL, daher braucht jeder Agent stabile, individuell adressierbare DNS-Name. Chart verwendet StatefulSet + Headless Service; jeder Pod advertised `http://<pod>.<svc>.<ns>.svc.cluster.local:8080` und registriert sich selbst unter Pod-Name. Gleicher Discovery-Mechanismus, den nackte cTrader CLI Nodes verwenden – siehe [../operations/node-discovery.md](../operations/node-discovery.md).

## Web-Skalierung (SignalR Backplane, S6)

Web-App = Blazor Server + SignalR (Live-Dashboard, Logs-Hub). Um **mehr als ein Web-Replica** auszuführen, setze `signalr`-Verbindungsstring zu Redis-Endpoint – App registriert dann **SignalR Redis Backplane** (`AddStackExchangeRedis`), daher Fan-Hub-Nachrichten und Circuit-Verhandlung über Replicas und ein Reconnect Landen auf anderer Pod bleibt live. Nein `signalr` Verbindungsstring = einzelne Replica In-Speicher (Unverändert). Koppelt mit Session-Affinität an Ingress für sanftesten Blazor-Server-Circuits.

## Copy-Agent Autoskalierung & Resilienz

Copy-Agent hostet langlebige Trading-Sockets, daher skaliert auf **Arbeit, nicht CPU**. Mit `copyAgent.keda.enabled=true` Chart installiert KEDA `ScaledObject`, das Postgres für laufende Copy-Profil-Count abfragt und Replicas skaliert, daher hostet jeder Pod etwa `copyAgent.keda.profilesPerPod` (Standard 25), zwischen `minReplicas`/`maxReplicas`. KEDA liest DB über `TriggerAuthentication` gebunden zu `copyAgent.keda.connectionSecretKey`-Secret-Key. Wenn `copyAgent.replicas > 1` (oder KEDA skaliert über 1) Chart auch adds `topologySpreadConstraints` (spread über Nodes) und `PodDisruptionBudget` (`minAvailable: 1`); auf Scale-In / Rolling Update gibt jeder Pod Leases auf `SIGTERM` frei (`terminationGracePeriodSeconds`, Standard 30), daher Überlebende reclaimed sofort – siehe [scaling.md](scaling.md).

## Schlüssel-Werte

| Wert | Zweck |
|-------|---------|
| `image.registry` / `.repository` / `.tag` / `.pullPolicy` | Bild-Koordinaten (`local` + `Never` für kind). |
| `secrets.existingSecret` | Verwende extern/versiegelt Secret anstatt Chart-verwaltete Werte. |
| `postgres.enabled` | `true` = In-Cluster Postgres (Dev). `false` + `externalDatabase.connectionString` für verwaltete DB (Prod). |
| `web.ingress.*` / `web.autoscaling` / `mcp.autoscaling` | Ingress + TLS, HPA auf CPU. |
| `nodeAgent.replicas` / `.privileged` / `.mode` / `.maxInstances` | Agent-Zahl, DinD-Privileg, Mode, Kapazität. |
| `web.dockerSocket.enabled` | hostPath `/var/run/docker.sock` für Web Builder/LocalNode (nur Docker-Runtime Nodes). |
| `observability.otlpEndpoint` | Verschiffen Logs+Traces+Metrics zu OTLP-Collector. |

## Probes

liveness `/alive`, readiness `/health` (Web) · `/version` (MCP) · `/health` (Agent) – gemappt in allen Umgebungen.

## In-Cluster-Test-Suite

Führe Copy-Trading-Suite als Kubernetes `Job` gegen bereistete App aus, daher werden Regressions im-Cluster genauso wie lokal gefangen. Copy-Tests brauchen nur Web + Postgres + Token-Cache – **keine** privilegierten Node-Agents.

One-Shot, reproduzierbar (kind up → build+load Bilder → deploy → führe Job aus → assert exit 0 → tear down):

```bash
scripts/k8s-e2e.sh                                   # deterministische Copy-Suite (keine Secrets)
USE_EXISTING_CLUSTER=1 scripts/k8s-e2e.sh            # wiederverwendung aktueller Kube-Kontext
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # Live
```

Manuell / CI-Verdrahtung – **Deterministisch (Standard, keine Secrets):**

```bash
docker build -f Dockerfile.tests -t cmind-tests:e2e .          # Runner-Bild (SDK + gebaute Test-Projekte)
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true
kubectl -n cmind wait --for=condition=complete --timeout=15m job/cmind-cmind-tests
kubectl -n cmind logs job/cmind-cmind-tests
```

**Live-Suite** benötigt zusätzlich Token-Cache. cTrader **Refresh-Tokens Single-Use**, daher muss Cache **schreibbar** sein: Job kopiert Secret in emptyDir bei `/app/secrets` über Init-Container.

```bash
kubectl -n cmind create secret generic cmind-copy-secrets --from-file=secrets/   # nie in das Bild gebakken
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true \
  --set tests.project='tests/IntegrationTests/IntegrationTests.csproj' \
  --set tests.filter='FullyQualifiedName~CopyTradingLiveTests' \
  --set tests.copySecret=cmind-copy-secrets
```

| Wert | Zweck |
|-------|---------|
| `tests.enabled` | Render Test `Job` (Standard `false`). |
| `tests.project` / `tests.filter` | Welches Projekt + `dotnet test --filter` zu laufen (Standard: Deterministisch). |
| `tests.copySecret` | Optional Secret mit gitignored `openapi-*.local.json`; kopiert in **schreibbar** emptyDir bei `/app/secrets` für Live-Suite. Leer ⇒ kein Secret-Mount. |
| `tests.backoffLimit` | Job Retry-Zahl (Standard `0`). |

`LiveCopySecrets` geht nach oben von `/app`, um `secrets/` zu finden; Live-Tests skippen clean, wenn Cache abwesend. `Dockerfile.tests` SDK-basiert, daher läuft gleiche Assertions wie lokale `dotnet test` – beide deterministisch (`101 bestanden`) und volle Live (`8 bestanden`) Suiten verifiziert Laufen innerhalb dieses Bildes lokal gegen Docker vor Versand.

## Teardown

```bash
helm -n cmind uninstall cmind        # oder: kubectl delete -f <rendered>.yaml
kind delete cluster --name cmind     # nur lokal
```

## Laufen der In-Cluster-Suite Cross-Platform (Linux / macOS / Windows / WSL)

`scripts/k8s-e2e.sh` OS-unabhängig. Konvertiert Repo-Pfad in native Form (`cygpath -m`), daher Docker, helm und kubectl resolves es auf **Windows/Git-Bash** genauso wie Linux/macOS – verifiziert End-to-End auf Windows (Kind-Cluster auf → Bilder gebaut+geladen → Chart bereitgestellt → In-Cluster-Test-Job grün → Teardown).

| Umgebung | Befehl |
|-------------|---------|
| Linux / macOS | `scripts/k8s-e2e.sh` |
| Windows (Git-Bash) | `bash scripts/k8s-e2e.sh` **oder** `pwsh scripts/k8s-e2e.ps1` |
| Windows → **WSL (bevorzugt)** | `pwsh scripts/k8s-e2e.ps1 -Wsl` |

**Bevorzuge WSL auf Windows.** Laufen innerhalb WSL verwendet native Linux-Pfade und Docker Desktop's WSL-Integration, vermeidet alle Path-Translation-Randkanten – robusteste Option. Braucht `docker`, `kind`, `helm`, `kubectl` und .NET SDK auf WSL PATH (Docker Desktop stellt `docker` bereit; installiere Rest in Distro, z.B. `go install sigs.k8s.io/kind@latest`, die Helm/Kubectl Release-Binaries). `scripts/k8s-e2e.ps1`-Wrapper wählt WSL mit `-Wsl`, fällt ansonsten zu Git-Bash zurück.

`kind` + `helm` selbst-installierbar, falls abwesend (Release-Binaries oder `choco install kind kubernetes-helm`); nicht als unerreichbar behandeln. Siehe auch [../testing/live-copy-trading.md](../testing/live-copy-trading.md).
