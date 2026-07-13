---
description: "tests/UnitTests/CopyTrading/FakeTradingSession.cs = インメモリ IOpenApiTradingSession すべてのコピートレード単位テストが実行されます。職業: 実際の cTrader Open API サーバーを 近く模倣..."
---

# FakeTradingSession — cTrader Open API 忠実度契約

`tests/UnitTests/CopyTrading/FakeTradingSession.cs` = インメモリ `IOpenApiTradingSession` すべてのコピートレード単位テストが実行されます。職業: **実際の cTrader Open API サーバー**を十分に近く模倣すること。単位テストは動作を凝縮します。ライブティアのみが使用するのを検出します。このドキュメント = 忠実度契約: どのような偽モデル、どの程度忠実に、そしてそれを正直に保つルール。

> **バインディングルール（CLAUDE.md）:** 偽は cTrader 忠実です。**テストが通っても弱めずに拡張してください**。あなたが依存する新しい実際の動作は、ここに忠実度テストでピンで留められてモデル化されます。

## 忠実度マトリックス（F1–F13）

計画 `plans/copy-trading-overhaul.md` §7.6 を追跡。凡例: ✅ モデル化 · ◑ 部分（オプト-イン / 拡張） · ⬜ まだモデル化されていません。

| # | 実際の Open API 動作 | 偽のステータス | モデル化方法 |
|---|------------------------|-------------|-------------------|
| F1 | 市場注文は**部分的フィル**できます | ◑ | `PartialFillFractionForCtid[ctid] = f` は `f×volume` のみを埋める; 統調して Phase‑1 true‑up（G5）が閉じるギャップを示します。Accept→fill イベントペア依然来ます。 |
| F2 | ボリューム**ステップ**に正規化、**最小**以下/ **最大**以上拒否 | ✅ | `VolumeBoundsForCtid[ctid] = (Step, Min, Max)` はステップに丸めていく、`CtraderRejectException(VolumeTooLow/High)` をスロー。 |
| F3 | **無効な SL/TP** 拒否（サイド + 数字） | ⬜ | Phase 0a/1 計画（M6 SL/TP 精度正規化でペア）。 |
| F4 | 価格**数字で整数スケール**; `pipPosition` | ◑ | `SymbolDetails` は現在 `Digits`（および `MaxVolume`）を実行し、実数記号から生成します。`PipPosition` はマーケット範囲許容を駆動し、`Digits` は SL/TP 精度正規化を駆動します（M6）。完全整数価格スケーリングは依然保留中。 |
| F5 | **マーケット範囲**は スポット が `base ± slippage` 内の場合のみ埋める、そうでなければ拒否 | ✅ | `IsMarketRangeRejected` は ライブスポット（`SetSpot`）を `baseSlippagePrice ± slippageInPoints` に比較。レガシー `RejectMarketRangeForCtid` フラグは依然拒否を強制。 |
| F6 | **ペンディングトリガー→フィル**デュアルイベント（オーダーは `positionId` + OPEN ポジション を実行） | ◑ | `PushOpen(..., orderId:)` 満たされたペンディングイベントを再作成; FX‑Blue/cMAM ダブルコピー重複排除が `CopyEngineHostTests.Filled_pending_does_not_double_open` でカバーされている。 |
| F7 | **サーバー駆動クローズ**（SL/TP ヒット、ストップアウト） | ⬜ | 今日は test-pushed（`PushClose`）をクローズ; 価格駆動 SL/TP-hit + ストップアウトクローズは計画。 |
| F8 | **アカウント毎の**シンボルテーブル / 詳細 | ◑ | シンボル名/id 毎偽; アカウント毎の異なるテーブル（クロスブローカー）保留中。 |
| F9 | 完全**アカウント状態**（残高、エクイティ、マージン、フリーマージン） | ◑ | `Balance` + `LoadPositionValuationsAsync`（`SetPositionValuation` 経由のエントリ/スワップ/コミッション）+ `SetSpot` フィードリアルエクイティを比例エクイティサイジング（G2、`CopyEquitySizingTests` でユニット テスト）に。使用済みマージンは統調 API で公開されないため、フリーマージンはエクイティと報告。 |
| F10 | イベントは**サーバータイムスタンプ**を実行 | ✅ | `ExecutionEvent.ServerTimestamp`（unix ms）— 実セッションはディール `ExecutionTimestamp` から読み取る; `PushOpen`/`PushPending` は `serverTimestamp:` を受け入れるため `FakeTimeProvider`-driven テストが実コピーレイテンシ を駆動（G1）。 |
| F11 | **トレーディングモード / スケジュール**（無効 / クローズオンリー / クローズ） | ⬜ | Phase 2b 計画。 |
| F12 | **型付けエラー分類法**（`ProtoOAErrorRes` コード） | ✅ | `RejectReasonForCtid[ctid] = CtraderRejectReason.X` ワンショット `CtraderRejectException(reason)` をスロー（NotEnoughMoney、MarketClosed、PositionNotFound、...）。 |
| F13 | **トークン無効化** — 古いトークン → 認証エラー | ✅ | `InvalidateToken(ctid)` は添付トークンをマーク古い。トレーディング呼び出しスロー**実**`OpenApiException` `OpenApiErrorKind.TokenInvalid`（コード `CH_ACCESS_TOKEN_INVALID`）、`SwapAccessTokenAsync` が新しいトークンをインストールするまで、正確にライブサーバーと同じ。M1 トークン-ロバストネステストを供給。 |

忠実度テストは `tests/UnitTests/CopyTrading/FakeTradingSessionFidelityTests.cs` に生存します。

## オプト-イン、デフォルトはレガシー動作を保有

すべての忠実度ノブはデフォルトで**オフ**なので偽はテスト が気にしないときのシンプルな常時フィル動作を保ちます。テストはアカウント毎にオプト-イン:

```csharp
session.VolumeBoundsForCtid[slave]        = (Step: 10, Min: 10, Max: 1000); // F2
session.PartialFillFractionForCtid[slave] = 0.6;                            // F1 / G5
session.RejectReasonForCtid[slave]        = CtraderRejectReason.NotEnoughMoney; // F12 (one-shot)
session.InvalidateToken(slave);                                             // F13
```

## 特性化 + 適合性（計画、偽 ≡ リアルを保つ）

2 つのメカニズムが移動実サーバーに対して偽を正直に保つ（追跡、Phase 0a 全体でランディング）:

1. **ライブ特性化**（`LiveApiCharacterization`、デモアカウント、シークレット-ゲート、`Inconclusive` クローズマーケット）: 実 Open API を駆動、正確なワイヤ真実（イベントシーケンス、スケーリング、拒否コード）をゴールデンフィクスチャに記録、テストプロジェクトにチェック-イン。フィクスチャにシークレットなし — 観測された形のみ。
2. **適合性ハーネス**: 同じシナリオスイートを 2 回実行 — `FakeTradingSession` に対して 1 回、ライブセッション に対して 1 回（シークレット存在する場合）— 同一の観測可能な結果をアサート。実サーバー変更 → ライブレッグ失敗 → 偽を更新。これは「ユニットテストすべてをカバー」を信頼できるものにします。

ライブ認証情報: `secrets/dev-credentials.local.json`（またはレガシー分割ファイル）— [docs/testing/dev-credentials.md](../testing/dev-credentials.md)を参照してください。
