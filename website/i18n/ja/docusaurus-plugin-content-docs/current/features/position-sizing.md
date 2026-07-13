---
description: "零售の機関サイズ設定 — 単一戦略のvolatilityターゲティングとfractional-Kellyエクスポージャー、ポートフォリオ全体の相関マトリックスを介した逆volatilityリスクパリティ配分。"
---

# Position Sizing & Portfolio

「このトレードのサイズはいくらにすべきか？」はedgeが複利になるか爆破するかを決定する問題です。機関は**volatilityターゲティング**と**Kelly基準**で答え、ブックを構築的时候我ら**リスクパリティ**而非平等ドルで構築します。cMindは両方とも零售にを提供します — 戦略のリターンシリーズの決定的数学、平易な英語の推奨事項付き。

**cBots → Position Sizing**（`/quant/sizing`）で開きます。

## 単一戦略のサイズ設定

戦略のリターン（またはエクイティカーブ）、目標年間volatility、Kelly分数、レバレッジキャップが与えられると、サイザーは以下を報告します：

- **実現年間volatility** — 戦略自身のvolatility、年率（平方根時間ルール）。
- **Volatilityターゲットサイズ設定** — 実現volatilityが目標を満たすエクスポージャー（`target ÷ realized vol`）、あなたのレバレッジ制限でキャップ。低vol戦略はより多くのサイズ獲得。
- **Full Kelly** — 成長最適な分数 `f* = μ / σ²`（リターンの平均 over分散）。
- **Fractional Kelly** — `f*`をあなたのKelly分数でスケール。Half-Kelly（0.5）は一般的な安全な選択です； full Kellyは real、不確実なedgeには有名なほど攻撃的。
- **推奨エクスポージャー** — volatilityターゲットとfractional-Kellyサイズ設定の**より小さい**（安全な）、キャップ付き。エッジのない戦略（full Kelly ≤ 0）は**ゼロ**にサイズ設定。

```http
POST /api/quant/sizing
{ "returns": [...], "targetVolatility": 0.10, "kellyFraction": 0.5, "leverageCap": 3 }
```

## ポートフォリオ配分

2つ以上の戦略（整列したリターンシリーズ）を与她ると、**逆volatilityリスクパリティ**でブックを構築します — 各戦略は`1 / volatility`で重み付けられ、正規化 — 因此 risk而非 dollarsが 均等に共有されます。入力を返します：

- 戦略間の**相関マトリックス**（密かに同じ賭けであるものを特定）；
- その重みでの**プロジェクトのポートフォリオvolatility**、サンプル共分散から；
- ブック全体を目標volatilityに向かってスケールする**レバレッジ**係数（キャップ付き）。

```http
POST /api/quant/portfolio
{ "strategies": [[...], [...]], "targetVolatility": 0.10, "leverageCap": 3 }
```

## これが信頼できる理由

すべては純粋で決定論的なドメインベコード（`Core.Portfolio`）であり、インフラ依存なし、外部呼び出しなし — vol-targetスケーリング、Kelly式、逆volatility重みの等リスク特性、相関マトリックスのユニットテスト済み。デフォルトでアドバイザリー：数字は推奨事項であり、而非自動注文。
