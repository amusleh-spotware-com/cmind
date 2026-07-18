---
description: "すべてのユーザーに1つのcTrader Open APIアプリケーションを出荷し（white-label共有モード）、登録する1つのredirect URL、per-message-typeクライアントレート制限。"
---

# 共有Open APIアプリケーション＆レート制限

デフォルトではすべてのユーザーが**自分の** cTrader Open APIアプリケーションを**Settings → Open API**に登録します。White-label演算子（typically cTrader brokerまたは再販業者）は代わりに**すべてのユーザー向けの1つの共有Open APIアプリケーション**を出荷できます — 誰も自分のを登録しない；誰もが演算子の単一アプリを通じてアカウントを承認します。

## 共有アプリケーションを提供する2つの方法

共有アプリケーションはデプロイメント設定**または**所有者設定UIのいずれかからプロビジョニングされます（所有者設定の値が優先）。1回提供すると共有モードが全員に対してオンになります。

### 1. デプロイメント設定（起動時にシード）

```jsonc
"App": {
  "OpenApi": {
    "PublicBaseUrl": "https://cmind.yourbroker.com",   // このデプロイメントの標準的なpublic URL
    "SharedApp": {
      "Enabled": true,
      "Name": "YourBroker Open API",
      "ClientId": "1234_abcd...",
      "ClientSecret": "…"                                // 保存時に暗号化；ログに記録されない
    }
  }
}
```

起動時にアプリが所有者アカウントが所有する1つの共有アプリケーションをシードします（べき等 — 決して上書きしません所有者編集runtime値、および再シードはno-op）。

### 2. 所有者設定（runtime、再デプロイ不要）

**Settings → Open API**（ownerのみ）には2つのものが表示されます：**Your Open API application**セクション — 所有者が自分の**own** per-userアプリを登録、編集、承認します。これは任意のユーザーと同じ方法で行います（共有アプリが設定されていない間に利用可能）。および**Deployment shared application**カードで、共有アプリを追加 / 編集 / 削除します。redirect URLが表示されてコピー用に用意されています。新規認証には直ちに効果的です。共有アプリが設定されたら、所有者の自分のアプリに優先し、**Your Open API application**セクションはアカウントが共有アプリを通じて承認することを通知するnoticeに切り替わります。

## redirect URL（cTraderに登録）

すべてのcTrader Open APIアプリケーションは**1つの**redirect URLを登録します — **共有アプリとper-userアプリの両方に同じ単一値**：

```
{your deployment URL}/openapi/callback
```

例：`https://cmind.yourbroker.com/openapi/callback`。

-  앱**は正確に値を表示** Open API設定ページで（コピーボタン付き） — cTraderパートナーポータルでOpen APIアプリケーションを作成時にそこに貼り付けます。
- `App:OpenApi:PublicBaseUrl`から構成されるため、リバースプロキシ / CDNの背後で安定します； 未設定の場合、受信リクエストホストにフォールバック。
- 招待者 vs 通常ユーザーの体験は**コールバック後**にユーザーが着地する場所でのみ異なります（アカウントリスト vs 「アカウントが追加されました」確認） — 登録されたredirect URLは変わりません。

## 共有モードでユーザーに表示されるもの

共有アプリケーションが存在するとき：

- ユーザーは自分のOpen APIアプリケーションを登録する**オプションはなく**、設定ページに**「Open APIはプロバイダーによって管理されています」**と表示され、共有アプリを使用する**Authorize accounts**ボタンが表示されます。
- 既存の個人アプリケーションは**削除されます**；承認されたアカウントは共有アプリに再 указаныされ、**再承認が必要**です（古いトークンは異なるクライアントidの下で発行されました）。個人アプリの作成を試みると「プロバイダーによって管理されています」エラーを返します。

## クライアントレート制限（perメッセージタイプ）

クライアントはバーストがサーバー側レート制限ブロックをトリップしないようにアウトバウンドcTrader Open APIメッセージをペーシングします。制限は**perメッセージタイプ**で、cTrader Open APIドキュメントと一致：

| カテゴリ | 対象 | デフォルト |
|---|---|---|
| `General` | 取引 + 読み取りメッセージ（注文、シンボル、アカウントクエリ） | 45 msg/s |
| `HistoricalData` | trendbar / tick-dataリクエスト（cTraderによりより積極的にスロットル） | 5 msg/s |

履歴データリクエストは собственную bucketと一般bucketの両方にカウントされます。Heartbeatと認証メッセージは決してペーシングされません。メッセージはバーストし 使用可能なレートでドレン — 何もドロップされず注文が保持されます。

高いcTrader制限をネゴシエートした場合はそれらを調整するか、カテゴリを`**0**`に設定してペーシングを完全に無効にします（無制限）：

- **設定:** `App:OpenApi:RateLimits:General` / `App:OpenApi:RateLimits:HistoricalData`（msg/sec）。
- **所有者設定:** **Settings → Open API**の**Client rate limits**カード（owner overrideが優先、新しい接続 / 再接続時に適用）。
