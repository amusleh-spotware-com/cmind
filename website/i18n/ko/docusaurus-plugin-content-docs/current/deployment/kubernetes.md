---
description: "Helm 차트: deploy/helm/cmind. Web, MCP, 자체 등록 노드 에이전트, 선택적 클러스터 내 Postgres를 배포합니다."
---

# Kubernetes 배포 — 단계별

Helm 차트: `deploy/helm/cmind`. Web, MCP, 자체 등록 노드 에이전트, 선택적 클러스터 내 Postgres를 배포합니다.

> **검증됨** 로컬 `kind` 클러스터에서 종단간: 모든 Pod이 `Ready`에 도달하고, 노드 에이전트는 Pod별 헤드리스 DNS 이름으로 자체 등록되며, `/health` + `/version`은 200을 반환하고, 스케일 다운된 에이전트는 자동으로 `IsReachable=f`로 표시됩니다. 아래 흐름 = 테스트된 것.

## 0. 필수 조건

- Kubernetes 클러스터 (관리형 EKS/AKS/GKE 또는 로컬 `kind`/`k3d`/`minikube`).
- `kubectl` (대상 컨텍스트를 지정) 및 `helm` 3.
- 클러스터가 가져올 수 있는 컨테이너 레지스트리 (로컬 `kind`는 건너뛰고 대신 이미지를 로드).

## 1. 세 개의 이미지 빌드

```bash
docker build -f Dockerfile.web        -t <registry>/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t <registry>/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t <registry>/cmind-node-agent:1.0.0 .
```

푸시 (`docker push <registry>/cmind-web:1.0.0` 등) **또는** 로컬 `kind` 클러스터의 경우 직접 로드:

```bash
kind create cluster --name cmind
for s in web mcp node-agent; do
  docker tag <registry>/cmind-$s:1.0.0 local/cmind-$s:test
  kind load docker-image local/cmind-$s:test --name cmind
done
```

## 2. 비밀 선택

```bash
PG_PASSWORD=$(openssl rand -hex 16)
JOIN_TOKEN=$(openssl rand -hex 24)   # >= 32자; 노드 자동 디스커버리를 위한 공유 클러스터 비밀
```

## 3. 차트 설치

레지스트리 기반 (관리형 클러스터):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --namespace cmind --create-namespace \
  --set image.registry=<registry-host> --set image.repository=<org>/cmind --set image.tag=1.0.0 \
  --set secrets.pgPassword="$PG_PASSWORD" \
  --set secrets.ownerEmail=you@example.com \
  --set secrets.ownerPassword='Change-Me-Str0ng!' \
  --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

로컬 `kind` (로드된 이미지, 외부 Postgres 없음, 권한 없는 에이전트):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --set image.registry=local --set image.repository=cmind --set image.tag=test --set image.pullPolicy=Never \
  --set web.dockerSocket.enabled=false --set nodeAgent.privileged=false \
  --set secrets.pgPassword="$PG_PASSWORD" --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

> `kind`/containerd에 호스트 Docker 소켓이 없으므로 `web.dockerSocket.enabled=false` (앱 내 빌더/LocalNode 사용 불가)이고 `nodeAgent.privileged=false` (에이전트는 여전히 **자체 등록**; DinD 없이는 cTrader 컨테이너를 실행할 수 없음). 실제 워크로드 실행의 경우 `nodeAgent.privileged=true`가 허용되는 노드 풀에서 에이전트를 실행합니다.

`helm` 바이너리가 없으신가요? 렌더링 및 적용:

```bash
helm template cmind deploy/helm/cmind -f my-values.yaml | kubectl apply -f -
```

## 4. 롤아웃 대기

```bash
kubectl -n cmind get pods -w
kubectl -n cmind rollout status deploy/cmind-web
```

예상: `cmind-web`, `cmind-mcp`, `cmind-postgres` (Deployment) 및 `cmind-node-agent-0` (StatefulSet) 모두 `Ready`. Web 준비 상태 (`/health`)는 DB 마이그레이션 후에만 통과합니다 (마이그레이션은 시작 시 실행됨).

## 5. 자동 디스커버리 확인

```bash
# 노드 에이전트는 Pod별 헤드리스 DNS BaseUrl 및 IsReachable=true로 DB에 나타나야 합니다
PG=$(kubectl -n cmind get pod -l app.kubernetes.io/component=postgres -o jsonpath='{.items[0].metadata.name}')
kubectl -n cmind exec "$PG" -- psql -U postgres -d appdb -c \
  'SELECT "Name","Kind","IsReachable","BaseUrl" FROM "Nodes";'
```

예시 (검증됨):

```
          Name           |    Kind     | IsReachable |                     BaseUrl
-------------------------+-------------+-------------+-------------------------------------------------
 cmind-node-agent-0      | ActiveMixed | t           | http://cmind-node-agent-0.cmind-node-agent...:8080
```

