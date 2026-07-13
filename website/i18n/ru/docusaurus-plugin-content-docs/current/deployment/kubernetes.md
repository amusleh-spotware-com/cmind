---
description: "Helm chart: deploy/helm/cmind. Развертывает Web, MCP, self-registering узел агентов, опциональный in-cluster Postgres."
---

# Kubernetes развертывание — шаг за шагом

Helm chart: `deploy/helm/cmind`. Развертывает Web, MCP, self-registering узел агентов, опциональный in-cluster Postgres.

> **Валидировано** end-to-end на локальном `kind` кластере: все pods достигают `Ready`, узел agent self-registers с per-pod headless DNS именем, `/health` + `/version` возвращают 200, scaled-down agent auto-marked unreachable. Flow ниже = что протестировано.

## 0. Prerequisites

- Kubernetes кластер (управляемый EKS/AKS/GKE, или локальный `kind`/`k3d`/`minikube`).
- `kubectl` (указал на целевой контекст) и `helm` 3.
- Container registry кластер может pull из (пропустить для локального `kind` — load образы вместо).

## 1. Построить три образа

```bash
docker build -f Dockerfile.web        -t <registry>/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t <registry>/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t <registry>/cmind-node-agent:1.0.0 .
```

Push (`docker push <registry>/cmind-web:1.0.0`, etc.), **или** для локального `kind` кластера load прямо:

```bash
kind create cluster --name cmind
for s in web mcp node-agent; do
  docker tag <registry>/cmind-$s:1.0.0 local/cmind-$s:test
  kind load docker-image local/cmind-$s:test --name cmind
done
```

## 2. Выбрать secrets

```bash
PG_PASSWORD=$(openssl rand -hex 16)
JOIN_TOKEN=$(openssl rand -hex 24)   # >= 32 chars; shared cluster secret для node auto-discovery
```

## 3. Установить chart

Registry-based (управляемый кластер):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --namespace cmind --create-namespace \
  --set image.registry=<registry-host> --set image.repository=<org>/cmind --set image.tag=1.0.0 \
  --set secrets.pgPassword="$PG_PASSWORD" \
  --set secrets.ownerEmail=you@example.com \
  --set secrets.ownerPassword='Change-Me-Str0ng!' \
  --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

Локальный `kind` (loaded образы, нет external Postgres, non-privileged агентов):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --set image.registry=local --set image.repository=cmind --set image.tag=test --set image.pullPolicy=Never \
  --set web.dockerSocket.enabled=false --set nodeAgent.privileged=false \
  --set secrets.pgPassword="$PG_PASSWORD" --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

> На `kind`/containerd нет host Docker socket, поэтому `web.dockerSocket.enabled=false` (in-app builder/LocalNode недоступно) и `nodeAgent.privileged=false` (agent все еще **self-registers**; просто не может запускать cTrader контейнеры без DinD). Для реального выполнения workload, запускайте агентов на node pool где `nodeAgent.privileged=true` разрешено.

Нет `helm` бинарного? Render и apply:

```bash
helm template cmind deploy/helm/cmind -f my-values.yaml | kubectl apply -f -
```

## 4. Ждать rollout

```bash
kubectl -n cmind get pods -w
kubectl -n cmind rollout status deploy/cmind-web
```

Ожидаем: `cmind-web`, `cmind-mcp`, `cmind-postgres` (Deployments) и `cmind-node-agent-0` (StatefulSet) все `Ready`. Web readiness (`/health`) проходит только один раз DB migrated (миграции запускаются при запуске).

## 5. Верифицировать auto-discovery

```bash
# Node agent должно появиться в БД с per-pod headless DNS BaseUrl и IsReachable=true
PG=$(kubectl -n cmind get pod -l app.kubernetes.io/component=postgres -o jsonpath='{.items[0].metadata.name}')
kubectl -n cmind exec "$PG" -- psql -U postgres -d appdb -c \
  'SELECT "Name","Kind","IsReachable","BaseUrl" FROM "Nodes";'
```

