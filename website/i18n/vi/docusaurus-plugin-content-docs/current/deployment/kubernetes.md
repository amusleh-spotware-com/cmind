---
description: "Helm chart: deploy/helm/cmind. Triển khai Web, MCP, self-registering node agents, optional in-cluster Postgres."
---

# Triển khai Kubernetes — từng bước

Helm chart: `deploy/helm/cmind`. Triển khai Web, MCP, self-registering node agents, optional in-cluster Postgres.

> **Được xác nhận** end-to-end trên local `kind` cluster: tất cả pods đạt `Ready`, node agent tự đăng ký với per-pod headless DNS name, `/health` + `/version` trả về 200, scaled-down agent được đánh dấu là unreachable. Flow dưới = những gì được test.

## 0. Điều kiện tiên quyết

- Kubernetes cluster (managed EKS/AKS/GKE, hoặc local `kind`/`k3d`/`minikube`).
- `kubectl` (trỏ tới target context) và `helm` 3.
- Container registry mà cluster có thể pull từ (bỏ qua cho local `kind` — load images thay vào đó).

## 1. Build ba images

```bash
docker build -f Dockerfile.web        -t <registry>/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t <registry>/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t <registry>/cmind-node-agent:1.0.0 .
```

Push (`docker push <registry>/cmind-web:1.0.0`, v.v.), **hoặc** cho local `kind` cluster load trực tiếp:

```bash
kind create cluster --name cmind
for s in web mcp node-agent; do
  docker tag <registry>/cmind-$s:1.0.0 local/cmind-$s:test
  kind load docker-image local/cmind-$s:test --name cmind
done
```

## 2. Chọn secrets

```bash
PG_PASSWORD=$(openssl rand -hex 16)
JOIN_TOKEN=$(openssl rand -hex 24)   # >= 32 chars; shared cluster secret cho node auto-discovery
```

## 3. Cài đặt chart

Dựa trên Registry (managed cluster):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --namespace cmind --create-namespace \
  --set image.registry=<registry-host> --set image.repository=<org>/cmind --set image.tag=1.0.0 \
  --set secrets.pgPassword="$PG_PASSWORD" \
  --set secrets.ownerEmail=you@example.com \
  --set secrets.ownerPassword='Change-Me-Str0ng!' \
  --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

Local `kind` (loaded images, không external Postgres, non-privileged agents):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --set image.registry=local --set image.repository=cmind --set image.tag=test --set image.pullPolicy=Never \
  --set web.dockerSocket.enabled=false --set nodeAgent.privileged=false \
  --set secrets.pgPassword="$PG_PASSWORD" --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

> Trên `kind`/containerd không có host Docker socket, nên `web.dockerSocket.enabled=false` (in-app builder/LocalNode không có sẵn) và `nodeAgent.privileged=false` (agent vẫn **tự đăng ký**; chỉ không thể chạy cTrader containers mà không có DinD). Để thực hiện khối lượng công việc thực sự, chạy agents trên node pool nơi `nodeAgent.privileged=true` được cho phép.

Không có `helm` binary? Render và apply:

```bash
helm template cmind deploy/helm/cmind -f my-values.yaml | kubectl apply -f -
```

## 4. Chờ rollout

```bash
kubectl -n cmind get pods -w
kubectl -n cmind rollout status deploy/cmind-web
```

Kỳ vọng: `cmind-web`, `cmind-mcp`, `cmind-postgres` (Deployments) và `cmind-node-agent-0` (StatefulSet) tất cả `Ready`. Web readiness (`/health`) chỉ vượt qua sau khi DB migrated (migrations chạy trên startup).

## 5. Xác minh auto-discovery

```bash
# Node agent sẽ xuất hiện trong DB với per-pod headless DNS BaseUrl và IsReachable=true
PG=$(kubectl -n cmind get pod -l app.kubernetes.io/component=postgres -o jsonpath='{.items[0].metadata.name}')
kubectl -n cmind exec "$PG" -- psql -U postgres -d appdb -c \
  'SELECT "Name","Kind","IsReachable","BaseUrl" FROM "Nodes";'
```

