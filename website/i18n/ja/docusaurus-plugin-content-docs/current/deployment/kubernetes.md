---
description: "Helm チャート: deploy/helm/cmind。Web、MCP、自己登録ノードエージェント、オプションのインクラスタ Postgres をデプロイします。"
---

# Kubernetes デプロイメント — ステップバイステップ

Helm チャート: `deploy/helm/cmind`。Web、MCP、自己登録ノードエージェント、オプションのインクラスタ Postgres をデプロイします。

> **検証済み**ローカル `kind` クラスタ上でエンドツーエンド: すべてのポッドが `Ready` に到達、ノードエージェントはポッドごとのヘッドレス DNS 名で自己登録、`/health` + `/version` が 200 を返す、スケールダウンエージェントが自動的に到達不可としてマークされます。下のフロー = テストされたもの。

## 0. 前提条件

- Kubernetes クラスタ（管理 EKS/AKS/GKE、またはローカル `kind`/`k3d`/`minikube`）。
- `kubectl`（ターゲットコンテキストを指す）および `helm` 3。
- コンテナレジストリクラスタがプルできます（ローカル `kind` をスキップ — イメージを代わりにロードします）。

## 1. 3 つのイメージをビルド

```bash
docker build -f Dockerfile.web        -t <registry>/cmind-web:1.0.0 .
docker build -f Dockerfile.mcp        -t <registry>/cmind-mcp:1.0.0 .
docker build -f Dockerfile.node-agent -t <registry>/cmind-node-agent:1.0.0 .
```

プッシュ（`docker push <registry>/cmind-web:1.0.0`など）、**または**ローカル `kind` クラスタの場合は直接ロード:

```bash
kind create cluster --name cmind
for s in web mcp node-agent; do
  docker tag <registry>/cmind-$s:1.0.0 local/cmind-$s:test
  kind load docker-image local/cmind-$s:test --name cmind
done
```

## 2. シークレットを選択

```bash
PG_PASSWORD=$(openssl rand -hex 16)
JOIN_TOKEN=$(openssl rand -hex 24)   # >= 32 chars; shared cluster secret for node auto-discovery
```

## 3. チャートをインストール

レジストリベース（管理クラスタ）:

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --namespace cmind --create-namespace \
  --set image.registry=<registry-host> --set image.repository=<org>/cmind --set image.tag=1.0.0 \
  --set secrets.pgPassword="$PG_PASSWORD" \
  --set secrets.ownerEmail=you@example.com \
  --set secrets.ownerPassword='Change-Me-Str0ng!' \
  --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

ローカル `kind`（ロードされたイメージ、外部 Postgres なし、非特権エージェント）:

```bash
helm upgrade --install cmind deploy/helm/cmind \
  --set image.registry=local --set image.repository=cmind --set image.tag=test --set image.pullPolicy=Never \
  --set web.dockerSocket.enabled=false --set nodeAgent.privileged=false \
  --set secrets.pgPassword="$PG_PASSWORD" --set secrets.discoveryJoinToken="$JOIN_TOKEN"
```

> `kind`/containerd ではホスト Docker ソケットがないため、`web.dockerSocket.enabled=false`
> （インアプリビルダー/LocalNode 利用不可）および `nodeAgent.privileged=false`（エージェントは依然として**自己登録**; DinD なしで cTrader コンテナを実行できません）。実際のワークロード実行のため、`nodeAgent.privileged=true` が許可されるノードプールでエージェントを実行してください。

`helm` バイナリなし？テンプレート化して適用:

```bash
helm template cmind deploy/helm/cmind -f my-values.yaml | kubectl apply -f -
```

## 4. ロールアウトを待機

```bash
kubectl -n cmind get pods -w
kubectl -n cmind rollout status deploy/cmind-web
```

予想: `cmind-web`、`cmind-mcp`、`cmind-postgres`（デプロイメント）および `cmind-node-agent-0`
（StatefulSet）すべて `Ready`。Web readiness（`/health`）は DB が移行されるまで（起動時に移行が実行される）のみ渡ります。

## 5. 自動検出を検証

```bash
# ノードエージェントは、ポッドごとのヘッドレス DNS BaseUrl と IsReachable=true を含む DB に表示される必要があります
PG=$(kubectl -n cmind get pod -l app.kubernetes.io/component=postgres -o jsonpath='{.items[0].metadata.name}')
kubectl -n cmind exec "$PG" -- psql -U postgres -d appdb -c \
  'SELECT "Name","Kind","IsReachable","BaseUrl" FROM "Nodes";'
```

