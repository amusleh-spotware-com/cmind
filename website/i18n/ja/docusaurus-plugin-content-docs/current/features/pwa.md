---
description: "cMindは電話またはデスクトップにネイティブアプリのようにインストールできます — ホーム画面アイコン、スタンドアローンウィンドウ、スプラッシュ、フレンドリーなオフラインページ。モバイルファーストで完全にレスポンシブ； ui-guidelines.mdを参照。"
---

# インストール可能なアプリ（PWA）

cMindは電話またはデスクトップにネイティブアプリのようにインストールできます — ホーム画面アイコン、スタンドアローンウィンドウ、スプラッシュ、フレンドリーなオフラインページ。**モバイルファースト**で完全にレスポンシブ； [ui-guidelines.md](../ui-guidelines.md)を参照。

## ここで「インストール可能」が意味する内容 — と正直な制限

Blazor **Server**はライブSignalRサーキット経由でレンダリングするため、 앱は完全にオフラインで実行できません。PWAが提供するもの：

- **インストール可能** — 有効なweb manifest + アイコン поэтому ブラウザが*Install* / *ホーム画面に追加*を提供。
- **アプリシェルキャッシュ** — サービスワーカーが静的アセット（CSS、アイコン、manifest）をキャッシュし、ネットワークがドロップしたときに**オフラインpage**を表示、而非ブラウザエラー。
- **ネイティブ感** — スタンドアローンディスプレイ、品牌theme-color/ステータスバー、 app icon、iOSホーム画面アイコン。

オフラインインタラクティブを提供**しません** — それはBlazor WebAssembly（別個の futureトラック）を必要とします。ライブ機能のオフライン使用を約束しないでください。

## ピース

| ピース | 場所 |
|-------|-------|
| Manifest（動的、品牌） | `Web/Endpoints/PwaEndpoints.cs` → `GET /manifest.webmanifest`（匿名） |
| アイコン（192、512、512-maskable、apple-touch-180） | `Web/wwwroot/icons/` |
| Service worker（app-shell） | `Web/wwwroot/service-worker.js` |
| オフラインfallbackページ | `Web/wwwroot/offline.html` |
| 登録 + iOSタグ + install-promptキャプチャ | `Web/Components/App.razor` |
| ルート定数 | `Core.Constants.PwaRoutes` |

### Manifest

`BrandingOptions`から動的に提供服务 因此 再販者の商品名、色、アイコンがインストール済みアプリに継承：`name`/`short_name`は`ProductName`から、`description`、`theme_color`は`AppBarColor`から、`background_color`は`BackgroundColor`から、`display: standalone`、アイコンセット（を含む **maskable** 512 forクリーンなAndroidアイコン）。匿名 — インストールプロンプトはサインイン前に動作する必要があります。

### Service worker

App-shellのみ。Blazorサーキット（`/_blazor`）、フレームワーク（`/_framework`）、SignalRハブ（`/hubs`）を**決して**傍受しません — これらは常にネットワーク。ナビゲーションはネットワークファーストでオフラインpage as fallback；静的アセット（`/css`、`/icons`、`/_content`）はバックグラウンドrevalidateでキャッシュファースト。`updateViaCache: 'none'`で登録因此 ワーカー更新が確実に適用。キャッシュはバージョン管理されています（`cmind-shell-v<n>`）— シェル変更時にバンプ。

### iOS

iOSはmanifestアイコン/splashを無視するため、`App.razor`は代わりに`apple-touch-icon`と`apple-mobile-web-app-*` metaタグも emitします。iOSには`beforeinstallprompt`がありません； ユーザーはSafariの*ホーム画面に追加*からインストールします。`beforeinstallprompt`はChromium/Androidで`window.deferredInstallPrompt`にキャプチャされ、カスタムインストール機能を提供します。

## テスト

- **E2E** — `E2ETests/PwaTests.cs`: manifestが`application/manifest+json`で提供、非空アイコン（maskableも含む）、`display: standalone`、`apple-touch-icon`リンク付き、サービスワーカーが登録 + アクティブ化。`MobileLayoutTests` / `MobileDialogTests`はPWAがインストールするモバイルシェルをカバー。
