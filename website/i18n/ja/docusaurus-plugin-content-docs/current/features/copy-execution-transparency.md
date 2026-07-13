---
description: "per-copy実行事実 — レイテンシ реальный slippage、fill vs failure — すべてのコピー試行でキャプチャされ、per-profile透明性レポートとして表面化。デフォルトオフ; App:Copy:TransparencyEnabled=trueで有効化。"
---

# Copy execution transparency (フェーズ3)

per-copy実行事実 — レイテンシ、現実的スリッページ、fill vs failure — すべてのコピー試行でキャプチャされ、per-profile透明性レポートとして表面化。**デフォルトオフ**; `App:Copy:TransparencyEnabled=true`で有効化。オフのとき、コピーエンジンは字节identical: hostはno-op sinkに放出、変更なし。

## 動作原理

```
CopyEngineHost ──Record(fact)──▶ ICopyEventSink
                                   │
             (transparency off) NullCopyEventSink   → 破棄（デフォルト；ゼロホットパスコスト）
             (transparency on)  ChannelCopyEventSink → バウンドメモリチャネル（DropOldest）
                                   │
                                   ▼
                          CopyExecutionDrainer (BackgroundService)
                                   │  App drain間隔でバッチ処理
                                   ▼
                          CopyExecution append-onlyテーブル  ◀── GET /api/copy/profiles/{id}/transparency
```

- **ホットパスはI/Oから解放。** Hostは`ICopyEventSink.Record(...)`を呼び出し — ノンブロッキング、決してスローしないエンキュー。決してawaitせず、データベースに触れず、注文実行をブロックしない。
- **バックプレッシャーより損失が優先。** チャネルはバウンド（`CopyExecutionChannelCapacity`）で`DropOldest`: DBドレイナーが停止している場合、*最古の*透明性行がドロップされ代わりにコピーが遅延。透明性 = ベストエフォートテレメトリ、トレーディング依存ではなく。
- **アウトオブバンド永続化。** `CopyExecutionDrainer`はチャネルをバッチ（`CopyExecutionDrainBatchSize`）で`CopyExecutionDrainInterval`にドレーし、スコープ`DataContext`経由で`CopyExecution`行を書き込みます。シャットダウン時の最終フラッシュ。
- **事実而非コマンド。** `CopyExecution` = append-onlyログ（`InstanceLog`/`AuditLog`と同様）、而非アグリゲート。読み取りモデルは直接クエリ（CQRS-lite）、メモリ内アグリゲート。

## 何が記録されるか

1つの`CopyExecutionRecord`は1つの宛先での1つのコピー試行ごと：

| Kind | いつ | 運ぶもの |
|------|------|---------|
| `Opened` | コピー注文が配置された | シンボル、サイド、wire volume、マスター価格 реализованный slippage（ポイント）、レイテンシ（ms） |
| `Failed` | コピーオープンがスロー/拒否された | シンボル、サイド、マスターvolume/price、レイテンシ、失敗理由（例外タイプ） |

（`Closed`/`Skipped`/`Reconciled`は将来拡張のためにenumに存在。）

## レポート

`GET /api/copy/profiles/{id}/transparency`（owner-scoped）は最新の500事実 대해以下を返します：

- **概要** — 合計、opened、failed、**fill rate**、**平均レイテンシ（ms）**、**平均スリッページ（ポイント）**。
- **最近** — 生の最近事実（宛先、ソースポジション、シンボル、サイド、volume、マスター価格、スリッページ、レイテンシ、理由、タイムスタンプ）。

## 設定（`App:Copy`）

| 設定 | デフォルト | 効果 |
|---------|---------|--------|
| `TransparencyEnabled` | `false` | このノードのper-copy事実キャプチャ+ドレイナー有效化。 |

チャネル容量、ドレインバッチサイズ、ドレイン間隔 = `CopyDefaults`定数（`CopyExecutionChannelCapacity` / `CopyExecutionDrainBatchSize` / `CopyExecutionDrainInterval`）。

## テスト

- **ユニット**（`CopyTransparencyTests`） — 正常なオープンは正しいシンボル/サイド/ volume/レイテンシで`Opened`事実を放出；拒否されたオープンは理由と共に`Failed`事実を放出。キャプチャリングsink介して駆動。
- **統合**（`CopyExecutionDrainerTests`、real Postgres） — ドレイナーはバッファされた事実を`CopyExecution`ログに永続化；空sinkは書き込みなし。
- **DST** — hostはno-opデフォルトsinkでfire-and-forgetを発するため、決定論的コピーストレススイートは緑のままであり（23/23）。
