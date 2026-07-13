---
description: "完全な再現可能なコピートレーディングテストスイート。2つのレイヤー："
---

# コピートレーディングテストスイート（決定的 + ライブ）

完全な再現可能なコピートレーディングテストスイート。2つのレイヤー：

1. **決定論的テスト**（xUnit、ネットワークなし） — コピー数学 + エンジンスピードロジック。高速、CI、シークレットなし。各money-managementモード、すべてのフィルター/オプション、エンジンレジリエンスをカバー。
2. **ライブE2Eテスト**（本当のcTraderデモアカウント） — 本当のcTraderデモアカウント間で実際の注文を配置+コピーする production `CopyEngineHost`。完全に自動化され、ユニットテストのように再実行可能：ローカルgitignoreファイルからキャッシュされた認証情報を読み取り、アクセス 토큰を自己リフレッシュ、スキップ clean when secrets absent（CI stays green）。

決して本番fundedアカウントでは実行しません — 各アカウント**デモ**、各ライブテストは開いたポジションを Closeします。

## レイアウト

```
tests/UnitTests/CopyTrading/
  CopySizingCalculatorTests.cs   — 各サイズモード + rounding + min/max lot
  CopyDecisionEngineTests.cs     — direction/reverse/slippage/delay/symbol filter/size-zero
  CopyEngineHostTests.cs         — メモリ内fake sessionに対してhostコピー論理をテスト
  FakeTradingSession.cs          — 决定的 IOpenApiTradingSession（注文/close/amendを記録）
  OpenApiConnectionTests.cs      — connect / reconnect / backoff / fatal fault（レジリエンス）

tests/IntegrationTests/CopyLive/
  LiveCopySecrets.cs             — gitignoreされたsecretを読み込み、リフレッシュされたトークンを保存
  LiveTokenBootstrapTests.cs     — アプリDBからアプリケショントークンをトークンキャッシュに複号
  LiveCopyFixture.cs             — アクセス 토큰をローテート демоアカウントリストを露出
  LiveCopyScenario.cs            — 1つの実際のコピーシナリオをエンドツーエンドで実行（open → copy → verify → clean up）
  CopyTradingLiveTests.cs        — ライブシナリオ（1:1、1:many、reverse、…）
```

## シークレット（ローカル、gitignored — 決してコミットされない）

すべての認証情報`<repo>/secrets/`（すでに`.gitignore`にある）。開発者は**最初の2ファイルのみ**を書く； 3番目（トークン）は自動生成。

`secrets/openapi-test-app.local.json` — Open API app：

```json
{ "ClientId": "2175_…", "ClientSecret": "…" }
```

`secrets/openapi-cids.local.json` — 承認するcIDログイン認証情報（1つまたは複数）：

```json
{ "Cids": [
  { "Cid": "amusleh",  "Username": "amusleh",  "Password": "…" },
  { "Cid": "afhacker", "Username": "afhacker", "Password": "…" }
] }
```

`secrets/openapi-tokens.local.json` — **オンボーディングによって written**、マルチcID、毎ランリフレッシュ：

```json
{ "Cids": [
  { "Cid": "amusleh", "RefreshToken": "…", "AccessToken": "…", "IsLive": false,
    "Accounts": [ { "CtidTraderAccountId": 25172589, "TraderLogin": 3635817, "IsLive": false }, … ] }
] }
```

リフレッシュトークン**決して期限切れしません**、therefore 1回限りのオンボーディング後、ライブテストは無期限に機能：各ランは各cIDのリフレッシュトークンを新鮮なアクセス 토큰と交換します（ローテーション） — ブラウザなし、プロンプトなし。

## 1回限りのオンボーディング（完全に自動化 — シークレット節約以上の開発者操作なし）

