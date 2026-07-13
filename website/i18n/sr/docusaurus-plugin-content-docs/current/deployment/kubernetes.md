---
description: "Helm графикон: deploy/helm/cmind. Развоја Web, MCP, само-регистрирање чворова агената, опционално у-кластер Postgres."
---

# Kubernetes развој — корак по корак

Helm графикон: `deploy/helm/cmind`. Развоја Web, MCP, само-регистрирање чворова агената, опционално
у-кластер Postgres.

> **Валидирано** крај-у-крај на локалном `kind` кластеру: сви подови достигну `Ready`, чвор агент
> само-регистрира са по-под бездомен DNS име, `/health` + `/version` враћа 200, скалиран-доле
> агент аутоматски означен недостижан. Ток доле = шта тестирано.

## 0. Предуслови

- Kubernetes кластер (управљан EKS/AKS/GKE, или локалан `kind`/`k3d`/`minikube`).
- `kubectl` (упутило се на циљни контекст) и `helm` 3.
- Контејнер регистру кластер може вући од (пропусти за локалан `kind` — учитај слике уместо).

## 1. Граде три слике

```bash
docker build -f Dockerfile.web        -t <registry>/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t <registry>/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t <registry>/cmind-node-agent:1.0.0 .
```

Избаци (`docker push <registry>/cmind-web:1.0.0`, итд.), **или** за локалан `kind` кластер учитај
директно:

```bash
kind create cluster --name cmind
for s in web mcp node-agent; do
  docker tag <registry>/cmind-$s:1.0.0 local/cmind-$s:test
  kind load docker-image local/cmind-$s:test --name cmind
done
```

## 2. Одаберите тајни

```bash
PG_PASSWORD=$(openssl rand -hex 16)
JOIN_TOKEN=$(openssl rand -hex 24)   # >= 32 знакови; дељена кластер тајна за чвор аутоматско откривање
```

## 3. Инсталирајте графикон

Регистру базирана (управљан кластер):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --namespace cmind --create-namespace \
  --set image.registry=<registry-host> --set image.repository=<org>/cmind --set image.tag=1.0.0 \
  --set secrets.pgPassword="$PG_PASSWORD" \
  --set secrets.ownerEmail=you@example.com \
  --set secrets.ownerPassword='Change-Me-Str0ng!' \
  --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

Локалан `kind` (учитане слике, нема externos Postgres, не-привилегирани агенти):

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --set image.registry=local --set image.repository=cmind --set image.tag=test --set image.pullPolicy=Never \
  --set web.dockerSocket.enabled=false --set nodeAgent.privileged=false \
  --set secrets.pgPassword="$PG_PASSWORD" --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

> На `kind`/containerd нема домаћина Docker утичнице, тако `web.dockerSocket.enabled=false`
> (у-апликација градилац/LocalNode недоступна) и `nodeAgent.privileged=false` (агент дакле
> **само-регистрира**; управо не може покренути cTrader контејнере без DinD). За прави радни ток
> извршавања, покрените агенте на чвор базену где `nodeAgent.privileged=true` дозвољено.

Не `helm` бинарна? Рендер и примени:

```bash
helm template cmind deploy/helm/cmind -f my-values.yaml | kubectl apply -f -
```

## 4. Чекајте покретање

```bash
kubectl -n cmind get pods -w
kubectl -n cmind rollout status deploy/cmind-web
```

Очекивање: `cmind-web`, `cmind-mcp`, `cmind-postgres` (Deployments) и `cmind-node-agent-0`
(StatefulSet) сви `Ready`. Web полетност (`/health`) прилози само једном DB миграран (миграције
трчи при покретању).

## 5. Проверите аутоматско откривање

```bash
# Чвор агент би требало да се појави у DB са по-под бездомен DNS BaseUrl и IsReachable=true
PG=$(kubectl -n cmind get pod -l app.kubernetes.io/component=postgres -o jsonpath='{.items[0].metadata.name}')
kubectl -n cmind exec "$PG" -- psql -U postgres -d appdb -c \
  'SELECT "Name","Kind","IsReachable","BaseUrl" FROM "Nodes";'
```

Пример (верификовано):