Ví dụ (được xác minh):

```
          Name           |    Kind     | IsReachable |                     BaseUrl
-------------------------+-------------+-------------+-------------------------------------------------
 cmind-node-agent-0      | ActiveMixed | t           | http://cmind-node-agent-0.cmind-node-agent...:8080
```

Scale capacity bằng cách thêm replicas — mỗi pod mới tự đăng ký trong vòng một heartbeat interval:

```bash
kubectl -n cmind scale statefulset/cmind-node-agent --replicas=3
```

Staleness reconciliation (được xác minh): scale agent xuống, chuyển sang `IsReachable=f` sau `discovery.heartbeatTtl`; scale trở lại, quay trở lại trực tuyến.

## 6. Truy cập UI

```bash
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# http://localhost:8080  — đăng nhập với seeded owner
```

External access: đặt `web.ingress.enabled=true`, `web.ingress.host`, và TLS.

## Tại sao node agents là StatefulSet

Main node dispatches công việc tới **specific** agent theo URL, nên mỗi agent cần stable, individually-addressable DNS name. Chart sử dụng StatefulSet + headless Service; mỗi pod quảng cáo `http://<pod>.<svc>.<ns>.svc.cluster.local:8080` và tự đăng ký với pod name.
Cơ chế discovery giống như bare cTrader CLI nodes sử dụng — xem [../operations/node-discovery.md](../operations/node-discovery.md).

## Web scale-out (SignalR backplane, S6)

Web app = Blazor Server + SignalR (live dashboard, logs hub). Để chạy **hơn một Web replica**, đặt `signalr` connection string tới Redis endpoint — ứng dụng sau đó đăng ký **SignalR Redis backplane** (`AddStackExchangeRedis`) nên hub messages và circuit negotiation fan across replicas và một reconnect landing trên pod khác vẫn sống. Không có `signalr` connection string = single-replica in-memory (không thay đổi). Kết hợp với session affinity tại ingress để Blazor Server circuits suôn sẻ nhất.

## Copy-agent autoscaling & resilience

Copy-agent lưu trữ long-lived trading sockets, nên scales trên **work, không phải CPU**. Với `copyAgent.keda.enabled=true` chart cài đặt KEDA `ScaledObject` truy vấn Postgres cho running copy-profile count và scales replicas nên mỗi pod lưu trữ khoảng `copyAgent.keda.profilesPerPod` (default 25), giữa `minReplicas`/`maxReplicas`. KEDA đọc DB qua `TriggerAuthentication` bound tới `copyAgent.keda.connectionSecretKey` secret key. Khi `copyAgent.replicas > 1` (hoặc KEDA scales quá 1) chart cũng thêm `topologySpreadConstraints` (spread across nodes) và `PodDisruptionBudget` (`minAvailable: 1`); trên scale-in / rolling update mỗi pod release leases trên `SIGTERM` (`terminationGracePeriodSeconds`, default 30) nên survivor reclaims ngay lập tức — xem [scaling.md](scaling.md).

## Giá trị chính

| Value | Mục đích |
|-------|---------|
| `image.registry` / `.repository` / `.tag` / `.pullPolicy` | Image coordinates (`local` + `Never` cho kind). |
| `secrets.existingSecret` | Sử dụng external/sealed Secret thay vì chart-managed values. |
| `postgres.enabled` | `true` = in-cluster Postgres (dev). `false` + `externalDatabase.connectionString` cho managed DB (prod). |
| `web.ingress.*` / `web.autoscaling` / `mcp.autoscaling` | Ingress + TLS, HPA trên CPU. |
| `nodeAgent.replicas` / `.privileged` / `.mode` / `.maxInstances` | Agent count, DinD privilege, mode, capacity. |
| `web.dockerSocket.enabled` | hostPath `/var/run/docker.sock` cho Web builder/LocalNode (Docker-runtime nodes chỉ). |
| `observability.otlpEndpoint` | Ship logs+traces+metrics tới OTLP collector. |

## Probes

liveness `/alive`, readiness `/health` (Web) · `/version` (MCP) · `/health` (agent) — mapped trong tất cả environments.

