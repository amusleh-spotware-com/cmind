---
description: "Helm chart: deploy/helm/cmind สำหรับ Deploy Web MCP self-registering node agents optional in-cluster Postgres"
---

# Kubernetes deployment — step by step

Helm chart: `deploy/helm/cmind` Deploys Web MCP self-registering node agents optional in-cluster Postgres

> **Validated** end-to-end บน local `kind` cluster: ทุก pods ถึง `Ready` node agent self-registers ด้วย per-pod headless DNS name `/health` + `/version` return 200 scaled-down agent auto-marked unreachable Flow below = สิ่งที่ tested

## 0 Prerequisites

- Kubernetes cluster (managed EKS/AKS/GKE หรือ local `kind`/`k3d`/`minikube`)
- `kubectl` (pointed ที่ target context) และ `helm` 3
- Container registry cluster สามารถ pull จาก (skip สำหรับ local `kind` — load images แทน)

## 1 Build images สาม

```bash
docker build -f Dockerfile.web        -t <registry>/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t <registry>/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t <registry>/cmind-node-agent:1.0.0 .
```

Push (`docker push <registry>/cmind-web:1.0.0` เป็นต้น) **หรือ** สำหรับ local `kind` cluster load direct:

```bash
kind create cluster --name cmind
for s in web mcp node-agent; do
  docker tag <registry>/cmind-$s:1.0.0 local/cmind-$s:test
  kind load docker-image local/cmind-$s:test --name cmind
done
```

## 2 Pick secrets

```bash
PG_PASSWORD=$(openssl rand -hex 16)
JOIN_TOKEN=$(openssl rand -hex 24)   # >= 32 chars; shared cluster secret สำหรับ node auto-discovery
```

## 3 Install chart

Registry-based (managed cluster):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --namespace cmind --create-namespace \
  --set image.registry=<registry-host> --set image.repository=<org>/cmind --set image.tag=1.0.0 \
  --set secrets.pgPassword="$PG_PASSWORD" \
  --set secrets.ownerEmail=you@example.com \
  --set secrets.ownerPassword='Change-Me-Str0ng!' \
  --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

Local `kind` (loaded images ไม่มี external Postgres non-privileged agents):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --set image.registry=local --set image.repository=cmind --set image.tag=test --set image.pullPolicy=Never \
  --set web.dockerSocket.enabled=false --set nodeAgent.privileged=false \
  --set secrets.pgPassword="$PG_PASSWORD" --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

> บน `kind`/containerd ไม่มี host Docker socket ดังนั้น `web.dockerSocket.enabled=false` (in-app builder/LocalNode unavailable) และ `nodeAgent.privileged=false` (agent ยังคงทำ **self-registers**; เพียงแค่ไม่สามารถเรียกใช้ cTrader containers โดยไม่มี DinD) สำหรับการดำเนินการ real workload รัน agents บน node pool ที่ `nodeAgent.privileged=true` อนุญาต

ไม่มี `helm` binary? Render และ apply:

```bash
helm template cmind deploy/helm/cmind -f my-values.yaml | kubectl apply -f -
```

## 4 Wait for rollout

```bash
kubectl -n cmind get pods -w
kubectl -n cmind rollout status deploy/cmind-web
```

Expect: `cmind-web` `cmind-mcp` `cmind-postgres` (Deployments) และ `cmind-node-agent-0` (StatefulSet) ทั้งหมด `Ready` Web readiness (`/health`) passes เฉพาะเมื่อ DB ได้รับการ migrate (migrations รันบน startup)

## 5 Verify auto-discovery

```bash
# Node agent ควรปรากฏใน DB ด้วย per-pod headless DNS BaseUrl และ IsReachable=true
PG=$(kubectl -n cmind get pod -l app.kubernetes.io/component=postgres -o jsonpath='{.items[0].metadata.name}')
kubectl -n cmind exec "$PG" -- psql -U postgres -d appdb -c \
  'SELECT "Name","Kind","IsReachable","BaseUrl" FROM "Nodes";'
```

Example (verified):

```
          Name           |    Kind     | IsReachable |                     BaseUrl
-------------------------+-------------+-------------+-------------------------------------------------
 cmind-node-agent-0      | ActiveMixed | t           | http://cmind-node-agent-0.cmind-node-agent...:8080
```