복제본을 추가하여 용량을 확장합니다 — 각 새 Pod은 하나의 하트비트 간격 내에 자체 등록됩니다:

```bash
kubectl -n cmind scale statefulset/cmind-node-agent --replicas=3
```

부실 조정 (검증됨): 에이전트 스케일 다운, `discovery.heartbeatTtl` 후 `IsReachable=f`로 전환; 다시 스케일 업하면 온라인으로 돌아옵니다.

## 6. UI 도달

```bash
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# http://localhost:8080  — 시드된 소유자로 로그인
```

외부 액세스: `web.ingress.enabled=true`, `web.ingress.host` 및 TLS 설정.

## 노드 에이전트가 StatefulSet인 이유

메인 노드는 **특정** 에이전트를 URL로 디스패치하므로 각 에이전트는 안정적인 개별 주소 지정 가능한 DNS 이름이 필요합니다. 차트는 StatefulSet + 헤드리스 서비스를 사용합니다. 각 Pod은 `http://<pod>.<svc>.<ns>.svc.cluster.local:8080`을 광고하고 Pod 이름으로 자체 등록합니다. 같은 디스커버리 메커니즘 베어 cTrader CLI 노드가 사용합니다 — [../operations/node-discovery.md](../operations/node-discovery.md)를 참조하세요.

## Web 스케일아웃 (SignalR 백플레인, S6)

Web 앱 = Blazor Server + SignalR (라이브 대시보드, 로그 허브). **하나 이상의 Web 복제본**을 실행하려면 `signalr` 연결 문자열을 Redis 엔드포인트로 설정합니다 — 앱은 **SignalR Redis 백플레인** (`AddStackExchangeRedis`)을 등록하여 허브 메시지 및 회로 협상이 복제본 전체에 걸쳐 팬아웃됩니다. 다른 Pod에 착륙하는 재연결은 라이브 상태를 유지합니다. `signalr` 연결 문자열 없음 = 단일 복제본 인메모리 (변경 없음). 인그레스에서 세션 선호도와 쌍을 이루면 가장 부드러운 Blazor Server 회로가 됩니다.

## 복사 에이전트 자동 스케일링 및 복원력

복사 에이전트는 오래 지속된 거래 소켓을 호스팅하므로 **작업, CPU가 아닌**에서 확장합니다. `copyAgent.keda.enabled=true`를 사용하면 차트는 Postgres를 쿼리하는 KEDA `ScaledObject`를 설치하여 실행 복사 프로필 수를 세고 복제본을 확장하여 각 Pod이 약 `copyAgent.keda.profilesPerPod` (기본값 25)를 호스팅합니다. `minReplicas`/`maxReplicas` 사이. KEDA는 `TriggerAuthentication`에 바인딩된 `copyAgent.keda.connectionSecretKey` 비밀 키를 통해 DB를 읽습니다. `copyAgent.replicas > 1` (또는 KEDA가 1을 넘어 확장)일 때 차트는 `topologySpreadConstraints` (노드 전체에 확산)와 `PodDisruptionBudget` (`minAvailable: 1`)도 추가합니다. 스케일인/롤링 업데이트에서 각 Pod은 `SIGTERM`에서 리스를 해제합니다 (`terminationGracePeriodSeconds`, 기본값 30). 따라서 생존자는 즉시 회수합니다 — [scaling.md](scaling.md)를 참조하세요.

## 핵심 값

| 값 | 목적 |
|-------|---------|
| `image.registry` / `.repository` / `.tag` / `.pullPolicy` | 이미지 좌표 (kind의 경우 `local` + `Never`). |
| `secrets.existingSecret` | 차트 관리 값 대신 외부/봉인된 비밀 사용. |
| `postgres.enabled` | `true` = 클러스터 내 Postgres (개발). `false` + `externalDatabase.connectionString` (프로덕션) 관리 DB용. |
| `web.ingress.*` / `web.autoscaling` / `mcp.autoscaling` | 인그레스 + TLS, CPU 기반 HPA. |
| `nodeAgent.replicas` / `.privileged` / `.mode` / `.maxInstances` | 에이전트 수, DinD 권한, 모드, 용량. |
| `web.dockerSocket.enabled` | Web 빌더/LocalNode용 hostPath `/var/run/docker.sock` (Docker 런타임 노드만 해당). |
| `observability.otlpEndpoint` | 로그+추적+메트릭을 OTLP 수집기로 전송. |

## 프로브

liveness `/alive`, readiness `/health` (Web) · `/version` (MCP) · `/health` (에이전트) — 모든 환경에 매핑됨.

## 클러스터 내 테스트 스위트

배포된 앱에 대해 복사 거래 스위트를 Kubernetes `Job`으로 실행하여 클러스터 내에서 로컬로 회귀가 caught됩니다. 복사 테스트는 Web + Postgres + 토큰 캐시만 필요합니다 — **권한 있는 노드 에이전트는 불필요**.