## In-cluster test suite

Chạy copy-trading suite như Kubernetes `Job` chống deployed app, nên regression được bắt in-cluster giống như locally. Copy tests chỉ cần Web + Postgres + token cache — **không** privileged node agents.

One-shot, reproducible (kind up → build+load images → deploy → run Job → assert exit 0 → tear down):

```bash
scripts/k8s-e2e.sh                                   # deterministic copy suite (không secrets)
USE_EXISTING_CLUSTER=1 scripts/k8s-e2e.sh            # reuse current kube context
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # live
```

Manual / CI wiring — **deterministic (default, không secrets):**

```bash
docker build -f Dockerfile.tests -t cmind-tests:e2e .          # runner image (SDK + built test projects)
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true
kubectl -n cmind wait --for=condition=complete --timeout=15m job/cmind-cmind-tests
kubectl -n cmind logs job/cmind-cmind-tests
```

**Live suite** thêm cần token cache. cTrader **refresh tokens single-use**, nên cache phải **writable**: Job copies Secret vào emptyDir tại `/app/secrets` qua init-container.

```bash
kubectl -n cmind create secret generic cmind-copy-secrets --from-file=secrets/   # never baked into the image
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true \
  --set tests.project='tests/IntegrationTests/IntegrationTests.csproj' \
  --set tests.filter='FullyQualifiedName~CopyTradingLiveTests' \
  --set tests.copySecret=cmind-copy-secrets
```

| Value | Mục đích |
|-------|---------|
| `tests.enabled` | Render test `Job` (default `false`). |
| `tests.project` / `tests.filter` | Project nào + `dotnet test --filter` để chạy (default: deterministic). |
| `tests.copySecret` | Optional Secret với gitignored `openapi-*.local.json`; copied vào **writable** emptyDir tại `/app/secrets` cho live suite. Empty ⇒ không secret mount. |
| `tests.backoffLimit` | Job retry count (default `0`). |

`LiveCopySecrets` walks up từ `/app` để tìm `secrets/`; live tests skip cleanly khi cache absent. `Dockerfile.tests` SDK-based nên chạy same assertions như local `dotnet test` — cả deterministic (`101 passed`) và full live (`8 passed`) suites được xác minh chạy bên trong image này locally chống Docker trước khi shipping.

## Teardown

```bash
helm -n cmind uninstall cmind        # hoặc: kubectl delete -f <rendered>.yaml
kind delete cluster --name cmind     # local chỉ
```

## Chạy in-cluster suite cross-platform (Linux / macOS / Windows / WSL)

`scripts/k8s-e2e.sh` OS-independent. Converts repo path tới native form (`cygpath -m`) nên Docker, helm và kubectl resolve nó trên **Windows/git-bash** cũng như Linux/macOS — được xác minh end to end trên Windows (kind cluster up → images built+loaded → chart deployed → in-cluster test Job green → teardown).

| Environment | Command |
|-------------|---------|
| Linux / macOS | `scripts/k8s-e2e.sh` |
| Windows (git-bash) | `bash scripts/k8s-e2e.sh` **hoặc** `pwsh scripts/k8s-e2e.ps1` |
| Windows → **WSL (preferred)** | `pwsh scripts/k8s-e2e.ps1 -Wsl` |

**Ưu tiên WSL trên Windows.** Chạy bên trong WSL sử dụng native Linux paths và Docker Desktop's WSL integration, tránh tất cả path-translation edge cases — tùy chọn mạnh mẽ nhất. Cần `docker`, `kind`, `helm`, `kubectl` và .NET SDK trên WSL PATH (Docker Desktop cung cấp `docker`; cài đặt phần còn lại trong distro, ví dụ `go install sigs.k8s.io/kind@latest`, helm/kubectl release binaries). `scripts/k8s-e2e.ps1` wrapper chọn WSL với `-Wsl`, fallback tới git-bash nếu không.

`kind` + `helm` self-installable nếu absent (release binaries hoặc `choco install kind kubernetes-helm`); không coi là unavailable. Xem thêm [../testing/live-copy-trading.md](../testing/live-copy-trading.md).
