---
description: "Strategy Health & Alpha Decay — 戦略の最近のSharpeを以前のレコードと比較し、最大平均シフト（CUSUM変化点）をロケートし、Healthy / Degrading / Decayed判定を返す決定論的崩壊検出。"
---

# Strategy Health & Alpha Decay

すべてのedgeは崩壊します — クォン研究はクォン戦略のhalf-lifeが年月からヶ月に短縮されたことを明確にしています therefore *適応は発見に勝ります*。Strategy Healthモニターは、戦略自身のリターン履歴から、edgeがまだそこにあるかどうかを伝えます。

**cBots → Strategy Health**（`/quant/health`）で開きます。

## 何をするか

リターンシリーズ（またはエクイティカーブ、最も古い最初）が与えられると：

- 履歴を**以前**と**最近**の半分に分割し、Sharpe比を比較します；
- **CUSUM変化点**スキャンを実行して、平均が最も明確にシフトした観察をロケートします（レジームブレイク）、偏差が統計的に注目に値るときのみ報告されます；
- 判定を返します：

| 判定 | 意味 |
|---|---|
| **Healthy** | 最近のパフォーマンスは以前のレコードと一致（またはそれより優れています）。 |
| **Degrading** | 最近のSharpeは以前のレコードより著しく弱い — 密切に監視。 |
| **Decayed** | edgeは最近のウィンドウで実質的に消滅しました — 一時停止を検討。 |
| **Unknown** | 判断するのに十分な履歴がありません。 |

```http
POST /api/quant/health
{ "returns": [...] }   // or { "equity": [...] }
```

## これが信頼できる理由

純粋で決定論的なドメインベコード（`Core.Health`）インフラ依存なし、外部呼び出しなし — decay、degrading、healthy、too-shortケースと変化点ローカライゼーションのユニットテスト済み。これは常駐の健康チェックの MANUAL comp-union to自律型エージェント：同じ統計が、edgeが衰えているライブ戦略を危険から除外するサーキットブレーカーを驱动します。