例（検証済み）:

```
          Name           |    Kind     | IsReachable |                     BaseUrl
-------------------------+-------------+-------------+-------------------------------------------------
 cmind-node-agent-0      | ActiveMixed | t           | http://cmind-node-agent-0.cmind-node-agent...:8080
```

レプリカを追加して容量をスケール — 各新しいポッドは 1 つのハートビート間隔内で自己登録します:

```bash
kubectl -n cmind scale statefulset/cmind-node-agent --replicas=3
```

鮮度の統調（検証済み）: エージェントをスケールダウン、`discovery.heartbeatTtl` 後に `IsReachable=f` にフリップ; スケールバックアップ、オンラインに戻る。

## 6. UI に到達

```bash
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# http://localhost:8080  — seeded owner でサインイン
```

外部アクセス: `web.ingress.enabled=true`、`web.ingress.host`、および TLS を設定します。

## ノードエージェントが StatefulSet である理由

メインノードは**特定の**エージェントに URL を指定して作業をディスパッチするため、各エージェントは安定した個別にアドレス可能な DNS 名が必要です。チャートは StatefulSet + ヘッドレスサービスを使用します。各ポッドは `http://<pod>.<svc>.<ns>.svc.cluster.local:8080` を広告し、ポッド名で自己登録します。
同じ検出メカニズム bare cTrader CLI ノードを使用 —
[../operations/node-discovery.md](../operations/node-discovery.md)を参照してください。

## Web スケールアウト（SignalR バックプレーン、S6）

Web アプリ = Blazor Server + SignalR（ライブダッシュボード、ログハブ）。**複数の Web レプリカ**を実行するには、`signalr` 接続文字列を Redis エンドポイントに設定します — アプリはそれから **SignalR Redis バックプレーン**（`AddStackExchangeRedis`）を登録するため、ハブメッセージと回路ネゴシエーション ファンがレプリカを横切り、異なるポッドに着地する再接続ライブのままです。`signalr` 接続文字列なし = シングルレプリカメモリ内（変更なし）。Ingress でセッション親和性を組み合わせて、最もスムーズな Blazor Server 回路。

## Copy-agent オートスケーリング & 復元力

Copy-agent ホストは長寿命トレーディングソケット、そのため **CPU ではなく作業にスケール**します。`copyAgent.keda.enabled=true` チャートは KEDA `ScaledObject` をインストールして、Postgres に実行中のコピープロファイルカウントをクエリし、各ポッドが約 `copyAgent.keda.profilesPerPod`（デフォルト 25）をホストするように、`minReplicas`/`maxReplicas` の間でレプリカをスケールします。KEDA は `TriggerAuthentication` にバインドされた `copyAgent.keda.connectionSecretKey` シークレットキーを介して DB を読みます。`copyAgent.replicas > 1`（または KEDA が 1 を超えてスケール）のとき、チャートは `topologySpreadConstraints`（ノード間に分散）と `PodDisruptionBudget`（`minAvailable: 1`）も追加します。スケールイン / ローリングアップデートで各ポッドは `SIGTERM`（`terminationGracePeriodSeconds`、デフォルト 30）でリースを解放し、生存者が直ちに再クレームします —
[scaling.md](scaling.md)を参照してください。

## キー値

| 値 | 目的 |
|-------|---------|
| `image.registry` / `.repository` / `.tag` / `.pullPolicy` | イメージ座標（`local` + kind 用の `Never`）。 |
| `secrets.existingSecret` | チャート管理値の代わりに、外部/シール済みシークレットを使用します。 |
| `postgres.enabled` | `true` = インクラスタ Postgres（開発）。`false` + `externalDatabase.connectionString` 管理 DB（本番）。 |
| `web.ingress.*` / `web.autoscaling` / `mcp.autoscaling` | Ingress + TLS、CPU 上の HPA。 |
| `nodeAgent.replicas` / `.privileged` / `.mode` / `.maxInstances` | エージェントカウント、DinD 特権、モード、容量。 |
| `web.dockerSocket.enabled` | hostPath `/var/run/docker.sock` Web ビルダー/LocalNode（Docker ランタイムノードのみ）。 |
| `observability.otlpEndpoint` | ログ+トレース+メトリクスを OTLP コレクタに出荷します。 |

## プローブ

liveness `/alive`、readiness `/health`（Web）· `/version`（MCP）· `/health`（エージェント）— すべての環境にマップされます。

## インクラスタテストスイート

