---
description: "再販業者リブランドapp — 商品名、logo、favicon、色、カスタムCSS — デプロイメント設定経由、コード変更なし。すべてのブランディング值はstock identityにデフォルト設定：未設定デプロイメントは以前と同じに見えます；再販業者が必要なもののみをoverride。"
---

# White-labelブランディング

再販業者リブランドapp — 商品名、logo、favicon、色、カスタムCSS — デプロイメント設定経由、コード変更なし。すべてのブランディング值は**stock identityにデフォルト設定**：未設定デプロイメントは以前と同じに見えます；再販業者が必要なもののみをoverride。

## モデル

- `Core.Options.BrandingOptions` — `App:Branding`からバインド。文字列ベース（設定edge）；各色はテーマ構築時に検証。
- `Core.Branding.HexColor` — CSS hex色の値オブジェクト（`#RGB` / `#RRGGBB`）、不変、自己検証。無効な色は`DomainException`（`domain.branding.color_invalid`）をスロー — 設定ミスのデプロイメントは起動時に即座に失敗し，而非壊れたパレットでレンダリング。
- `Web.Components.Theme.Build(BrandingOptions)` — ブランディングからMudBlazorテーマを生成。ブランド化されたパレットエントリのみが設定から来ます；typography、レイアウト、ニュートラル表面トーンは固定 поэтому 商品在全再販業者間で一貫性を維持。
- `Web.Branding.IBrandingThemeProvider` — シングルトン、テーマは1回構築、オプション変更時に再構築。`MainLayout`/`EmptyLayout`の`MudThemeProvider`にinject、`AppBar`に商品名/logo用。`App.razor`は直接`IOptionsMonitor<AppOptions>`を読んでpage `<head>`（title、description、favicon、theme-colour、custom CSS）。

## 設定

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "Description": "AcmeFX — copy trading and strategy automation.",
      "LogoUrl": "/branding/acme-logo.svg",
      "FaviconUrl": "/branding/acme.ico",
      "PrimaryColor": "#2D7FF9",
      "SecondaryColor": "#1E63C8",
      "AppBarColor": "#0B1220",
      "BackgroundColor": "#0E1525",
      "SurfaceColor": "#161E30",
      "SuccessColor": "#3FB950",
      "ErrorColor": "#F85149",
      "WarningColor": "#D29922",
      "InfoColor": "#2D7FF9",
      "CustomCss": ".mud-appbar { letter-spacing: 1px; }"
    }
  }
}
```

環境変数形式：`App__Branding__ProductName=AcmeFX`、`App__Branding__PrimaryColor=%232D7FF9`。

| キー | 効果 | デフォルト |
|-----|---------|---------|
| `ProductName` | App-barテキスト + page `<title>` | `cMind` |
| `LogoUrl` | App-barロゴ画像；空の場合、商品名テキストが表示 | *(empty)* |
| `FaviconUrl` | `<link rel="icon">` | `favicon.svg` |
| `Description` | `<meta name="description">` | stock description |
| `PrimaryColor` / `SecondaryColor` | accent、drawerアイコン、ボタン | `#26C281` / `#1FB97A` |
| `AppBarColor` / `BackgroundColor` / `SurfaceColor` | chrome + surfaces； `AppBarColor`が`<meta theme-color>` + PWA manifest `theme_color`、`BackgroundColor`がmanifest `background_color`を驱动 | darkパレット |
| `SuccessColor` / `ErrorColor` / `WarningColor` / `InfoColor` | ステータス色 | stock |
| `CustomCss` | `<head>`に注入された`<style>`（デプロイメント信頼） | *(empty)* |
| `ShowSiteLink` | ダッシュボードに「Powered by cMind」creditリンクを表示 | `true` |
| `RequireMfa` | すべてのユーザーにアプリの使用前に二要素認証を設定することを要求 | `false` |
| `NodesUi` | どのNodes表面が出荷されるか： `Full`（リスト + 手動 add/delete）、`Monitor`（読み取り専用リスト、add/deleteなし）、`Hidden`（navなし、ページなし、手動APIなし） | `Full` |
| `RestrictNodesToOwner` | `true`の場合、ownerのみがノードを表示/管理可能；それ以外の場合は全admin-or-aboveスタッフ表面を表示可能。通常のユーザーはどちらでもノードを表示 never | `false` |

`LogoUrl`/`FaviconUrl`で参照されるアセットはWeb app `wwwroot`から提供（例： `wwwroot/branding/`フォルダーをマウント）または任意の絶対URL。

`App:Branding`は起動時に検証されます（`BrandingOptionsValidator`、実行 via `ValidateOnStart`）：各色は有効なhexで，`CustomCss`は`<`/`>`を含んではなりません（`<style>`タグから飛び出せない）。設定ミスれたデプロイメントは明確なメッセージで起動に失敗し，而非壊れたページを描画。

## Powered-byリンク