オンボーディングは保存されたcID認証情報からヘッドレスブラウザで本当のcTrader IDログ인을驱动し、ローカルHTTPSリスナー（アプリの登録されたredirectで`https://localhost:7080/openapi/callback`）でOAuthコールバックをキャプチャ、コードを失効トークンと交換し、アカウントリストをロードし、マルチcIDトークンキャッシュを書きます。マシンごとに1回実行（またはcIDの追加時）：

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

`openapi-cids.local.json`のすべてのcIDを承認し、`openapi-tokens.local.json`を書きます。その後、ライブコピーテストは他には何も必要としません。（cIDのcTrader IDアカウントは自動化が完了するためにログインに2FA/captchaが必要です。）

**代替ブートストラップ**（アカウントが実行中のアプリで既に承認されている場合）：アプリのPostgresボリュームから保存されたトークンを復号化する代わりに再認証：

```bash
docker run -d --name cmind-pg-extract -e POSTGRES_PASSWORD=appdev \
  -v app-pg-data:/var/lib/postgresql/data -p 5544:5432 postgres:17-alpine
CMIND_VOLUME_CONN="Host=127.0.0.1;Port=5544;Database=appdb;Username=postgres;Password=appdev" \
  dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveTokenBootstrapTests
docker rm -f cmind-pg-extract
```

## 安全 — デモのみ

ライブテストは**デモアカウントのみで取引**： fixtureはトークンキャッシュを`IsLive == false`のアカウントにフィルターし демоゲートウェイに接続因此 命令は絶対に本番fundedアカウントに着地しません即使 liveアカウントが承認されている場合も。各ポジションはテストが開いたものはクリーンアップで Closeされます。

## 実行

```bash
# 決定論的コピーテストのみ（高速、シークレットなし、CIセーフ）
dotnet test tests/UnitTests --filter FullyQualifiedName~CopyTrading

# 本当のデモアカウントでライブコピーテスト（2つのシークレットファイルが必要）
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests

# すべて
dotnet test
```

 シークレットファイルなしでは、ライブテストはスキップ理由 + no-opsとして合格Therefore スイートは任意の場所での実行が安全です。

## Coverage

### Money management / sizing（决定的 — `CopySizingCalculatorTests`）
FixedLot · LotMultiplier · NotionalMultiplier（contract-size / 通貨） · ProportionalBalance ·
ProportionalEquity · ProportionalFreeMargin · AutoProportional · FixedRiskPercent · FixedLeverage ·
balance/leverage/capacity miss-matchの上下にスケール（「golden rule」） ·
lot-step rounding · min-lot skip vs force-to-min · max-lot cap ·
 tighter-of bound-vs-spec min & max · zero master balance skip。

### 決定フィルタ（决定的 — `CopyDecisionEngineTests`）
Symbol white/black list / allow · LongOnly / ShortOnly · reverse flips effective side ·
slippage over limit skip + exactly-at-limit allowed · stale-signal（max delay）skip · size-zero skip ·
reconnect reconciliation（open-missing dedup、close-orphaned）。

### コピーエンジンホスト（决定的 — `CopyEngineHostTests`、メモリ内session）
オープンミラーは market order（side / volume / label） · **reverse** flips side and **swaps SL/TP** ·
**symbol mapping** resolves destination symbol · **order-failure on one slave still copies to the others** ·
source close closes the mirrored copy · reconnect resync closes orphaned copies。

### 接続レジリエンス（决定的 — `OpenApiConnectionTests`）
Connectedに到達 app auth後 · ドロップされた接続が再接続して再認証 · fatal auth error faults ·
exponential backoff。

### ライブ、本当のcTraderデモアカウント（`CopyTradingLiveTests`）
トークンリフレッシュ + アカウント一覧 · **1:1** copy executes · **1:many** copy mirrors to every slave ·
**reverse** turns master buy into slave sell · **cross-cID** copy（master under one cID mirrors to slave under another、each authenticating with own token）。各 реальная最小lotポジションをマスターで開き、エンジンがそれをミラーするのを待ち（source-position-id labelでslaveで一致）、assert、 everythingをclose。クローズド市場は**Inconclusive**を報告而非 failing。