```
          Name           |    Kind     | IsReachable |                     BaseUrl
-------------------------+-------------+-------------+-------------------------------------------------
 cmind-node-agent-0      | ActiveMixed | t           | http://cmind-node-agent-0.cmind-node-agent...:8080
```

Скала капацитета додавањем репа — свака нова под само-регистрира унутар једног интервала пулса:

```bash
kubectl -n cmind scale statefulset/cmind-node-agent --replicas=3
```

Старост сагласност (верификовано): скала агент доле, прелази на `IsReachable=f` после
`discovery.heartbeatTtl`; скала назад горе, враћа се на мрежи.

## 6. Достигните UI

```bash
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# http://localhost:8080  — пријавите се са сејаним власником
```

Externos приступа: поставити `web.ingress.enabled=true`, `web.ingress.host`, и TLS.

## Зашто су чворови агентима StatefulSet

Главни чвор распоручи радних на **специфична** агент од URL, тако да сваки агент требава стабилна,
индивидуално-адресибилна DNS име. Графикон користи StatefulSet + бездомен Service; сваки под
оглашава `http://<pod>.<svc>.<ns>.svc.cluster.local:8080` и само-регистрира под име.
Исти откривање механизам голо cTrader CLI чворови користе —
видети [../operations/node-discovery.md](../operations/node-discovery.md).

## Web скала-изван (SignalR позадина, S6)

Web апп = Blazor Server + SignalR (живи панел, дневници хаб). Да трчи **више од једног Web репе**,
поставити `signalr` везни низ за Redis крајњу тачку — апп тада регистрира **SignalR Redis
позадина** (`AddStackExchangeRedis`) тако хаб поруке и кругови преговори вентилатор преко репа и
препознај слетање на различитом поду остају живо. Не `signalr` везни низ = једна репа
у-меморијски (без промена). Парови са сеансе афинитета при улазу за гладан Blazor Server кругови.

## Копирање-агент аутоматског скалирања & отпорност

Копирање-агент домаћин дугоживи трговачке утичнице, тако скале на **радни, не CPU**. Са
`copyAgent.keda.enabled=true` графикон инсталира KEDA `ScaledObject` који анкета Postgres за
покренута копирање-профил број и скале репе тако свака под домаћин око `copyAgent.keda.profilesPerPod`
(подразумевано 25), између `minReplicas`/`maxReplicas`. KEDA чита DB преко `TriggerAuthentication` везана на
`copyAgent.keda.connectionSecretKey` кључна тајна. Када `copyAgent.replicas > 1` (или KEDA скале преко 1)
графикон такође додаје `topologySpreadConstraints` (раширена преко чворова) и `PodDisruptionBudget`
(`minAvailable: 1`); на скала-у / увођене освежавање свака под отпусти лизе на `SIGTERM`
(`terminationGracePeriodSeconds`, подразумевано 30) тако преживети рекрајма одмах — видети
[scaling.md](scaling.md).

## Кључне вредности

| Вредност | Намена |
|-------|---------|
| `image.registry` / `.repository` / `.tag` / `.pullPolicy` | Координате слике (`local` + `Never` за kind). |
| `secrets.existingSecret` | Користи externos/печатена Secret уместо графикон-управљане вредности. |
| `postgres.enabled` | `true` = у-кластер Postgres (разв). `false` + `externalDatabase.connectionString` за управљан DB (произв). |
| `web.ingress.*` / `web.autoscaling` / `mcp.autoscaling` | Улаз + TLS, HPA на CPU. |
| `nodeAgent.replicas` / `.privileged` / `.mode` / `.maxInstances` | Агент број, DinD привилегија, режим, капацитета. |
| `web.dockerSocket.enabled` | hostPath `/var/run/docker.sock` за Web градилац/LocalNode (Docker-рантајм чворови само). |
| `observability.otlpEndpoint` | Отправи дневници+трагови+метрике за OTLP колектор. |

## Пробе

liveness `/alive`, полетност `/health` (Web) · `/version` (MCP) · `/health` (агент) — мапирано у све
окружења.

## У-кластер тест свита

Трчи копирање-трговањеу свиту као Kubernetes `Job` против развојене апп, тако регресија хватена
у-кластер исто као локално. Копирање тестови требају само Web + Postgres + токен кеш — **не**
привилегирани чвор агенти.