ダッシュボードはプロジェクトのドキュメントサイトを示す小さな**「Powered by cMind」**creditリンクを描画します。これは`App:Branding:ShowSiteLink`で制御され、**デフォルトで`true`** — 未設定のデプロイメントはそれを表示します。完全にホワイトラベルされたインスタンスを実行する再販業者`App__Branding__ShowSiteLink=false`を設定して完全に削除します。

リンクはダッシュボードコンポーネントからemitされ、`IBrandingThemeProvider` / `BrandingOptions`経由でフラグを読み取るため、それを切り替えは設定のみの変更（而非リビルド）。[White-label for business](../white-label-for-business.md#the-powered-by-cmind-link)のビジネス向けサマリーを参照。

## ブローカー許可リスト

white-labelデプロイメントはユーザーが追加できるブローカーのトレーディングアカウントを制限できます — 因此 brokerがcMindを自身のクライアントのみに実行している場合、常に自身のブックにサービスを提供します。`App:Accounts`で構成：

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Pepperstone", "IC Markets"]
    }
  }
}
```

環境変数形式：`App__Accounts__AllowedBrokers__0=Pepperstone`。

**動作：**

- **空のリスト（デフォルト） ⇒ 無制限。** すべてのbrokerが許可され**検証は実行されません** — 在庫デプロイメントは完全に不改変。
- **非空 ⇒ 制限。** cMindはユーザーが追加しようとする各アカウントをリストに対してチェックします（大文字小文字を区別しない）：
  - **Open API（OAuth）リンク** — broker名はcTrader Open APIからauthoritatively reportされるため、許可されていないアカウントは単純に**スキップ**されます（同じgrantの許可されたアカウントは引き続きリンク）；認証ページはユーザーにどのbrokerがスキップされたかを通知。
  - **手動cID（username / password）** — ユーザーが入力したbrokerは**信頼されていません**。cMindはcTrader CLI（`Account.BrokerName`を読んで）でブローカーprobe cBotを実行してアカウントの本当のbrokerを検証し、その検証された名前を永続化します。許可されていないbrokerは通知と共に拒否されます；検証失敗（悪い認証情報、ノードなし、タイムアウト）も表面化され、アカウントは追加されません。

**モデル：**

- `Core.Options.AccountsOptions` — `App:Accounts`からバインド（`AllowedBrokers`、`BrokerProbeTimeout`、`BrokerProbeAlgoPath`）。
- `Core.Accounts.BrokerName` — 値オブジェクト（トリム済み、大文字小文字を区別しない等価）。
- `Core.Accounts.BrokerAllowlist` — `IsRestricted` / `Allows(broker)`； 空 = すべてを許可。`CTraderIdAccount.AddTradingAccount` / `LinkOpenApiAccount`内の不変量として強制（`domain.account.broker_not_allowed`）。
- `Core.Accounts.IBrokerVerifier` → `Web.Accounts.BrokerVerifier` — Webホスト（Docker socketを持つ）でprobeコンテナを実行、ログをtail、 `Core.Accounts.BrokerProbeOutput`でbrokerを解析。許可リストが制限されている場合にのみ呼び出される。

**ブローカーprobe cBot：** 事前構築された`broker-probe.algo`がWeb appと共にが出荷されます（`src/Web/BrokerProbe/`、出力に`broker-probe/broker-probe.algo`としてコピー）、，因此在箱から出してすぐにデフォルト`App:Accounts:BrokerProbeAlgoPath`が解決されます — 相対パスはappベースディレクトリに対して解決され、絶対パスはそのまま使用されます。ソースは`tools/broker-probe/`にあります。algoが存在しない場合、手動cID検証は閉じられます — 制限された許可リスト下のアカウントは依然としてOpen APIパス経由でリンクできます probeが必要です。

## ブローカー許可リスト — テスト

- **ユニット** — `UnitTests/Accounts/`: `BrokerName`/`BrokerAllowlist`値オブジェクト、`BrokerProbeOutput`パーサー、`CTraderIdAccount`許可リスト不変量。
- **統合** — `IntegrationTests/BrokerAllowlistTests.cs`: 仮説Verifierを使用した手動cIDエンドポイント（無制限 / 検証済み / 不許可 / 検証失敗）+ Open API linkerが許可されていないアカウントをスキップ。`BrokerVerifierLiveTests.cs`はcID認証情報 + algoが提供されたとき**実際の**probeを実行します（否则は正常にスキップ）。
- **E2E** — `E2ETests/BrokerAllowlistTests.cs`: 制限されたデプロイメントが手動追加を拒否し「検証できませんでした」通知を表示（アカウント行が追加されない）。

## ノードUI可視性

ノードはほとんどのテナントが手で管理しないインフラです — cTrader CLIエージェントは[自己登録+ハートビート](../operations/node-discovery.md)するため、white-labelデプロイメントは手動コントロール、またはNodes表面全体を非表示にして自動Discovery経由で健全なクラスターを依然として実行できます。これらを管理する2つの設定専用ブランディングキー：

```json
{
  "App": {
    "Branding": {
      "NodesUi": "Monitor",
      "RestrictNodesToOwner": true
    }
  }
}
```

環境変数形式：`App__Branding__NodesUi=Hidden`、`App__Branding__RestrictNodesToOwner=true`。

**`NodesUi` — 3つのモード：**

- **`Full`（デフォルト）** — 在庫商品：ノードリスト plus手動**新規ノード**と**削除**コントロール。`POST`/`DELETE /api/nodes`が動作。
- **`Monitor`** — 読み取り専用表面：リストとライブ統計は維持，但し手動追加と削除が削除されました。ノードは自動Discoveryからのみ表示されます。`POST`/`DELETE /api/nodes`が**404**を返します。
- **`Hidden`** — Nodes navリンクとページが完全に削除され、ページルートがダッシュボードにリダイレクト；手動add/delete APIがオフ。クラスターは自動Discoveryのみ。
- **`RestrictNodesToOwner`**は誰がノードを表示して管理できるかの 下限制限。デフォルト`false`は標準**admin-or-above**スタッフ表面を維持（`AdminOrAbove`）；`true`に設定すると**ownerのみ**（`Owner`）になります。どちらの方法でも**通常のユーザーはノードを決して表示しません** — これはownerのみとより広いスタッフ表面の間でのみ選択します。

Node **自動Discoveryは両方のキーの影響を受けません**：匿名`POST /api/nodes/register`自己登録+ハートビートエンドポイントは常に動作するため、`Hidden`/`Monitor`デプロイメントはそれでもクラスターを自動的に成長させます。

**モデル：**

- `Core.Nodes.NodesUiMode` — `Full` / `Monitor` / `Hidden`。
- `Core.Nodes.NodesUiAccess` — モード + owner制限を構成する単一の情報源： `IsPageVisible`、`AllowsManualManagement`、`RequiredPolicy(restrictToOwner)`。Nav（`NavMenu.razor`）、ページ（`Pages/Nodes.razor`）、エンドポイント（`NodeEndpoints`）のすべてがそれを読み取るため、UIとAPIは決して不同意になりません。
- `Core.Options.BrandingOptions.NodesUi` / `.RestrictNodesToOwner` — `App:Branding`からバインド。

## ノードUI可視性 — テスト

- **ユニット** — `UnitTests/Nodes/NodesUiAccessTests.cs`: すべてのモード + デフォルトブランディング全体のページ可視性、手動管理、必須ポリシー解決。
- **統合** — `IntegrationTests/NodeUiGatingTests.cs`: 実際のHTTP + Postgres — `Full`は手動追加を許可し、`Monitor`/`Hidden`はaddとdeleteを404し、`RestrictNodesToOwner`は管理者がアクセス禁止ながら所有者が読み取りリストを読み取ることを許可。
- **E2E** — `E2ETests/NodesUiTests.cs`（デフォルト`Full`：navリンク + ページ + 新規ノードボタンが描画）と`E2ETests/NodesHiddenTests.cs`（`Hidden`：navリンクが gone、`/nodes`がリダイレクト）。

## デザイントークン（CSS変数）

ブランディングはMudBlazorのみ而非、アプリの**本身**のスタイルシート + カスタムコンポーネントにも到達します。`Web.Branding.BrandingCss.BuildRootVariables(BrandingOptions)`はブランドパレットを`:root`のCSS customプロパティとしてemits（`--app-primary`、`--app-primary-hover`、`--app-surface`、`--app-appbar`、`--app-success`/`--app-error`/`--app-warning`/`--app-info`、…）、`App.razor`の`site.css`の直後に注入。`site.css`と各コンポーネントは`var(--app-*)`を読み取る — **ハードコードされた色なし** — 因此 再販業者パレットは（ログイン hero、ボトムnav、ヘルプチップ、オフラインページ）を含むすべての場所で無料でフローします。ニュートラル表面トーンは`site.css :root`でデフォルト； `CustomCss`（最後に注入）は任意のトークンをoverrideできます。[ui-guidelines.md](../ui-guidelines.md) §2を参照。

## ブランドPWA

インストール済みアプリもブランド化されています — manifestエンドポイント（`/manifest.webmanifest`）は`BrandingOptions`から構築されます（`ProductName` → `name`/`short_name`、`Description`、`AppBarColor`/`BackgroundColor` → theme/background）。[pwa.md](pwa.md)を参照。

## テスト

- **ユニット** — `UnitTests/Branding/HexColorTests.cs`: 有効/無効のhex検証。
- **統合** — `IntegrationTests/ThemeBuildTests.cs`: 色がパレットにマッピングされる、無効な色がスロー； `IntegrationTests/BrandingHttpTests.cs`: カスタム`ProductName`/description/theme-colourが提供されたpage `<head>`でレンダリング（WebApplicationFactory + Postgres）、デフォルトがstock nameを維持。
- **E2E** — `E2ETests/BrandingTests.cs`: ブランド化された商品名がapp barで実際のブラウザでレンダリング。
