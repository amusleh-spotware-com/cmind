# COT cBot API

交易商承诺数据通过经过身份验证的 REST API 对 cBots 和外部客户公开，
因此策略可以拉取头寸（净头寸、% 开放权益、COT 指数）作为信号输入。
它重新使用**与货币强度市场 API 相同的 JWT 机制和 `market:read` 范围**——一个令牌，一个方案。

## 身份验证

1. 在应用中，发行市场数据 API 客户端（所有者）并授予其 **`market:read`** 范围。
2. 将客户端 id/secret 交换为短期持有者令牌：

   ```http
   POST /api/calendar/v1/token
   Content-Type: application/json

   { "clientId": "…", "clientSecret": "…" }
   ```

   响应包含 `token`、`expiresAt` 和授予的 `scopes`。
3. 在每个 COT 调用上发送令牌：

   ```http
   Authorization: Bearer <token>
   ```

缺少/无效的令牌返回 `401`；没有 `market:read` 的令牌返回 `403`。

## 端点

基本路径 `/api/market/v1/cot`。所有响应都是 JSON。

| 方法和路径 | 目的 |
|---------------|---------|
| `GET /markets` | 追踪的合约市场目录。可选的 `group`（Fx、Metals、Energy、Agriculture、Softs、Rates、Indices、Crypto）和 `q` 关键字。 |
| `GET /latest?code={code}&kind={kind}&combined={bool}` | 市场的最新每周快照。 |
| `GET /history/{code}?kind={kind}&combined={bool}&from={iso}&to={iso}` | 窗口内的每周历史。 |

参数：

- `code` — CFTC 合约市场代码（例如 `099741` 对于欧元 FX；从 `/markets` 获取）。
- `kind` — `Legacy`（默认）、`Disaggregated` 或 `Tff`。
- `combined` — `true` 用于期货 + 期权，`false`（默认）仅用于期货。
- `asOf`（ISO-8601，可选）— 时间点锚：仅返回在该时刻公开的报告，
  因此回测不会看到前瞻。

### 示例

```http
GET /api/market/v1/cot/latest?code=088691&kind=Legacy HTTP/1.1
Authorization: Bearer <token>
```

```json
{
  "contractCode": "088691",
  "marketName": "Gold",
  "kind": "Legacy",
  "combined": false,
  "reportDate": "2024-01-02T00:00:00+00:00",
  "knownAt": "2024-01-05T20:30:00+00:00",
  "openInterest": 450000,
  "cotIndex": 82.4,
  "extreme": "LongExtreme",
  "categories": [
    { "category": "NonCommercial", "long": 250000, "short": 90000, "net": 160000, "longPercentOfOi": 55.5 }
  ]
}
```

## MCP 工具

AI 客户可以使用相同的读取模型作为 MCP 工具：`CotMarkets`、`CotLatest`、`CotHistory`
和 `CotHealth` — 每个都通过可选的 `asOf` 获得时间点正确性。请参阅
[交易商承诺功能](./cot-report.md)以获得完整的情景。

## 门控

API 位于与页面相同的两级门控后面：`App:Branding:EnableCot` 和 `App:Features:Cot`。
禁用其中任何一个时，`/api/market/v1/cot` 下的每个路由都会返回 `404`。
