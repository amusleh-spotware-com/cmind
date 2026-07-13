---
description: "cTrader CLI ノードは自己登録 + ハートビート経由でクラスタに参加 — 手動エントリなし。Consul/Nomad/kubeadm エージェントと同じパターン: エージェントはメインノード位置と共有クラスタシークレットを知っています..."
---

# ノード自動検出

cTrader CLI ノードは**自己登録 + ハートビート**経由でクラスタに参加します — 手動エントリなし。Consul/Nomad/kubeadm エージェントと同じパターン: エージェントはメインノード位置と共有クラスタシークレットを知ったうえでブートし、その後継続的に自身を通知します。

> Docker Compose および `kind` Kubernetes クラスタ上でエンドツーエンド検証済み: エージェント自己登録、DB で到達可能と表示、ハートビート TTL 後停止時に自動的に到達不可とマーク、再開時にオンラインに戻る。

## どのように機能するか

```
CtraderCliNode agent                         Main（Web）
------------------                         ----------
POST /api/nodes/register  ── join token ──▶ トークンを検証（定時）
  { name, baseUrl, mode,                    プロトコルバージョンの検証
    maxInstances, dataDir,                   名前で CtraderCliNode をアップサート
    protocolVersion }                        LastHeartbeatAt をスタンプ、IsReachable=true
        ▲                                     └─ CtraderCliNode.SelfRegister / RecordHeartbeat
        │  every HeartbeatInterval            NodeHeartbeatMonitor（バックグラウンド）:
        └──────────────────────────────────── if now - LastHeartbeatAt > HeartbeatTtl
                                                 → CtraderCliNode.MarkUnreachable()（NodeWentOffline）
```

- **登録 == ハートビート。** エージェントは `HeartbeatIntervalSeconds` で再 POST します。最初の呼び出しはノードを作成（`NodeRegistered` イベント）; 後の呼び出しはライブネス更新。停止後の再開ハートビートはノードをアップサート可能に反転（`NodeCameOnline`）。
- **ライブネス統調。** `NodeHeartbeatMonitor` は最後のハートビート超過ノードを `HeartbeatTtl` 到達不可とマークします。スケジューラー（`IsActive`/`AcceptsRun`/`AcceptsBacktest` ゲート到達可能性）は再度報告するまで作業を配置停止します。
- **孤立インスタンス再クレーム。** `NodeInstanceReclaimer`（バックグラウンド）は到達不可なノードで迷ったすべての非ターミナルインスタンスを**Failed**（`FailureReason = "Node unreachable - instance reclaimed"`、`InstanceFailed` ドメインイベント → ユーザー通知）に遷移させ、クラッシュ/パーティション化ノードがインスタンス「Running」を永遠に停止させることはできません。再クレームは、ノードの最後のハートビートが `HeartbeatTtl + InstanceReclaimGrace` を超えて古くなった後にのみ発火し、短時間のブリップが最初に回復する機会を与えます。再クレーム**実行は自動再スケジュールされません**: パーティション化されたが生きているノードは依然としてコンテナを実行している可能性があり、コンテナレベルのフェンシングがないため、再起動は二重実行のリスクがあります — ユーザーは意図的に再クレームされた実行を再開します。バックテストは自己終了するため、再クレームされたバックテストは単に再実行されます。
- **ID はノード名。** メインは `NodeName` でアップサート、再起動時に IP/URL が変更されたポッドはアイデンティティを保持し、新しい `AdvertiseUrl` を再登録します。
- **モードは最初の登録で固定。** ノードモード（`Run`/`Backtest`/`Mixed`）は永続化タイプ、ハートビートで変更できない。別のモードでの再登録はライブネスに尊重されますがモード変更は無視されます（警告としてログ）。モード変更: ノード削除、再登録させます。

## 設定

Main（Web）— `App:Discovery`:

| キー | デフォルト | 意味 |
|-----|---------|---------|
| `Enabled` | `false` | 登録エンドポイント + モニター のマスタースイッチ。 |
| `JoinToken` | — | エージェントが提示する必要のある共有クラスタシークレット（≥ 32 文字）。 |
| `HeartbeatTtl` | `00:01:30` | サイレントノードをマーク到達不可の前の猶予。 |
| `InstanceReclaimGrace` | `00:01:00` | `HeartbeatTtl` を超えて到達不可なノード上の迷ったインスタンスが再クレーム（失敗）される前の追加マージン。 |
| `MonitorInterval` | `00:00:30` | モニター と インスタンス再クレーマー スイープの実行頻度。 |
| `HeartbeatInterval` | `00:00:30` | エージェントに推奨の合図として返される値。 |

エージェント（CtraderCliNode）— `NodeAgent`:

| キー | 意味 |
|-----|---------|
| `MainUrl` | メインノードのベース URL。空 = 手動登録モード（ループ no-op）。 |
| `AdvertiseUrl` | メイン がこのエージェントに到達するために使用する URL。 |
| `NodeName` | 一意の名前。空白の場合はマシン名にデフォルト。 |
| `Mode` | `Run` / `Backtest` / `Mixed`。 |
| `MaxInstances` | スケジューラー で尊重される容量ヒント。 |
| `HeartbeatIntervalSeconds` | 再登録合図。 |
| `JwtSecret` | メイン の `JoinToken` と等しい必要があります — 登録キャリア と ディスパッチ JWT 署名キーの両方。 |

## セキュリティモデル（v1）

自動登録ノードは**1 つのクラスタシークレット**を共有（`JoinToken` == 各エージェントの `JwtSecret`）。メインは各ディスパッチリクエストに 5 分 HS256 JWT をそのシークレットで署名します。エージェント は検証します。要件:

- `JoinToken` ≥ 32 文字を保持し、それをローテーション（メイン の `App:Discovery:JoinToken` とすべてのエージェント の `NodeAgent:JwtSecret` を一緒に更新）。
- 本番環境でメイン およびエージェント の前で TLS を終了（リバースプロキシ / ingress）。
- エージェントは引き続き `AllowedImagePrefix` にマッチするイメージのみを実行します。

**強化フォローアップ（v1 ではない）:** 登録時に固有のノードごとのシークレットを発行（kubeadm スタイルブートストラップ → ノードごとの認証情報）。単一の侵害されたエージェントはピアのディスパッチトークンを偽造できません。登録フロー は既にレスポンスボディを返します — ミント済みノードごとのシークレットを返すのに自然な場所。

## 手動ノードは依然機能

`POST /api/nodes`（管理 UI）は引き続きノードごとの独自のシークレットでピンノードを登録します。検出は加算的です。

ホワイトラベルデプロイメントは**手動コントロールを非表示**（またはノード全体のサーフェスを非表示）にして純粋に自動検出に依存できます: `App:Branding:NodesUi=Monitor` は手動追加/削除をドロップし、`Hidden` はナビ、ページ、手動 API を削除し、`App:Branding:RestrictNodesToOwner` はサーフェスを所有者のみに制限します。ここの自己登録 + ハートビートエンドポイントはすべてのモードで影響を受けません。
[ホワイトラベル → ノード UI 可視性](../features/white-label.md#nodes-ui-visibility)を参照してください。
