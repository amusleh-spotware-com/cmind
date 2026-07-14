---
description: "テストスイートが必要とするすべての認証情報は単一のgitignoreされたファイルに配置されます：secrets/dev-credentials.local.json。コミットされたテンプレートをコピーしてあなたが持つものを入力します — 各値はオプションであり、欠落している値を必要とするテストは正常にスキップします。"
---

# 開発認証情報 — すべてのテストに1つのファイル

テストスイートが必要とするすべての認証情報は単一のgitignoreされたファイルに配置されます：`secrets/dev-credentials.local.json`。コミットされたテンプレートをコピーしてあなたが持つものを埋めます — 各値はオプションであり、欠落している値を必要とするテストは正常にスキップします。

```bash
mkdir -p secrets
cp dev-credentials.example.json secrets/dev-credentials.local.json
# edit secrets/dev-credentials.local.json
```

## 各テスト層が読み取るもの

| 層 | 必要 | 送信元 |
|------|-------|------|
| **ユニット**（`tests/UnitTests`） | nothing | — 决定的、ネットワークなし、シークレットなし |
| **統合**（`tests/IntegrationTests`） | Postgres | Testcontainers（Docker） — 自動 |
| **ライブコピー**（`tests/IntegrationTests/CopyLive`） | OpenAPI app + トークンキャッシュ | `OpenApi.App`、`OpenApi.Tokens` |
| **E2Eオンボーディング**（`tests/E2ETests/CopyLive`） | OpenAPI app + cIDログイン | `OpenApi.App`、`OpenApi.Cids` |
| **E2Eリアルrun/backtest**（`CBotRealRunBacktestTests`） | cIDログイン + **デモ**アカウント番号 | `OpenApi.Cids[].{Username,Password,Accounts}` |
| **AI機能** | Anthropicキー | `Ai.ApiKey`（未設定 ⇒ AI機能が無効を返し、 appは引き続き実行） |
| **Live economic-calendar sources** (`tests/IntegrationTests/Calendar/CalendarSourceLiveTests`) | FRED / BLS API keys | `Calendar.FredApiKey`, `Calendar.BlsApiKey` (unset ⇒ that source's live test skips; the keyless central-bank schedule still works) |

## スキーマ

`dev-credentials.example.json`をrepo root参照。セクション：

- `OpenApi.App` — `{ ClientId, ClientSecret }`のcTrader Open APIアプリケーション。
- `OpenApi.Cids` — ヘッドレスOAuthオンボーディングで使用されるcTrader IDログイン。各エントリは`**Accounts**`配列 — そのcIDの下でのcTraderトレーディングアカウント番号（ログイン/アカウント番号、例： `3635817`）、テストインフラストラクチャがリンクして駆動できるアカウント。`CBotRealRunBacktestTests`は非空`Accounts`配列を持つ最初のエントリを読み取り、そのcID + アカウントをアプリに追加し、次にそれに対して実際にcBotを実行してバックテストします。**ここにデモアカウント番号のみを置いてください** — 決して本番アカウント； run/backtestテストはあなたがリストするすべてのアカウントに実際の注文を配置します。空/省略`Accounts` ⇒ リアルrun/backtestテストは正常にスキップ。
- `OpenApi.Tokens` — 承認されたcIDごと（refresh/accessトークン + アカウントリスト付き）のマルチcIDトークンキャッシュ。オンボーディングとトークンリフレッシュステップによって自動的に書き込まれます；あなたが編集することはほとんどありません。
- `Owner` — E2E下のアプリのシードownerログイン。
- `Database.ConnectionString` — Testcontainersではなく外部Postgresにテストを向ける場合のみ。
- `Ai.ApiKey` — AI機能用のAnthropic APIキー。
- `Calendar.FredApiKey` — [FRED](https://fredaccount.stlouisfed.org/apikeys) (St. Louis Fed) API key. The primary economic-calendar value source (interest rates, inflation, employment).
- `Calendar.BlsApiKey` — [BLS](https://data.bls.gov/registrationEngine/) (US Bureau of Labor Statistics) v2 registration key (CPI, PPI, employment, JOLTS). Absent ⇒ the low-quota public tier.

  Both feed the exact `FredSource`/`BlsSource` the ingestion worker uses. With a key present, `CalendarSourceLiveTests` hits the real provider and asserts observations come back; absent, that source's test skips cleanly. The app also reads these at runtime via `App:Calendar:FredApiKey` / `App:Calendar:BlsApiKey` (environment variables override — e.g. `FRED_API_KEY`, `BLS_API_KEY`).

## 優先順位

1. **環境変数**がすべてをオーバーライド（例：`App__OwnerPassword`、`App:Ai:ApiKey`）。
2. **`secrets/dev-credentials.local.json`** — 統一ファイル（優先）。
3. **レガシー分割ファイル** — `openapi-test-app.local.json`、`openapi-cids.local.json`、`openapi-tokens.local.json`は統一ファイルが存在しないときにまだ読み取られます поэтому 既存のマシンは引き続き動作。新しいセットアップは単一ファイルを使用する必要があります。

## 安全

- `secrets/`と`*.local.json`はgitignored — ここにあるものは決してコミットされません。
- ライブコピーテストは非デモアカウントでの実行を拒否します（`IsLive`アカウントは`LiveCopyFixture`によってフィルターアウトされます）。トークンキャッシュにデモアカウントのみを保持してください。
- クラスター内（Kubernetes）ランはファイルをread-only Secretとしてマウントします； トークンリフレッシュはメモリに保持され、read-only write-backは沈黙のno-opです。