Scale capacity โดยการเพิ่ม replicas — แต่ละ new pod self-registers ภายใน one heartbeat interval:

```bash
kubectl -n cmind scale statefulset/cmind-node-agent --replicas=3
```

Staleness reconciliation (verified): scale agent down flips ไปที่ `IsReachable=f` หลัง `discovery.heartbeatTtl`; scale กลับขึ้น returns online

## 6 Reach UI

```bash
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# http://localhost:8080  — sign in ด้วย seeded owner
```

External access: set `web.ingress.enabled=true` `web.ingress.host` และ TLS

## เหตุใด node agents เป็น StatefulSet

Main node dispatches งานไป **specific** agent โดย URL ดังนั้นแต่ละ agent ต้องการ stable individually-addressable DNS name Chart ใช้ StatefulSet + headless Service; แต่ละ pod advertises `http://<pod>.<svc>.<ns>.svc.cluster.local:8080` และ self-registers ภายใต้ pod name Same discovery mechanism bare cTrader CLI nodes ใช้ — ดู [../operations/node-discovery.md](../operations/node-discovery.md)

## Web scale-out (SignalR backplane S6)

Web app = Blazor Server + SignalR (live dashboard logs hub) เพื่อรัน **มากกว่า one Web replica** ตั้ง `signalr` connection string ไป Redis endpoint — app จากนั้นลงทะเบียน **SignalR Redis backplane** (`AddStackExchangeRedis`) เพื่อให้ hub messages และ circuit negotiation fan ข้าม replicas และ reconnect landing บน different pod stays live ไม่มี `signalr` connection string = single-replica in-memory (unchanged) Pair ด้วย session affinity ที่ ingress สำหรับ smoothest Blazor Server circuits

## Copy-agent autoscaling & resilience

Copy-agent hosts long-lived trading sockets ดังนั้น scales บน **work ไม่ใช่ CPU** ด้วย `copyAgent.keda.enabled=true` chart installs KEDA `ScaledObject` ที่ queries Postgres สำหรับ running copy-profile count และ scales replicas ดังนั้นแต่ละ pod hosts ประมาณ `copyAgent.keda.profilesPerPod` (default 25) ระหว่าง `minReplicas`/`maxReplicas` KEDA reads DB ผ่าน `TriggerAuthentication` bound ไป `copyAgent.keda.connectionSecretKey` secret key เมื่อ `copyAgent.replicas > 1` (หรือ KEDA scales ตัดต่อ 1) chart นอกจากนี้ยังเพิ่ม `topologySpreadConstraints` (spread ข้าม nodes) และ `PodDisruptionBudget` (`minAvailable: 1`); บน scale-in / rolling update แต่ละ pod releases leases บน `SIGTERM` (`terminationGracePeriodSeconds` default 30) ดังนั้น survivor reclaims ทันที — ดู [scaling.md](scaling.md)

## Key values

| Value | Purpose |
|-------|---------|
| `image.registry` / `.repository` / `.tag` / `.pullPolicy` | Image coordinates (`local` + `Never` สำหรับ kind) |
| `secrets.existingSecret` | ใช้ external/sealed Secret แทน chart-managed values |
| `postgres.enabled` | `true` = in-cluster Postgres (dev) `false` + `externalDatabase.connectionString` สำหรับ managed DB (prod) |
| `web.ingress.*` / `web.autoscaling` / `mcp.autoscaling` | Ingress + TLS HPA บน CPU |
| `nodeAgent.replicas` / `.privileged` / `.mode` / `.maxInstances` | Agent count DinD privilege mode capacity |
| `web.dockerSocket.enabled` | hostPath `/var/run/docker.sock` สำหรับ Web builder/LocalNode (Docker-runtime nodes เท่านั้น) |
| `observability.otlpEndpoint` | Ship logs+traces+metrics ไป OTLP collector |

## Probes

liveness `/alive` readiness `/health` (Web) · `/version` (MCP) · `/health` (agent) — mapped ในทุก environments

## In-cluster test suite

Run copy-trading suite เป็น Kubernetes `Job` ตรวจสอบ deployed app ดังนั้น regression caught in-cluster เหมือนกับ locally Copy tests ต้องการเพียง Web + Postgres + token cache — **ไม่มี** privileged node agents