## ロギング＆監査可能性

すべてのコピートレーディング操作はソース生成された構造化イベント経由でログされます（`Core/Logging/LogMessages.cs`、イベントID 1043–1055）、完全なtrailが監査可能：

| イベント | Id | 意味 |
|-------|----|---------|
| CopyHostStarted | 1046 | プロファイルのエンジンが上がりました（ソース + 宛先数） |
| CopySourceOpen | 1047 | マスターがポジションを開きました（シンボル / サイド / lots） |
| CopyOrderPlaced | 1048 | コピーがslaveに送信されました（シンボル / サイド / volume / ソースid） |
| CopySkipped | 1049 | コピーがスキップされました + 理由（slippage / direction / symbol_filter / size_zero / …） |
| CopyProtectionApplied | 1050 | SL/TPがslaveコピーに適用されました |
| CopyOpenFailed | 1051 | slaveコピーオープンが失敗しました（isolated — other slaves continue） |
| CopySourceClose / CopyPositionClosed | 1052 / 1053 | マスターがclose → slaveコピーがclose |
| CopyCloseFailed | 1054 | slaveコピーcloseが失敗しました |
| CopyResync | 1055 | 再接続照合（ソースオープンカウント、孤立close） |
| CopyPartialClose | 1056 | マスター部分的closeがmirror — 比例sliceがslaveでclose |
| CopyScaleIn | 1057 | マスターscale-inがmirror（opt-in） — 追加volumeがslaveにコピー |
| CopyPendingOrderPlaced | 1058 | 保留中limit/stopがslaveにmirrorされました（opt-in） |
| CopyPendingOrderCancelled | 1059 | ソース保留中キャンセル → slave保留中キャンセル |
| CopyTrailingApplied | 1060 | trailing stopがslaveコピーに適用されました（opt-in） |
| CopyStopLossAmended | 1061 | ソースSL移動がslaveコピーを再修正しました |
| CopyHostTokenRotated | 1062 | スーパーバイザーがアクセス 토큰がローテーションした後実行中ホストを再起動しました |

ログはSerilog compact JSONとしてemitted（構造化props：`ProfileId`、`DestinationCtid`、`SourcePositionId`、`Symbol`、`Side`、`Volume`、…）、`OTEL_EXPORTER_OTLP_ENDPOINT`設定時にOTLPにshipped。**完全に構成可能** perカテゴリ — 例：copy-engineの詳細度を引き上げ/下げコードを触らずに：

```jsonc
// appsettings.json — Serilog level overrides
"Serilog": { "MinimumLevel": { "Override": {
  "CopyEngine": "Information",              // the CopyEngineHost audit trail
  "Nodes.CopyTrading": "Information"        // supervisor / token refresh
} } }
```

`Audit_log_records_every_trading_operation` host testはopen、order、protection、closeのtrailが発行されることをassert。

## エッジケース（実際のコピー/MAMプラットフォームが失敗する方法に対して検証済み）