원샷, 재현 가능 (kind up → 빌드+로드 이미지 → 배포 → Job 실행 → assert exit 0 → 부수):

```bash
scripts/k8s-e2e.sh                                   # 결정론적 복사 스위트 (비밀 없음)
USE_EXISTING_CLUSTER=1 scripts/k8s-e2e.sh            # 현재 kube 컨텍스트 재사용
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # 라이브
```

수동 / CI 와이어링 — **결정론적 (기본값, 비밀 없음):**

```bash
docker build -f Dockerfile.tests -t cmind-tests:e2e .          # 러너 이미지 (SDK + 빌드 테스트 프로젝트)
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true
kubectl -n cmind wait --for=condition=complete --timeout=15m job/cmind-cmind-tests
kubectl -n cmind logs job/cmind-cmind-tests
```

**라이브 스위트**는 추가로 토큰 캐시가 필요합니다. cTrader **새로고침 토큰 단일 사용**, 따라서 캐시는 **쓰기 가능**해야 합니다: Job은 초기 컨테이너를 통해 비밀을 `/app/secrets`의 emptyDir로 복사합니다.

```bash
kubectl -n cmind create secret generic cmind-copy-secrets --from-file=secrets/   # 이미지에 베이크되지 않음
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true \
  --set tests.project='tests/IntegrationTests/IntegrationTests.csproj' \
  --set tests.filter='FullyQualifiedName~CopyTradingLiveTests' \
  --set tests.copySecret=cmind-copy-secrets
```

| 값 | 목적 |
|-------|---------|
| `tests.enabled` | 테스트 `Job` 렌더링 (기본값 `false`). |
| `tests.project` / `tests.filter` | 어떤 프로젝트 + `dotnet test --filter`를 실행할지 (기본값: 결정론적). |
| `tests.copySecret` | gitignored `openapi-*.local.json`을 포함하는 선택적 비밀; 라이브 스위트를 위해 `/app/secrets`의 **쓰기 가능** emptyDir로 복사됨. 빈 = 비밀 마운트 없음. |
| `tests.backoffLimit` | Job 재시도 수 (기본값 `0`). |

`LiveCopySecrets`는 `/app`에서 위로 걸어가서 `secrets/`를 찾습니다. 라이브 테스트는 캐시가 없을 때 깨끗하게 건너뜁니다. `Dockerfile.tests`는 SDK 기반이므로 로컬 `dotnet test`와 동일한 어설션을 실행합니다 — 결정론적 (`101 passed`) 및 전체 라이브 (`8 passed`) 스위트 모두 배포 전에 Docker에 대해 로컬로 이 이미지 내부에서 실행 검증됨.

## 부수

```bash
helm -n cmind uninstall cmind        # 또는: kubectl delete -f <rendered>.yaml
kind delete cluster --name cmind     # 로컬만 해당
```

## 크로스 플랫폼에서 클러스터 내 스위트 실행 (Linux / macOS / Windows / WSL)

`scripts/k8s-e2e.sh`는 OS 독립적입니다. 저장소 경로를 네이티브 형식으로 변환 (`cygpath -m`)하여 Docker, helm 및 kubectl이 **Windows/git-bash**뿐만 아니라 Linux/macOS에서도 해결하도록 합니다 — Windows에서 종단간 검증됨 (kind 클러스터 업 → 이미지 빌드+로드 → 차트 배포 → 클러스터 내 테스트 Job green → 부수).

| 환경 | 명령 |
|-------------|---------|
| Linux / macOS | `scripts/k8s-e2e.sh` |
| Windows (git-bash) | `bash scripts/k8s-e2e.sh` **또는** `pwsh scripts/k8s-e2e.ps1` |
| Windows → **WSL (권장)** | `pwsh scripts/k8s-e2e.ps1 -Wsl` |

**Windows에서 WSL을 선호합니다.** WSL 내에서 실행하면 기본 Linux 경로 및 Docker Desktop의 WSL 통합을 사용하여 모든 경로 변환 엣지 케이스를 피합니다 — 가장 견고한 옵션. WSL PATH에 `docker`, `kind`, `helm`, `kubectl` 및 .NET SDK 필요 (Docker Desktop은 `docker` 제공; 배포판에 나머지 설치, 예: `go install sigs.k8s.io/kind@latest`, helm/kubectl 릴리스 바이너리). `scripts/k8s-e2e.ps1` 래퍼는 `-Wsl`로 WSL을 선택하고, 그렇지 않으면 git-bash로 폴백합니다.

`kind` + `helm`은 없을 경우 자체 설치 가능 (릴리스 바이너리 또는 `choco install kind kubernetes-helm`); 사용할 수 없는 것으로 취급하지 마세요. [../testing/live-copy-trading.md](../testing/live-copy-trading.md)도 참조하세요.