One-shot reproducible (kind up → build+load images → deploy → run Job → assert exit 0 → tear down):

```bash
scripts/k8s-e2e.sh                                   # deterministic copy suite (ไม่มี secrets)
USE_EXISTING_CLUSTER=1 scripts/k8s-e2e.sh            # reuse current kube context
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # live
```

Manual / CI wiring — **deterministic (default no secrets):**

```bash
docker build -f Dockerfile.tests -t cmind-tests:e2e .          # runner image (SDK + built test projects)
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true
kubectl -n cmind wait --for=condition=complete --timeout=15m job/cmind-cmind-tests
kubectl -n cmind logs job/cmind-cmind-tests
```

**Live suite** นอกจากนี้ยังต้อง token cache cTrader **refresh tokens single-use** ดังนั้น cache ต้อง **writable**: Job copies Secret เข้าไป emptyDir ที่ `/app/secrets` ผ่าน init-container

```bash
kubectl -n cmind create secret generic cmind-copy-secrets --from-file=secrets/   # ไม่เคยอบแห้งเข้าไปในอิมเมจ
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true \
  --set tests.project='tests/IntegrationTests/IntegrationTests.csproj' \
  --set tests.filter='FullyQualifiedName~CopyTradingLiveTests' \
  --set tests.copySecret=cmind-copy-secrets
```

| Value | Purpose |
|-------|---------|
| `tests.enabled` | Render test `Job` (default `false`) |
| `tests.project` / `tests.filter` | ที่โครงการใด + `dotnet test --filter` จะรัน (default: deterministic) |
| `tests.copySecret` | Optional Secret ด้วย gitignored `openapi-*.local.json`; copied เข้าไป **writable** emptyDir ที่ `/app/secrets` สำหรับ live suite Empty ⇒ ไม่มี secret mount |
| `tests.backoffLimit` | Job retry count (default `0`) |

`LiveCopySecrets` เดิน ขึ้น จาก `/app` เพื่อค้นหา `secrets/`; live tests skip อย่างสะอาดเมื่อ cache absent `Dockerfile.tests` SDK-based ดังนั้นทำงาน assertions เดียวกันเป็น local `dotnet test` — ทั้ง deterministic (`101 passed`) และ full live (`8 passed`) suites verified running ภายในอิมเมจนี้ locally ตรวจสอบ Docker ก่อน shipping

## Teardown

```bash
helm -n cmind uninstall cmind        # or: kubectl delete -f <rendered>.yaml
kind delete cluster --name cmind     # local เท่านั้น
```

## Running in-cluster suite cross-platform (Linux / macOS / Windows / WSL)

`scripts/k8s-e2e.sh` OS-independent Converts repo path ไปรูปแบบ native (`cygpath -m`) ดังนั้น Docker helm และ kubectl resolve มัน บน **Windows/git-bash** เช่นเดียวกับ Linux/macOS — verified end ไปจบบน Windows (kind cluster up → images built+loaded → chart deployed → in-cluster test Job green → teardown)

| Environment | Command |
|-------------|---------|
| Linux / macOS | `scripts/k8s-e2e.sh` |
| Windows (git-bash) | `bash scripts/k8s-e2e.sh` **หรือ** `pwsh scripts/k8s-e2e.ps1` |
| Windows → **WSL (preferred)** | `pwsh scripts/k8s-e2e.ps1 -Wsl` |

**Prefer WSL บน Windows** Running ภายใน WSL ใช้ native Linux paths และ Docker Desktop WSL integration หลีกเลี่ยง path-translation edge cases ทั้งหมด — ตัวเลือกที่ robust ที่สุด Needs `docker` `kind` `helm` `kubectl` และ .NET SDK บน WSL PATH (Docker Desktop จัดให้; ติดตั้ง rest ใน distro เช่น `go install sigs.k8s.io/kind@latest` helm/kubectl release binaries) `scripts/k8s-e2e.ps1` wrapper picks WSL ด้วย `-Wsl` falls back ไป git-bash อื่น

`kind` + `helm` self-installable หากไม่มี (release binaries หรือ `choco install kind kubernetes-helm`); อย่าปฏิบัติเป็น unavailable ดู [../testing/live-copy-trading.md](../testing/live-copy-trading.md)
