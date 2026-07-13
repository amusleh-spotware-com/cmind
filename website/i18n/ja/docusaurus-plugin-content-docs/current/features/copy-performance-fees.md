---
description: "ウォーターマークスタイルのmoney-managerパフォーマンス手数料：プロバイダーがフォロワーのピークエクイティ上の*新規*利益に対してパーセンテージを請求。デフォルトオフ、App:Copy:FeesEnabledでオプトイン。"
---

# Copy performance fees (フェーズ4)

Money-manager**ウォーターマークでのパフォーマンス手数料**、標準コピートレーディングモデル（cTrader Copy、Darwinex、ZuluTrade利益分配）：プロバイダーがフォロワーの*新規*利益に対してパーセンテージを請求 — 開始バランスでは決してなく、まだ回復した地面では二度と請求されません。**オプトイン**、`App:Copy:FeesEnabled`（デフォルトオフ）。

## モデル（ウォーターマーク）

各宛先（フォロワーアカウント）ごと、決済時：

1. **最初の決済**がウォーターマーク（HWM）を現在のエクイティにシード → 請求なし（フォロワーは決して Deposit に請求されない）。
2. **新高**（エクイティ > HWM）：`fee = performanceFeePercent × (equity − HWM)`、次に`HWM ← equity`。
3. **ピーク以下**：請求なし、HWM変更なし — フォロワーはまず古いピークを回復する必要があるため、同じ利益に対して二度請求されることはありません。

手数料計算は`CopyDestination.SettleFee(equity)`上のドメイン不変量 — アグリゲートが所有；決済サービスはpollされたエクイティのみを供給し、返された金額を記録します。`PerformanceFee`は50%でキャップされた値オブジェクトため、設定ミスがあってもフォロワーの全利益を持ち逃げすることはできません。

## 決済方法

```
CopyFeeSettlementService (BackgroundService、FeesEnabledの時のみ)
   │  App:Copy:FeeSettlementInterval마다
   ├─ 運行中のプロファイルとfee設定された宛先をロード
   ├─ ICopyEquityReader.ReadEquityAsync(ctid)   ← OpenApiCopyEquityReaderがセッションを開き、
   │                                               balance + floating P&Lを計算（PropFirmEquityCalculator）
   ├─ destination.SettleFee(equity)             ← HWMロジックをアグリゲートに
   └─ 新しい新高でのみ advanced HWM + append CopyFeeAccrualを永続化
```

- `ICopyEquityReader`はコア抽象化；ライブ実装（`OpenApiCopyEquityReader`）が唯一のインフラ片 — そのため決済 + HWMロジックはフェイクリーダーでテストされ、ライブbroker不要。
- `CopyFeeAccrual`はappend-onlyログ（HWM-before、equity、fee %、fee amount、settled-at） — 手数料レポートと請求用のファクトログ，而非アグリゲート。

## 設定＆API

| `App:Copy`設定 | デフォルト | 効果 |
|--------------------|---------|--------|
| `FeesEnabled` | `false` | 決済サービスを実行。 |
| `FeeSettlementInterval` | `1h` | エクイティがpollされ、手数料が決済される頻度。 |

Per-destination: `PerformanceFeePercent`（0–50）は宛先に設定されます（宛先追加/編集リクエスト）。

- `GET /api/copy/profiles/{id}/fees` — プロファイルの手数料発生 + 請求合計。

## テスト

- **ユニット**（`CopyPerformanceFeeTests`） — HWM不変量：最初の決済がシード+請求なし；新高はピーク上の利益のみを請求；ピーク以下では請求なし+ピークが後退しない；ドローダウンの後、古いピークを回復した後のみ請求；0%は決して請求しない；VOは範囲外のパーセントを拒否。
- **統合**（`CopyFeeSettlementTests`、real Postgres、fake equity reader） — seed→10k（請求なし、シード記録）、12k（400請求、マーク前進）、11k（請求なし、マーク保持）；発生が正しいowner/amountで永続化。

コピーホストは手数料で不改変（決済は別DBジョブ）、そのためコピーDSTストレススイートは影響を受けません（23/23）。