Пример (верифицировано):

```
          Name           |    Kind     | IsReachable |                     BaseUrl
-------------------------+-------------+-------------+-------------------------------------------------
 cmind-node-agent-0      | ActiveMixed | t           | http://cmind-node-agent-0.cmind-node-agent...:8080
```

Scale вместимость добавлением replicas — каждый новый pod self-registers в один heartbeat интервал:

```bash
kubectl -n cmind scale statefulset/cmind-node-agent --replicas=3
```

Staleness reconciliation (верифицировано): scale agent down, flips в `IsReachable=f` после `discovery.heartbeatTtl`; scale back up, возвращает online.

## 6. Доступ к UI

```bash
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# http://localhost:8080  — войти с seeded owner
```

Внешний доступ: установить `web.ingress.enabled=true`, `web.ingress.host` и TLS.

## Почему узел агентов это StatefulSet

Main узел dispatches работу в **specific** agent по URL, поэтому каждый agent требует stable, individually-addressable DNS имя. Chart использует StatefulSet + headless Service; каждый pod размещает `http://<pod>.<svc>.<ns>.svc.cluster.local:8080` и self-registers под имя pod.
Тот же mechanism обнаружения bare cTrader CLI узлы используют — смотрите [../operations/node-discovery.md](../operations/node-discovery.md).

## Web scale-out (SignalR backplane, S6)

Web app = Blazor Server + SignalR (live dashboard, logs hub). Для запуска **более одного Web replica**, установить `signalr` connection string на Redis endpoint — app затем регистрирует **SignalR Redis backplane** (`AddStackExchangeRedis`) поэтому hub сообщения и circuit переговоры fan через replicas и reconnect приземление на другой pod остается live. Нет `signalr` connection string = single-replica in-memory (без изменений). Pair с session affinity на ingress для smoothest Blazor Server circuits.

## Copy-agent autoscaling & resilience

Copy-agent hosts long-lived trading сокеты, поэтому scales на **work, не CPU**. С `copyAgent.keda.enabled=true` chart устанавливает KEDA `ScaledObject`, который запрашивает Postgres для running copy-profile count и scales replicas поэтому каждый pod hosts около `copyAgent.keda.profilesPerPod` (default 25), между `minReplicas`/`maxReplicas`. KEDA читает БД через `TriggerAuthentication` bound к `copyAgent.keda.connectionSecretKey` secret key. Когда `copyAgent.replicas > 1` (или KEDA scales past 1) chart также добавляет `topologySpreadConstraints` (spread через узлы) и `PodDisruptionBudget` (`minAvailable: 1`); на scale-in / rolling update каждый pod releases leases на `SIGTERM` (`terminationGracePeriodSeconds`, default 30) поэтому survivor reclaims немедленно — смотрите [scaling.md](scaling.md).

## Ключевые значения

| Значение | Цель |
|-------|---------|
| `image.registry` / `.repository` / `.tag` / `.pullPolicy` | Image coordinates (`local` + `Never` для kind). |
| `secrets.existingSecret` | Использовать external/sealed Secret вместо chart-managed значений. |
| `postgres.enabled` | `true` = in-cluster Postgres (dev). `false` + `externalDatabase.connectionString` для managed БД (prod). |
| `web.ingress.*` / `web.autoscaling` / `mcp.autoscaling` | Ingress + TLS, HPA на CPU. |
| `nodeAgent.replicas` / `.privileged` / `.mode` / `.maxInstances` | Agent count, DinD привилегия, режим, вместимость. |
| `web.dockerSocket.enabled` | hostPath `/var/run/docker.sock` для Web builder/LocalNode (Docker-runtime узлы только). |
| `observability.otlpEndpoint` | Ship logs+traces+metrics в OTLP collector. |

## Probes

liveness `/alive`, readiness `/health` (Web) · `/version` (MCP) · `/health` (agent) — mapped во всех окружениях.

## In-cluster test suite

