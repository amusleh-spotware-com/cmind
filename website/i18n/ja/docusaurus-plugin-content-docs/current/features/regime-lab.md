---
---
description: "Regime Lab — リターンシリーズをCalm / Normal / Turbulent volatilityレジームにラベル付けし、per-regimeパフォーマンスを報告plus Hurst指数（トレンド持続性 vs 平均回帰）。決定的。"
---

# Regime Lab

単一のSharpe比はほとんどのedgeが条件付きであるという真実を隠します：静かな、トレンド市場で得很好で、乱流で死んでいる（またはその逆）。Regime Labは戦略の歴史をvolatilityレジームに分割し、それぞれでどのようにしたかを示します — 因此 あなたのedgeが実際にどこに存在するかがわかります。

**cBots → Regime Lab**（`/quant/regimes`）で開きます。

## 何をするか

リターンシリーズ（またはエクイティカーブ、最も古い最初）が与えられると：

- 各点で**trailing実現volatility**を計算し、履歴を**Calm**、**Normal**、**Turbulent**レジームに分割します。
- **per-regimeパフォーマンス**を報告 — 観察、平均リターン、volatility、Sharpe — 因此 edgeがどこにあるかが見えます；
- **Hurst指数**をrescaled-range（R/S）分析で推定：~0.55以上はシリーズは**トレンド/持続**、~0.45以下は**平均回帰**、~0.5の場合はランダムウォークに近い。

```http
POST /api/quant/regimes
{ "returns": [...], "window": 10 }   // or { "equity": [...] }
```

## これが信頼できる理由

純粋で決定論的なドメインベコード（`Core.Regimes`）イン费依存なし、外部呼び出しなし — レジーム分離（静かな vs 乱流volatility）およびHurst方向（反持続的シリーズは0.5未満でスコア、持続的トレンドは0.5以上でスコア）のユニットテスト済み。同じレジームシグナルは自律型エージェントのリフレクションループに供給されるため、エージェントはedgeが realであるレジームに傾くことができます。
