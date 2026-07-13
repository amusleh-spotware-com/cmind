---
description: "経済カレンダーはバージョン付き、JWT保護、レート制限付きのREST APIとして公開 — フラッグシップ統合表面。FXStreet Calendar APIと機能パリティを持ち、point-in-time asOf、完全リビジョン連鎖、決定論的影響根拠、サプライズ分析、国→シンボル解決、他のカレンダーAPIが公開していないblackout計算を備えています。"
---

# カレンダーREST & cBot API

経済カレンダーは**バージョン付き、JWT保護、レート制限付きのREST API**として公開されます — フラッグシップ統合表面。任意の外部サービス、ダッシュボード、またはcBotが製品として統合します。FXStreet Calendar APIと機能パリティを持ち、それを上回ります：point-in-time `asOf`、完全リビジョン連鎖、決定論的影響根拠、サプライズ分析、国→シンボル解決、他のカレンダーAPIが公開していないblackout計算。

> **ステータス。** JWTセキュリティ（クライアント発行+トークン交換）、ゲーティング、コア読み取りエンドポイント — `token`、`events`、`events/{id}`、`history`、`series`、`surprises`、`next`、`blackout`、`affected-symbols`、`health` — は**実装済みで統合テスト済み**（auth、scope強制、feature/white-label 404）、Plus **`events/batch`**（バウンド多重化）と discoverable **`/openapi.json`** ドキュメント、**`ETag`/`If-None-Match` 304** on event/history reads、**keyset cursor pagination**（`Link: rel="next"`）、**SSE `stream`**（ライブ`event: release`プッシュ、ポールバック）、**HMAC署名付きwebhooks**（`X-CMind-Signature: sha256=…`、owner登録、設定ゲート付きワーカーが永続化了 Watermarkから配送）、そして出荷された**型付けクライアント**（`CmindCalendarClient`）。フル公開API表面が実装されています。

## セキュリティ — JWT

APIはリポジトリの既存のHS256トークンマシナリーを再利用します（CtraderCliNodeエージェントが使用する同じパターン）：

- アプリ管理者が**Calendar APIクライアント**（名前+スコープ+有効期限）を発行。クライアントは`POST /api/calendar/v1/token`でidとsecretを交換して**短命HS256 JWT**（`iss=cmind-calendar`、`aud=calendar-api`、`exp` ~15分、`scope`クレーム）を取得。只有短命JWTがリクエストに載ります（`Authorization: Bearer <jwt>`）。
- クライアントsecretは`ISecretProtector`で**暗号化**保存 — 平文ではなく、ログにもならない。
- **スコープ**（最小権限）：`calendar:read`、`calendar:blackout`、`calendar:surprises`、`calendar:stream`。cBotトークンは通常`read` + `blackout`のみを取得。
- 標準`JwtBearer`検証（issuer、audience、lifetime、署名キー; `alg=none`拒否; タイトクロックスキュー）。パーclientトークンバケットレート制限+グローバルリミッター; `429` with `Retry-After`。すべてのauth失敗は監査済み。
- クライアントの無効化は将来のトークン発行を直ちに停止; 短命JWT寿命が漏れたトークンをバウンド。 featureが無効な場合、全`/api/calendar/**`ツリーが`404`を返します。

## コンベンション

- **ベースパス＆バージョン管理:** `/api/calendar/v1/...`（URLバージョン管理; 追加変更はバンプなし）。
- **形式:** JSON; RFC 3339 UTC instants plus明示的な`sourceTimeZone`; オプション`tz=`はUTCアンカーを失うことなく便宜的な現地時間を描画。
- **ページネーション:** cursor-based（`cursor`、`limit` ≤ 1000）; bodyと`Link`ヘッダーに`next` cursor。
- **キャッシング:** `ETag` + `If-None-Match`; 歴史的範囲は長いTTL、今後のものは短い。
- **エラー:** RFC 7807 `problem+json`、決して生の`500`ではない。
- **劣化読み取り:** source/DB障害は`200` best-known data plus `X-Calendar-Freshness`/`stale=true` сигнал（または真有に何も知られていない場合のみ`503 Retry-After`）を返します — cBotが判断。

## エンドポイント

