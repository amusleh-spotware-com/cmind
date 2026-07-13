---
description: "ストレススイート。お金がかかるアプリ部分を攻撃します — 主としてコピートレーディング — 敵対的、ランダム化、fault-injectedワークロードで。正しい状態を主張。"
---

# ストレステスト

ストレススイート。お金にかかるアプリ部分を攻撃します — 主として**コピートレーディング** — 敵対的、ランダム化、fault-injectedワークロードで。システムが正しい状態を主張。『tests/StressTests'에 있으며、 нормальном `dotnet test` green gateで実行されます。

## アプローチ — 决定的シミュレーション Testing（DST）

分散金融システムをストレステストする最良の方法= **决定的シミュレーション Testing**、TigerBeetle、FoundationDB、Antithesisに準拠：*シミュレートされた*世界で реаль論理を実行、**シード付き**ランダムワークロード + 注入されたfaultで駆動、静止状態で不変量を主張。すべてシード+决定的 → 失敗はseedから正確に再現。組み合わせ：

- **Chaos-engineering fault injection**（Netflix Chaos Monkeyスタイル） — 接続ドロップ、注文拒否、トークンローテーション、ノード死。
- **Property-based不変量** — 正確な呼び出しシーケンスをアサートしない； プロパティはイベントがどのように介在するかに関係なく保持する必要がある（収束、孤立なし、最大1つのリーースホルダー）。

アプリは既に完全なDST世界モデルを出荷しています：`FakeTradingSession`、cTrader-faithfulメモリ内Open APIセッション。ストレススイートはモック而非それ再利用します（リンク済み、単一の情報源） therefore シミュレートされたbrokerは本当の那么好み動作します。

## 対象

### コピートレーディング（主な焦点）

`CopyDstWorld`（`tests/StressTests/CopyTrading/`）経由で駆動、 fake sessionに対してライブ`CopyEngineHost`を実行、一貫性のあるソースワークロードを発信：

| シナリオ | ストレス対象 |
|---|---|
| `Mass_fan_out…` | 1ソース → 80宛先、150 open then close； 完全ファンアウト + ドレイン |
| `High_frequency_open_close…` | 300 rapid interleaved open/close； リークされたポジションなし |
| `Partial_close_and_scale_in_storm…` | 部分的close + scale-inチャーン； label-set安定性 |
| `Connection_flap_storm…` | 反復socket切断/再接続 + 中間フライト非同期； resync収束 |
| `Order_rejection_cascade…` | サブセットがすべての注文を拒否； 健康な宛先は影響を受けません затем 自己修復 через resync |
| `Token_rotation_storm…` | 命令storm中の rapid インプレーストークンスワップ |
| `Randomized_chaos_workload…`（10seeds） | **DSTコア** — すべてのイベントタイプ + すべてのfaultが予測不能に介在 |
| `CopyLeaseReclaimStressTests` | ノード死 + スケールされたクラスター全体のリーース回収（純粋なドメイン、`FakeTimeProvider`） |

**収束不変量。** 静止状態で、すべての健康的な宛先は確かに開いているソースポジションのセットを正確にミラー — 孤立なし、欠落なし。ラベルの*セット*で主張（scale-inは、同じソースラベルの下で2番目の宛先ポジションを開きます therefore 重複ラベルが予想される）。現在注文を拒否している宛先はlag допускаされ、回復すると照合されます。

**リーース不変量。** ノードが死んで+活気づけられるスケジュールで、 最大1つのノードがプロファイルで有効なリーースを保持； デッドノードのリーースは正確に期限切れでを取得し、健康なクラスターはすべてのプロファイルが正確に1つのノードで保持されることで落ち着きます。`CopyEngineSupervisor`'のクレーム述語を`CopyProfile`ドメインリーースメソッドに対してミラーします。

### スイートのスレッド安全性

`FakeTradingSession`は単一スレッド； ストレスワークロードはホストが読み取り/書き込みを行う間テストスレッドから変異させます。`SyncTradingSession`はそれをラップし、すべてのセッション操作を1つのゲートでアトミックにします（再接続コールバック間でゲートを保持するのではなく — `_stateGate`に対して反転しデッドロックします）。シミュレーター自体は不改変のまま。

## 発見されたバグ

- **`CopyEngineHost`の起動時resync競合。** `OnReconnected`が初期参照ロード + 最初のresyncの前に配線され、それが`_stateGate`なし実行されました。起動中のsocket flapが2番目のresync并发で実行され、ホストの非同時状態辞書を破損（`_symbolDetails`、`_sourceVolumes`）。修正：ゲートの下で起動ロード + 最初のresyncを実行。 produção race而非テストアーティファクト — DST chaosワークロードがそれをsurface化しました。

## 実行

```bash
dotnet test tests/StressTests/StressTests.csproj
```

スイートは**シリアル化されます**（`[assembly: CollectionBehavior(DisableTestParallelization = true)]`）：各テストはライブホストバックグラウンドループをスピン、wall clockで静止状態まで駆動、 поэтому 並列実行はホストタスクを飢餓させ、収束タイムアウトをflakyにします。ワークロードはスイートがデフォルトのgreen gateに留まるように秒で終了するようにサイズされます。失敗はそのseedを印刷します； そのseedを再実行して正確に介在を再現。

## 拡張

- 新しいコピー動作 → `CopyDstWorld`にソースopを追加（イベントストリームとのソースブックメンバーシップの一貫性を維持）+ 加重ケース`CopyChaosDstTests`に追加。それが宛先ポジションを作成またはretireできる場合、収束不変量がまだ保持されていることを確認してください。
- 新しいfault → `CopyDstWorld`にインジェクタを追加（`SyncTradingSession`経由で`FakeTradingSession`'のコントロール表面に委任） + 名前付きシナリオplus混沌ミックスで運動。
- シミュレーターをcTrader-faithfulに保ちます（root `CLAUDE.md` mandate参照）； ストレステストをパスさせるために弱めないでください。
