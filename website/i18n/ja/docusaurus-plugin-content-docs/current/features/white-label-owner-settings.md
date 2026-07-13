---
id: white-label-owner-settings
title: Owner settingsのWhite-labelオプション
sidebar_label: White-label owner settings
---

# Owner settingsのWhite-labelオプション

デプロイメントが設定（appsettings/env）を介して設定できるすべてのwhite-labelオプションは、
**런타임에서도 앱 所有者が設定可能**で、**Settings → Deployment**から、再デプロイなしに。
所有者のオーバーライドは設定**より優先**; クリアするとオプションがデプロイメントに設定された
（または組み込みデフォルト）値に戻ります。

これはホワイトラベル*デプロイ*が製品を構成する方法と同じ —
 同じノブ、同じ効果 — 因此オペレーターはブランディング、ゲート、ンポリシーをライブで調整し、
 即座に結果を見ることができます。

## 配置場所

- **UI：** 設定ダイアログの所有者専用**Deployment**セクション +
  ディープリンク可能なページ**`/settings/deployment`**。
  オプションは**カテゴリごとのタブ**にグループ化
  （Branding、Theme、Features、Registration、Accounts、Email、AI、Open API、Prop firm）、
  モバイルファースト、desktopではウィンドウ化されたダイアログ、phoneでは全画面表面。
- **API：** `/api/whitelabel`（所有者のみ、フィーチャーゲートなし）：
  - `GET /api/whitelabel` — すべてのオプション（有効な値、provenance（`Config` / `Owner` / `Default`）、
    オーバーライドが設定されているかどうかを含む）。**秘密はマスク**（値は決して返されない）。
  - `PUT /api/whitelabel/{key}` `{ "value": "…" }` — オーバーライドを設定
    （オプションの種類ごとに検証）。**secret**上の空白の値は既存のsecretを維持。
  - `DELETE /api/whitelabel/{key}` — 1つのオーバーライドをクリア（configに戻す）。
  - `POST /api/whitelabel/reset` — **すべての**オーバーライドをクリア（純粋なconfigにデプロイを戻す）。

## オーバーライドが有効になる仕組み

所有者のオーバーライドは暗号化された where-needed `AppSetting`行として保存され、
装飾された`IOptionsMonitor<AppOptions>`の上にレイヤー化されます。
すべてのコンシューマがすでにそのモニターを通じてオプションを読み取っているため、
オーバーライドはアプリ全体で**ライブ**で適用されます —
 theme、page title、MFA gate、AIプロバイダゲート、ブローカー許可リスト、
 登録ポリシー、email transport設定などが次回の読み取りで更新
 （theme/brandingは即座に再レンリング）。
 データベースが短時間利用できない場合、レイヤーは**fail open**して設定されたベースラインにいくため、
 オーバーライドの読み取りがアプリを壊す永远不会。

**フィーチャーフラグ**は同じサーフェスの一部ですが、既存のフィーチャーオーバーライドストア
（`IFeatureGate`）を通じて永続化されるため、Featuresタブとスタンドアロンのフィーチャー トグルが決してdivergentない。

**Secrets**（SMTPパスワード、CAPTCHAシークレット、プロビジョニングシークレット）は保存時に暗号化
（`ISecretProtector`、purpose `whitelabel.secret`）、UIでは書き込み専用、APIでは決して返されない。

## 委任されたオプション

**共有Open APIアプリケーション**の認証情報と**per-message-typeレート制限**は
**Open API**設定セクションで管理されます
（copy-trading / Open API docsを参照）。
 これらはDeploymentカタログに*委任*エントリとして表示されます
 （read-only here、リンク付き）、因此何も複製されず、同期保証がそれでも нихをcounted as covered。

## 常に同期（強制）

設定に新しいwhite-labelオプションを追加することは、**同じコミットでowner settingsに表示する必要があります**。
これは`WhiteLabelCatalogParityTests`によって強制されます：
これはすべてのwhite-label options-recordプロパティを反映し、プロパティが
`Core/WhiteLabel/WhiteLabelCatalog`に登録されていない場合
（または理由付きで`IntentionallyExcluded`に明示的に列表されている場合）
はビルドを失敗させます。
`CLAUDE.md`のマンデート10を参照。

## メモ

- **メールが設定されていないデプロイメントでSMTPを有効にするには再起動が必要**
  （送信者タイプは起動時に選択される）; 既に設定されている送信者のホスト/認証情報はライブで更新。
- オプション**ラベル/説明**はデータ shownとして technical config-knob識別子です;
  タブラベルとすべてのインタラクティブクロムは完全にローカライズされています。
