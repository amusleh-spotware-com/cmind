---
description: "Transaction Cost Analysis — 注文の執行品質（basis pointsおよび実装不足の手紙におけるスリッページ）を到着価格に対して測定、银行が живущих на実行エッジの一部。決定的。"
---

# Transaction Cost Analysis (TCA)

執行alphaは取引ごとに微量で、数千の上で巨大 —银行とpropデスクがエッジを維持する方法の一部です。TCAは*決定した*価格から実際に達成した価格がどの程度ずれたかを測定します。

**cBots → Execution Cost**（`/quant/tca`）で開きます。

## 何を測定するか

**到着（決定）価格**、**サイド**、あなたの**fills**（price × quantity）が与えられると、以下を報告します：

- **平均fill価格（VWAP）** — 実際に取得したvolume加重価格。
- **スリッページ（bps）** — 到着からVWAPへのbasis pointsでのドリフト、**署名付きで正数がコスト**（購入が到着超または売りが到着以下に）負数が価格改善。
- **実装不足** — そのコストをprice × quantitytermsで表現：ドリフトがこの注文であなたに使ったお金。

```http
POST /api/quant/tca
{ "arrivalPrice": 1.1000, "side": "Buy",
  "fills": [ { "price": 1.1010, "quantity": 100 }, { "price": 1.1020, "quantity": 100 } ] }
```

## スマートスライシング（Almgren-Chriss）

コストの測定を超えて、cMindは大型注文を*最小化*するように計画できます。**cBots → Execution Schedule**（`/quant/execution`）は**Almgren-Chriss最適執行スケジュール**を構築：总量、数量、スライスの数、リスク回避/volatility、一時的な市場影響が与えられると、各スライスで取引するサイズを返します。高いリスク回避はスケジュールを**フロントロード**（タイミングリスクを削減）；ゼロリスク回避は均等な**TWAP**に平坦化。スライスは常に总量に合計します。

```http
POST /api/quant/execution-schedule
{ "totalQuantity": 100, "slices": 5, "riskAversion": 2, "volatility": 0.02, "temporaryImpact": 0.1 }
```

## これが信頼できる理由

純粋で決定論的なドメインベコード（`Core.Execution`）インフラ依存なし、外部呼び出しなし — 买入/卖出コスト符号、価格改善、ゼロスリッページ、VWAP集計、入力ガードのユニットテスト済み。これは執行品質の測定半分です；コピーエンジンがミラー注文のコストを判断し（そしてスマートスライシングで削減）するために使用する同じ不足メトリクスです。
