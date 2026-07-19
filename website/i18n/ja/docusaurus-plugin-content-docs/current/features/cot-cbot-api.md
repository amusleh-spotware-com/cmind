# COT cBot API

Commitment of TradersデータはcBotおよび外部クライアント向けに認証されたREST APIで公開されているため、戦略はポジショニング（純ポジション、建玉の％、COTインデックス）をシグナル入力として抽出できます。
これは**同じJWTメカニズムと`market:read`スコープ**を通貨強度市場APIとして再利用します — 1つのトークン、1つのスキーム。

## Authentication

1. アプリで、市場データAPIクライアント（所有者）を発行し、**`market:read`**スコープを付与します。
2. クライアントid/secretを短寿命ベアラートークンと交換します：

   ```http
   POST /api/calendar/v1/token
   Content-Type: application/json

   { "clientId": "…", "clientSecret": "…" }
   ```

   レスポンスは`token`、`expiresAt`、および付与された`scopes`を含みます。
3. すべてのCOT呼び出しでトークンを送信します：

   ```http
   Authorization: Bearer <token>
   ```

トークンなし/無効なトークンは`401`を返します；`market:read`なしのトークンは`403`を返します。

## Endpoints

ベースパス `/api/market/v1/cot`。すべてのレスポンスはJSONです。

| Method & path | Purpose |
|---------------|---------|
| `GET /markets` | 追跡対象契約市場カタログ。オプショナル`group`（Fx、Metals、Energy、Agriculture、Softs、Rates、Indices、Crypto）およびキーワード`q`。 |
| `GET /latest?code={code}&kind={kind}&combined={bool}` | 市場の最新週次スナップショット。 |
| `GET /history/{code}?kind={kind}&combined={bool}&from={iso}&to={iso}` | ウィンドウ上の週次履歴。 |

Parameters:

- `code` — CFTC契約市場コード（例：Euro FXの場合`099741`；`/markets`から取得）。
- `kind` — `Legacy`（デフォルト）、`Disaggregated`、または`Tff`。
- `combined` — 先物+オプションの場合`true`、先物のみの場合`false`（デフォルト）。
- `asOf`（ISO-8601、オプショナル） — 特定時点アンカー：その瞬間に公開されたレポートのみが返されるため、
  バックテストはルックアヘッドを見ません。

### Example

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

## MCP tools

同じ読み取りモデルはAIクライアント向けのMCPツールとして利用可能です：`CotMarkets`、`CotLatest`、`CotHistory`
および`CotHealth` — 各々はオプショナルな`asOf`経由で特定時点正確です。完全な全体像については
[Commitment of Traders機能](./cot-report.md)を参照してください。

## Gating

APIはページと同じ2段階ゲートの背後にあります：`App:Branding:EnableCot`および`App:Features:Cot`。
どちらかオフの場合、`/api/market/v1/cot`下のすべてのルートは`404`を返します。