Запускайте copy-trading suite как Kubernetes `Job` против развернутого приложения, поэтому регрессия поймана in-cluster то же как локально. Copy тесты требуют только Web + Postgres + token cache — **нет** привилегированных узел агентов.

One-shot, reproducible (kind up → build+load images → deploy → run Job → assert exit 0 → tear down):

```bash
scripts/k8s-e2e.sh                                   # deterministic copy suite (нет secrets)
USE_EXISTING_CLUSTER=1 scripts/k8s-e2e.sh            # переиспользуйте текущий kube контекст
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # live
```

Ручно / CI wiring — **deterministic (default, нет secrets):**

```bash
docker build -f Dockerfile.tests -t cmind-tests:e2e .          # runner образ (SDK + built test проекты)
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true
kubectl -n cmind wait --for=condition=complete --timeout=15m job/cmind-cmind-tests
kubectl -n cmind logs job/cmind-cmind-tests
```

**Live suite** дополнительно требует token cache. cTrader **refresh tokens single-use**, поэтому cache должен быть **writable**: Job копирует Secret в emptyDir на `/app/secrets` через init-container.

```bash
kubectl -n cmind create secret generic cmind-copy-secrets --from-file=secrets/   # никогда не baked в образ
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true \
  --set tests.project='tests/IntegrationTests/IntegrationTests.csproj' \
  --set tests.filter='FullyQualifiedName~CopyTradingLiveTests' \
  --set tests.copySecret=cmind-copy-secrets
```

| Значение | Цель |
|-------|---------|
| `tests.enabled` | Render test `Job` (default `false`). |
| `tests.project` / `tests.filter` | Какой проект + `dotnet test --filter` запускать (default: deterministic). |
| `tests.copySecret` | Опциональный Secret с gitignored `openapi-*.local.json`; скопирован в **writable** emptyDir на `/app/secrets` для live suite. Empty ⇒ нет secret mount. |
| `tests.backoffLimit` | Job retry count (default `0`). |

`LiveCopySecrets` ходит вверх из `/app` для нахождения `secrets/`; live тесты skip cleanly когда cache отсутствует. `Dockerfile.tests` SDK-based поэтому запускает те же assertions как локальный `dotnet test` — оба deterministic (`101 passed`) и полный live (`8 passed`) suites верифицированы запущением внутри этого образа локально против Docker перед отправкой.

## Teardown

```bash
helm -n cmind uninstall cmind        # или: kubectl delete -f <rendered>.yaml
kind delete cluster --name cmind     # локальный только
```

## Запуск in-cluster suite cross-platform (Linux / macOS / Windows / WSL)

`scripts/k8s-e2e.sh` OS-independent. Конвертирует repo path в native форму (`cygpath -m`) поэтому Docker, helm и kubectl resolve это на **Windows/git-bash** также как Linux/macOS — верифицировано end to end на Windows (kind cluster up → images built+loaded → chart deployed → in-cluster test Job green → teardown).

| Окружение | Команда |
|-------------|---------|
| Linux / macOS | `scripts/k8s-e2e.sh` |
| Windows (git-bash) | `bash scripts/k8s-e2e.sh` **или** `pwsh scripts/k8s-e2e.ps1` |
| Windows → **WSL (preferred)** | `pwsh scripts/k8s-e2e.ps1 -Wsl` |

**Предпочитайте WSL на Windows.** Запуск внутри WSL использует native Linux пути и Docker Desktop's WSL интеграцию, избегая всех path-translation edge cases — most robust опция. Требует `docker`, `kind`, `helm`, `kubectl` и .NET SDK на WSL PATH (Docker Desktop обеспечивает `docker`; установить rest в distro, например `go install sigs.k8s.io/kind@latest`, helm/kubectl release бинарные). `scripts/k8s-e2e.ps1` wrapper подбирает WSL с `-Wsl`, fallback в git-bash другой способ.

`kind` + `helm` self-installable если отсутствуют (release бинарные или `choco install kind kubernetes-helm`); не рассматривайте как недоступные. Смотрите также [../testing/live-copy-trading.md](../testing/live-copy-trading.md).