デプロイされたアプリに対して Kubernetes `Job` としてコピートレーディングスイートを実行します。回帰はインクラスタで同じようにローカルで検出されます。コピーテストは Web + Postgres + トークンキャッシュのみが必要です — **いいえ** 特権ノードエージェント。

1 回限り、再現可能（kind up → ビルド+ロードイメージ → デプロイ → Job 実行 → アサート終了 0 → クリア）:

```bash
scripts/k8s-e2e.sh                                   # deterministic copy suite (no secrets)
USE_EXISTING_CLUSTER=1 scripts/k8s-e2e.sh            # reuse current kube context
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # live
```

マニュアル / CI 配線 — **決定論的（デフォルト、シークレットなし）:**

```bash
docker build -f Dockerfile.tests -t cmind-tests:e2e .          # runner image (SDK + built test projects)
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true
kubectl -n cmind wait --for=condition=complete --timeout=15m job/cmind-cmind-tests
kubectl -n cmind logs job/cmind-cmind-tests
```

**ライブスイート**は追加でトークンキャッシュが必要です。cTrader **リフレッシュトークンシングルユース**、そのためキャッシュは**書き込み可能**である必要があります: Job は初期化コンテナを介して `/app/secrets` のシークレットを emptyDir にコピーします。

```bash
kubectl -n cmind create secret generic cmind-copy-secrets --from-file=secrets/   # never baked into the image
helm upgrade cmind deploy/helm/cmind -n cmind --reuse-values --set tests.enabled=true \
  --set tests.project='tests/IntegrationTests/IntegrationTests.csproj' \
  --set tests.filter='FullyQualifiedName~CopyTradingLiveTests' \
  --set tests.copySecret=cmind-copy-secrets
```

| 値 | 目的 |
|-------|---------|
| `tests.enabled` | テスト `Job` をレンダリング（デフォルト `false`）。 |
| `tests.project` / `tests.filter` | どのプロジェクト + `dotnet test --filter` を実行するか（デフォルト: 決定論的）。 |
| `tests.copySecret` | gitignored `openapi-*.local.json` を含むオプションシークレット; ライブスイート用に **書き込み可能** emptyDir の `/app/secrets` にコピーされます。空 ⇒ シークレットマウントなし。 |
| `tests.backoffLimit` | Job 再試行カウント（デフォルト `0`）。 |

`LiveCopySecrets` は `/app` から上向きに歩いて `secrets/` を見つけます。ライブテストはキャッシュが不在のときにクリーンにスキップします。`Dockerfile.tests` SDK ベースなので、ローカル `dotnet test` と同じアサーションを実行します — 決定論的（`101 passed`）と完全ライブ（`8 passed`）の両方のスイートが、この イメージ内でローカルで Docker に対して出荷前に検証されました。

## クリーンアップ

```bash
helm -n cmind uninstall cmind        # or: kubectl delete -f <rendered>.yaml
kind delete cluster --name cmind     # local only
```

## クロスプラットフォーム（Linux / macOS / Windows / WSL）でインクラスタスイートを実行

`scripts/k8s-e2e.sh` OS 独立。リポパスをネイティブ形式に変換（`cygpath -m`）して Docker、
helm と kubectl は **Windows/git-bash** および Linux/macOS で解決します — Windows でエンドツーエンドで検証済み
（kind クラスタアップ → イメージビルト+ロード → チャートデプロイ → インクラスタテストジョブ緑 → クリーンアップ）。

| 環境 | コマンド |
|-------------|---------|
| Linux / macOS | `scripts/k8s-e2e.sh` |
| Windows（git-bash） | `bash scripts/k8s-e2e.sh` **または** `pwsh scripts/k8s-e2e.ps1` |
| Windows → **WSL（推奨）** | `pwsh scripts/k8s-e2e.ps1 -Wsl` |

**Windows では WSL を優先。** WSL 内で実行するとネイティブ Linux パスと Docker Desktop の WSL 統合を使用し、すべてのパス変換エッジケースを回避します — 最も堅牢なオプション。WSL PATH 上の `docker`、`kind`、`helm`、`kubectl` および .NET SDK が必要です（Docker Desktop は `docker` を提供します。distro でその他をインストール、例：`go install sigs.k8s.io/kind@latest`、helm/kubectl リリースバイナリ）。`scripts/k8s-e2e.ps1` ラッパーは `-Wsl` で WSL を選択、そうでなければ git-bash にフォールバック。

`kind` + `helm` 自己インストール可能（リリースバイナリまたは `choco install kind kubernetes-helm`）; 利用不可として扱わない。[../testing/live-copy-trading.md](../testing/live-copy-trading.md)も参照してください。