Једно-тес, поновљива (kind горе → граде+учитај слике → развоја → трчи Job → твр изход 0 → слови):

```bash
scripts/k8s-e2e.sh                                   # детерминистичка копирање свита (нема тајни)
USE_EXISTING_CLUSTER=1 scripts/k8s-e2e.sh            # поново користи текући kube контекст
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # живо
```

Ручно / CI жица — **детерминистичка (подразумевано, нема тајни):**

```bash
docker build -f Dockerfile.tests -t cmind-tests:e2e .          # трчи слику (SDK + грађена тест пројекти)
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true
kubectl -n cmind wait --for=condition=complete --timeout=15m job/cmind-cmind-tests
kubectl -n cmind logs job/cmind-cmind-tests
```

**Живо свита** додатно требај токен кеш. cTrader **освежи токени једно-користи**, тако кеш
мора да буде **писна**: Job копира Secret у emptyDir на `/app/secrets` преко иниц-контејнер.

```bash
kubectl -n cmind create secret generic cmind-copy-secrets --from-file=secrets/   # никад печена у слику
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true \
  --set tests.project='tests/IntegrationTests/IntegrationTests.csproj' \
  --set tests.filter='FullyQualifiedName~CopyTradingLiveTests' \
  --set tests.copySecret=cmind-copy-secrets
```

| Вредност | Намена |
|-------|---------|
| `tests.enabled` | Рендер тест `Job` (подразумевано `false`). |
| `tests.project` / `tests.filter` | Који пројекат + `dotnet test --filter` да трчи (подразумевано: детерминистичка). |
| `tests.copySecret` | Опционално Secret са gitignored `openapi-*.local.json`; копирано у **писна** emptyDir на `/app/secrets` за живо свита. Празно ⇒ не тајна гора. |
| `tests.backoffLimit` | Job повтор број (подразумевано `0`). |

`LiveCopySecrets` шета горе од `/app` да пронађе `secrets/`; живо тестови пропусти чисто када кеш
одсутна. `Dockerfile.tests` SDK базирана тако трчи исти твр као локалан `dotnet test` — обе
детерминистичка (`101 passed`) и пуна живо (`8 passed`) свита верификована покреће унутар ово
слику локално против Docker пре пошиљања.

## Разборитост

```bash
helm -n cmind uninstall cmind        # или: kubectl delete -f <rendered>.yaml
kind delete cluster --name cmind     # локалан само
```

## Покреће у-кластер свита кросплатформа (Linux / macOS / Windows / WSL)

`scripts/k8s-e2e.sh` OS-независна. Претвара репо пут у домаћин облик (`cygpath -m`) тако Docker,
helm и kubectl реши то на **Windows/git-bash** као и Linux/macOS — верификовано крај у крај на Windows
(kind кластер горе → слике грађене+учитане → графикон развојена → у-кластер тест Job зелен → разборитост).

| Окружење | Команда |
|-------------|---------|
| Linux / macOS | `scripts/k8s-e2e.sh` |
| Windows (git-bash) | `bash scripts/k8s-e2e.sh` **или** `pwsh scripts/k8s-e2e.ps1` |
| Windows → **WSL (препоручено)** | `pwsh scripts/k8s-e2e.ps1 -Wsl` |

**Преферирам WSL на Windows.** Покреће унутар WSL користи домаће Linux путеве и Docker Desktop WSL интеграције,
избегавање свих путе-превод граничних случајева — најробуснија опција. Требаю `docker`, `kind`, `helm`,
`kubectl` и .NET SDK на WSL PATH (Docker Desktop обезбеђује `docker`; инсталирај остатак у дистро,
нпр. `go install sigs.k8s.io/kind@latest`, хелм/kubectl развој бинарне). `scripts/k8s-e2e.ps1`
омотач бира WSL са `-Wsl`, пада назад на git-bash инач.

`kind` + `helm` само-инсталирив ако одсутна (развој бинарне или `choco install kind kubernetes-helm`);
не прати као недоступна. Видети такође [../testing/live-copy-trading.md](../testing/live-copy-trading.md).