Slippage & latency、symbol suffix/mismatch、再接続時の重複トレード、レバレッジmiss-match & margin-safe sizing、deposit-currency/contract-size差異、min/max lot & rounding、拒否された注文、方向フィルタ、切断後の孤立cleanup — すべて上記でカバー。[leverage miss-match](https://copygram.app/blog/education/the-truth-about-leverage-mismatches-copying-high-leverage-low-leverage-accounts) ·
[cross-broker copying](https://www.mt4copier.com/cross-broker-trade-copying-efficient-forex-replication/) ·
[copier pitfalls](https://www.mt4copier.com/copy-trading-pitfalls-every-account-manager-must-avoid/) ·
[slippage & latency](https://copygram.app/blog/education/understanding-slippage-latency-copy-trading) ·
[why copy trading fails](https://xtsupport.zendesk.com/hc/en-us/articles/51566808595993-Why-Copy-Trading-Fails-Causes-Prevention-Guide) ·
[risk parameters](https://www.mt4copier.com/risk-parameters/).

## 高度なミラーリングcoverage（部分的close · 保留中注文 · SL-trailing）

ホストは市場open/close以上をミラーします。各動作 = 宛先ごとのopt-inフラグ`CopyDestination`で（`MirrorPartialClose`デフォルトオン、`MirrorScaleIn`/`CopyPendingOrders`/`CopyTrailingStop`デフォルトオフ）、意図メソッドでガード、jsonb永続化（マイグレーション `CopyAdvancedMirroringAndNodeAffinity`）。

| 動作 | 決定的テスト（`CopyEngineHostTests`） | ライブテスト |
|-----------|--------------------------------------------|-----------|
| 部分的close → 比例slice | `Partial_close_mirrors_a_proportional_slice_on_the_slave`（1.0→0.4は60%close）+ disabled path | `Partial_close_shrinks_the_slave_copy_proportionally` ✅ |
| Scale-in | `Scale_in_is_ignored_by_default_and_mirrored_when_enabled` | — |
| 保留中limit/stop配置 | `Pending_order_is_placed_on_the_slave_when_enabled`（Theory: Limit+Stop）+ disabled path | `Pending_limit_order_is_mirrored_and_cancel_propagates` ✅ |
| 保留中キャンセル | `Source_pending_cancel_cancels_the_slave_pending` | （同じライブテスト — masterでキャンセル、slaveがキャンセルをassert）✅ |
| Fill保留中重複openなし | `Filled_pending_does_not_double_open`（order-id → position-id dedupe） | — |
| Trailing stop | `Trailing_stop_is_applied_to_the_copy_when_enabled` | `Trailing_stop_is_mirrored_onto_the_slave_copy` ✅ |
| ソースSL移動再修正 | `Source_stop_loss_move_re_amends_the_copy` | — |
| 監査イベントが発生 | `Advanced_mirroring_audit_events_fire`（1056/1058/1059） | — |

上記のすべてのライブテストは本当のcTraderデモアカウントに対して検証済み（1:1、1:many、reverse、cross-cID、部分的close、保留中+cancel、trailing）。

`OpenApiTradingSession`でのWire追加：`SendPendingOrderAsync`、`CancelOrderAsync`、`ReconcilePendingOrdersAsync`、trailing flag on `AmendPositionSltpAsync`、order/pending fields on `ExecutionEvent`、`LoadSpotPriceAsync`（spot subscribe → bid/ask、ライブ保留中/trailingテストで市場から離れた resting ordersを配置するために使用）、`StopLoss`/`TrailingStopLoss` on `OpenPositionSnapshot`（copyのtrailing状態はreconcile経由で観測可能）。宛先コピーは**ソースポジションid**でラベル付けされます（保留中コピーはソース**注文id**で）therefore 再接続照合はidベースで、決してトレードが重複しません。

**cTraderイベントのgotcha（ライブで検証済み）：** resting保留中注文の`ORDER_ACCEPTED`/`ORDER_CANCELLED`実行eventは**非オープン`Position`プレースホルダー** plus `Order`を運びます。ストリームはposition branch**の前**に*order*イベントとして分類する必要があります（positionが`OPEN`でないことをgate）さもなくば保留中配置がposition closeとして誤って読み取られます。`SourceExecutionsAsync`はこれを行います；これを見逃すと保留中ミラーリングがすべてサイレントにドロップされます。

## トークンローテーション + ノード亲和性

- **実行中ホストへのローテーション。** `CopyEngineSupervisor`は各実行中ホストのトークン署名を記録し、すべてのreconcileでDBからプランを再構築します（`OpenApiTokenRefreshService`によって свежиにローテーション）。変更された署名はホストを再起動します（`CopyHostTokenRotated`、1062）；新しいホストの`ResyncAsync`はトレードを重複させることなく状態を再構築。ライブホストがコピーを持続していることを確認するために途中で強制ローテーションを実行 via `IOpenApiTokenClient.RefreshAsync`。
- **ノード亲和性（重複コピーなし）。** Webローカルノードと`CopyAgent`ワーカーの両方がスーパーバイザーを実行。各実行中プロファイルは正確に1つのノードによってクレームされます（`CopyProfile.AssignedNode`、アトミック`ExecuteUpdate`クレーム `CopyOptions.NodeName`がキー、デフォルトマシン名）。スーパーバイザーは自身が所有するプロファイルのみをホスト；stop/pauseはクレームを解放。Coverage：
  - ドメイン（unit）：`AssignToNode_makes_profile_hosted_by_only_that_node`、
    `Stopping_a_profile_releases_its_node_assignment`、`NodeIdentity_rejects_blank`。
  - **統合（real Postgres、Testcontainers）**: `CopyNodeAffinityTests`はスーパーバイザーの実際の`ClaimUnassignedProfilesAsync`を驱动 — 最初のノードが3つの実行中プロファイルをすべてクレーム、2番目が**0**をクレーム（重複ホストなし）、pause→restartが別のノード用にクレームを解放することをassert。
  - **ローテーション検出**（`TokenRotationSignatureTests`）：スーパーバイザーの`TokenSignature`はソースまたは宛先トークンがローテーション的时候会 changes、安定した 其他情况（実行中ホストは実際のローテーションでのみ再起動）。

### 単一使用リフレッシュトークン（重要）

cTrader**リフレッシュトークンは単一使用です** —  각 リフレッシュは*新しい*リフレッシュトークンを返し、古いものを無効化します。ライブfixtureは起動時にリフレッシュし、回転したトークンを`secrets/openapi-tokens.local.json`に永続化します。后果：
- 実行がリフレッシュ하지만**永続化できません**（例：読み取り専用マウント）場合、キャッシュされたトークンがデッド、next実行が`ACCESS_DENIED`で失敗。ヘッドレスオンボーディングで再生成：
  `CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`。
- `LiveCopySecrets.SaveTokens`は書き込み失敗を飲み поэтому 読み取り専用キャッシュは実行をクラッシュさせませんが、**ライブ**クラスター内スイートは依然として**書き込み可能**キャッシュが必要です（K8s JobはSecretをemptyDirにコピー — 必須 — 単一使用リフレッシュトークンを永続化可能）。

## Kubernetesクラスターでスイートを実行

スイート全体をHelmデプロイされたアプリに対してクラスター内で実行因此 回帰はクラスター内でローカルに捕捉されます。[`docs/deployment/kubernetes.md`](../deployment/kubernetes.md#in-cluster-test-suite)を参照。

```bash
scripts/k8s-e2e.sh                                   # kindクラスター、决定的スイート（シークレットなし）
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # ライブ
```

`Dockerfile.tests`がランナーイメージをビルド； Helm `tests-job.yaml`（gated `tests.enabled=false`）がクラスター内Postgres + Webに対して実行します。**デフォルト = 决定的コピースイート**（シークレットなし、トークンローテーションなし）。ライブスイートについては`tests.copySecret`をgitignored `openapi-*.local.json`を保持するSecretに設定します； init-containerはそれを`/app/secrets`の**書き込み可能**emptyDirにコピーします（必須 — 単一使用リフレッシュトークンを永続化可能）。コピーテストはWeb + Postgres + トークンキャッシュのみが必要です — 特権ノードエージェントは不要。スクリプトはJobがexit 0でログに`Passed!`を含めることをassert。

**ここで検証済み（Docker、クラスターなし）：** テスト画像は决定的スイート（`101 passed`）と書き込み可能な`secrets/` mountでフル**ライブ**スイート（`8 passed`）を実行 — イーブン JobパスなしKubernetes。`kind`/`kubectl`/`helm`がオーサリング環境にないため、完全な`k8s-e2e.sh`クラスターランは実行されていない1つのステップです。

## ライブオプションマトリックス + カオス（LiveCopyMatrix / LiveCopyChaos）

2つのデータ驱动ライブスイートが`LiveCopyScenario` / `LiveCopyFixture`、决定的DSTストレススイートのライブ対応物を構築：

- **`LiveCopyMatrix`** — `[Theory]`/`[MemberData]`オプションマトリックス：各行に異なる構成の宛先で実際のマスターopen、ゴールデン結果をassert。行：`one_to_one`、`half_multiplier`、`reverse`（反対側）、`manage_only`（開くなし）、`trading_hours_closed`（ウィンドウがnowを除外 → no copy）、`source_label_block`（ラベルフィルタ → no copy）、`lot_sanity_block`（天井 → no copy）。
- **`LiveCopyChaos`** — 敵対的な開始に対するコピーエンジン：ホスト開始前にマスターが既にポジションを保有Therefore 一致は起動時resyncからのみ可能です（`Sync-Open-on-Start`）、plus toggle offのnegative case。决定的socket-flap / token-rotation / rejection chaosはDSTスイートで網羅的にカバーされています； これはライブresyncパスをassertします。

両方**スキップ clean** credentialsなし、 закрыт市場では**Inconclusive**をレポート、各行は開いたポジションをクリーンアップします（デモアカウントのみ）。

**Fixture堅牢性：** `LiveCopyFixture`はキャッシュされたトークンがリフレッシュできないとき正常にクリーン skipに劣化します（cTraderリフレッシュトークンは単一使用 — staleキャッシュまたはオフラインランはもう全体のライブコレクションをフォールトしません； トークンキャッシュをリフレッシュするにはOAuthオンボーディングを再実行）。

## ライブ + クラスタースイートを自分で実行（言い訳なし — すべてが自己サービス可能）

以下はすべてこの環境で利用可能です。**「認証情報が利用不可」「トークンが古い」「kind/helmがインストールされていない」と主張しないでください** — それらを設定して実行してください。

### 1. 新しいOpen APIトークンを取得（ヘッドレスOAuth、ブラウザ相互作用なし）

cTraderリフレッシュトークンは単一使用，因此 キャッシュされたトークンは古くなります。保存されたcID username/passwordから自分を再作成してください（`secrets/openapi-cids.local.json` + `secrets/openapi-test-app.local.json`、または統一された`secrets/dev-credentials.local.json`）。オンボーディングテストは**ヘッドレスEdge** OAuthをPlaywright経由で驱动し、`secrets/openapi-tokens.local.json`を書きます：

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

~13秒； すべてのcIDを承認、キャッシュ新鮮なトークン。ライブスイートがリフレッシュ失敗によりfixture利用不可をレポートしたときにいつでも再実行。

### 2. ライブコピー Suiteを実行（本当のcTraderデモアカウント）

```bash
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests   # コアミラーリング（8）
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyMatrix          # オプションマトリックス（7）
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyChaos           # resync chaos（2）
```

 реальный DEMO注文を配置 + cleanup（決して本番アカウントではない）、关闭市場で**Inconclusive**をレポート。エンドツーエンドで検証済み。

### 3. 実行中のアプリからトークンをブートストラップ（代替）

アプリが実行中でcIDがアプリ内でリンクされている場合、アプリの最新のリフレッシュトークンを`app-pg-data` Postgresボリュームから直接抽出して再認証する代わりに — `LiveTokenBootstrapTests`、設定`CMIND_VOLUME_CONN`を参照。

### 4. KubernetesクラスターE2E

`kind`、`helm`、Dockerが利用可能（PATH上没有の場合は`go install`/releaseバイナリまたは`choco install kind kubernetes-helm`でインストール）。イメージをビルド+ロード、chartをデプロイ、クラスター内テストJobを実行、exit 0をassertする1回限りのスクリプト：

```bash
scripts/k8s-e2e.sh                                 # 决定的コピースイート（シークレットなし）
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh   # ライブクラスター内
```

[../deployment/kubernetes.md](../deployment/kubernetes.md)を参照。