| メソッド＆パス | 目的 | 主要パラメータ |
|---|---|---|
| `POST /v1/token` | 	client id+secret → 短命JWT | body: `clientId`, `clientSecret` |
| `GET /v1/events` | ウィンドウ内のイベント（今候または歴史的） | `from`,`to`,`countries`,`currencies`,`series`,`minImpact`,`category`,`q`,`asOf`,`cursor`,`limit`,`tz` |
| `GET /v1/events/{id}` | 1つのイベント：完全リビジョン連鎖、サプライズ、影響根拠、影響を受けるシンボル | `watchlist?`,`asOf?` |
| `GET /v1/events/{id}/revisions` | 順序付きリビジョン履歴 | — |
| `GET /v1/history` | シリーズの詳細歴史的プル（≥10y） | `series`,`from`,`to`,`asOf`,`cursor`,`limit` |
| `GET /v1/series` | 追跡中のインジケーター＋cadence＋ソースのカタログ | `countries`,`currencies`,`q` |
| `GET /v1/surprises` | 歴史的actual/forecast/surprise z-scoreシリーズ | `series`,`count`/`from,to` |
| `GET /v1/next` | シンボルに対する次の関連リリース（国→シンボルマッピング） | `symbol`,`minImpact` |
| `GET /v1/blackout` | シンボルが今すぐ/Tでhigh-impactウィンドウ内有無 | `symbol`,`at?`,`minImpact`,`before`,`after` |
| `GET /v1/affected-symbols` | イベント→監視リスト内のシンボル解決 | `eventId`,`watchlist` |
| `POST /v1/events:batch` | 1つのラウンドトリップで複数クエリを多重化 | body: クエリの配列 |
| `GET /v1/stream` (SSE) | ライブプッシュ: releases/revisions/window-enter | `currencies`,`minImpact` (scope `calendar:stream`) |
| `POST /v1/webhooks` | release/revision/blackoutのHMAC署名付きコールバックを登録 | body: url, filters, secret |
| `GET /v1/health` | ソースごと新鲜度＋カバレッジ | — |

## Blackout — cBotニュースフィルター

`GET /v1/blackout`は`{ inBlackout, event, startsAt, endsAt, stale }`を返します。不確実性ではデフォルトで**設定された保守的な回答**（デフォルトでfail-closed：リスクオフbotの「blackout内と仮定」）plus `stale` flag — データギャップはNFPを通じてトレーディングを許可することは決してありません。エンドポイントはハードサーバータイムアウトで純粋なDB/キャッシュ読み取りです；ホットパスで同期origin fetchはありません。

出荷された型付けクライアント（`Infrastructure.Calendar.CmindCalendarClient`）がこれをラップ：その`HttpClient`をAPIルートに向ける、`GetTokenAsync(clientId, clientSecret)`を1回呼び出す、次に各注文前に`GetBlackoutAsync(token, symbol)` — **fail-safe by construction**（非成功または解析エラーはすべて`InBlackout = true, Stale = true`を返すので、データギャップはトレーディングを許可することは決してない）。cBotは以下のようにニュースの周りで一時停止します：

```csharp
// cTrader cBotでWebRequest + Calendar APIクライアントトークンを使用する擬似コード。
var jwt = CalendarApi.GetToken(clientId, clientSecret);           // POST /v1/token
var res = CalendarApi.Blackout(jwt, symbol: SymbolName,           // GET  /v1/blackout
                               minImpact: "High", before: 15, after: 15);
if (res.InBlackout || res.Stale)                                  // fail-safe: stale ⇒ treat as blackout
    return;                                                       // skip new entries in the news window
// ...otherwise proceed to place the order
```

## バックテストのポイントインタイム

任意の読み取りに`asOf`を渡して過去の瞬間の正確なカレンダーを取得 — actuals、forecasts、revisions *がその時点でそうだったもの*。`asOf`読み取りは純粋でキャッシュ可能なため、履歴を打つバックテストは毎回同じバイトを取得し、バックテストされたニュースルールはライブと同じ動作をします（履歴で改訂値からのルックアヘッドなし）。

## algo呼び出し元のレジリエンス

APIはトレーディングホットパスに座るため、ライブbotにthrowすることは決してありません：すべてのパスが整形式`problem+json`または型付けされた劣化ボディを返します。コピートレーディングのレジリエンスプリミティブを再利用 — 各ソースクライアント上の標準HTTPレジリエンスハンドラー、ソースごとのドメインサーキットブレーカー、起動時協調を持つリース保護されたシングルトンインジェストワーカー、`/health`に Wired health checks。出荷された型付けクライアントスニペットには、再試行+タイムアウト+サーキットブレーカーが事前設定されており、bot作成者はレジリエンスを継承します。

## 兄弟：AI通貨強さ（`market:read`）

[AI macro currency-strength](./currency-strength.md)読み取りモデルは**同じ**JWTマシナリーに乗ります — 1つのスキーム、1つの署名secret、1つのレート limiter — `market:read`スコープのみを追加。そのスコープでAPIクライアントを登録、同じようにトークンを交換し、呼び出します：

```
GET /api/market/v1/currency-strength/latest?horizon=3M&tier=Majors
GET /api/market/v1/currency-strength/history?days=30
GET /api/market/v1/currency-strength/pair/EUR/USD?horizon=3M
```

```csharp
// POST /api/calendar/v1/token経由でトークンを取得後:
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
var view = await http.GetFromJsonAsync<JsonElement>(
    baseUrl + "/api/market/v1/currency-strength/latest?horizon=3M");
// view.ranking[], view.forecasts[], view.pairs[] (bias/conviction), view.narrative
```

`market:read`がないトークンは`403`を取得; 期限切れ/改ざんされたトークンは`401`を取得。エンドポイントはAI feature flagでゲートされ、カレンダーfeature gate независимый`/api/market/v1`で提供されるため、カレンダーfeature gateから独立しています。run/backtestディスパッチで、デプロイメントは`CMIND_API_BASEURL` + 短命`market:read`トークンを注入して、cBotがゼロのクライアント登録でコールバックできるようにします。
