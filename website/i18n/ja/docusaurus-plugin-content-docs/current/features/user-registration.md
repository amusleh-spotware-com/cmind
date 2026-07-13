---
description: "セキュアでwhite-label-gatedのセルフサービスユーザー登録 — アプリ内サインアップページとサーバー間プロビジョニングAPI、設定可能なユーザー属性、管理者承認またはメール確認 gating、anti-abuseガード。デフォルトで無効。"
---

# ユーザー登録

デフォルトでは**owner/管理者が手動で**ユーザーを追加します（Usersページ → *新規ユーザー*）。ユーザーを大規模にオンボーディングする必要があるwhite-labelデプロイメント、またはアプリを別のサービスと統合する場合、cMindは**セキュアなセルフサービス登録**パスも出します。それは**デフォルトで無効**です：在庫デプロイメントは不改変で、ページとAPIの両方ともデプロイメントがオプトインするまで404を返します。

2つの入口が1つのドメインフローを共有します：

1. **アプリ内ページ**（`/register`） — `/login`と同じシェル内のブランド化されたモバイルファーストのサインアップページ。
2. **プロビジョニングAPI**（`POST /api/provision`） — 統合するサービスがアカウントを作成するためのサーバー間エンドポイントで、デプロイメントごとのプロビジョニングsecretで認証。

## 何が記録されるか — データ最小化

cMindはトレーディング**ツール**：それはcBotをビルド/実行/バックテストし、各ユーザーの*cTrader Open API認証情報*でトレーディングアカウントをバインドします。それはトレーディングアカウントを開いた하거나 고객 자산을保管하지 않습니다 поэтому KYC/AML ID検証は**brokerの**義務而非このプラットフォームの義務です поэтому 登録フォームはデフォルトで**メールのみ**を記録 — サービスを提供するために必要な最小（GDPR Art. 5(1)(c) データ最小化；適法性の根拠 = 契約）。cMindは意図的に国民ID / 生年月日 / 住所フィールドを**出しません**。

他のすべての属性は`App:Registration:Attributes`経由でデプロイメントごとに**オプトインper属性**で、それぞれ独立して`Off` / `Optional` / `Required`：

| 属性 | メモ |
|---|---|
| `FullName`, `DisplayName`, `Company` | 自由テキスト、長さバウンド。 |
| `Country` | ISO 3166-1 alpha-2、固定コードセットで検証。 |
| `Phone` | E.164形式（`+14155552671`）。 |
| `Locale` | BCP-47形状（`en-US`）、正規化。 |
| `MarketingOptIn` | 別個の、**チェックされていない**チェックボックス — 必須の同意と一緒にバンドルされていません（CAN-SPAM）。 |
| `AgeConfirmation` | チェックボックスのみ；**生年月日はいかなる也不知しません**。 |

属性は`AppUser`アグリゲートが所有する`UserProfile`値オブジェクトに住み、構築時に検証されます。**GDPR消去**（`AppUser.Anonymize()`）はプロファイルと任意の verificationトークンをスクラブ。

**同意。** `RequireTermsAcceptance`がオンのとき、ユーザーは出版された法的文書（Terms、Privacy、Risk Disclosure）を受け入れる必要があります。同意は既存の`ConsentRecord`アグリゲート経由で記録されます — バージョンスタンプ、タイムスタンプ付き、発信元IP付き — MiFID/ESMAグレードの記録保管に使用されるのと同じストア。

## ゲーティングモード

自己登録アカウントは_gate（`App:Registration:Mode`）をクリアするまでサインインできません：

- **`AdminApproval`**（デフォルト） — アカウントはキューに入れられます； owner/管理者が**Users**ページ（*承認待ち*セクション）で承認します。メールインフラが必要ありません。
- **`EmailVerification`** — 単一使用、有効期限付きの確認リンクがメールで送信されます；リンクが開かれたときにアカウントがアクティブになります。メール転送が必要です（`App:Email`）。**メール転送が構成されていない場合、このモードは起動時に自動的に`AdminApproval`に低下します**、因此 登録を有効にすることがサイレントに壊れることはありません。
- **`Open`** — アカウントは即座にアクティブ（信頼済み/devのみ）。

自己登録ユーザーは常に**`User`**（または設定されていれば`Viewer`）として作成されます — ドメインは自己登録経由でOwner/Adminをミングすることを**固く拒否**します。

## セキュリティ＆anti-abuse

- **Anti-enumeration。** 重複メールは新鮮なサインアップと同じ**同じ**中立的な`202 Accepted`をもたらし、何も作成しません — アプリ決して住所が既にアカウントを持つかどうかを開露しません。
- **レート制限。** 公開エンドポイントはIP별로スロットルされます（authリミッターより harder）。
- **パスワードポリシー。** 最小長が強制；パスワードはハッシュ化されます（Argon2 via `IPasswordHasher`）；確認トークンはSHA-256ハッシュとしてのみ保存され、単一使用+有効期限付き。
- **メール衛生。** オプションのメールドメイン許可リストと使い捨てプロバイダーブロックリスト。
- **CAPTCHA（オプション）。** reCAPTCHA / hCaptcha / Turnstileは共通の検証契約を介して。
- **ログインゲート。** 保留中のアカウントは中立的な応答でログインを拒否されます。

## プロビジョニングAPI（統合）

`App:Registration:Api:Enabled`と`Secret`が設定されている場合、別のサービスがユーザーを作成できます：

```
POST /api/provision
X-Provision-Secret: <the configured secret>
{ "email": "user@example.com", "password": "…", "role": 2 }
```

Secretは定数時間で比較されます。プロビジョニングされたアカウントは`Api.ActivateImmediately` / `Api.InviteMustChangePassword`に応じて**アクティブ**（または`MustChangePassword`で招待）として作成されます。

## 有効化

登録には**両方**フィーチャーフラグとマスタースイッチが必要です：

```jsonc
"App": {
  "Features": { "Registration": true },
  "Registration": {
    "Enabled": true,
    "Mode": "AdminApproval",           // or EmailVerification / Open
    "DefaultRole": "User",             // never Owner/Admin
    "RequireTermsAcceptance": true,
    "AllowedEmailDomains": [],          // empty = any
    "BlockDisposableEmail": true,
    "Attributes": { "FullName": "Optional", "Country": "Off" },
    "Api": { "Enabled": false, "Secret": "" }
  }
}
```

`App:Email`セクション（SMTP `Host`、`Port`、`UseStartTls`、`Username`、`Password`、`FromAddress`、`FromName`）は`EmailVerification`モードで使用されるメール転送を設定します；`Host`を未設定のままにするとメールなしで実行します（no-op sender）。フィーチャートグルとホワイトラベルの設定については[feature toggles](./feature-toggles.md)と[white-label](./white-label.md)を参照。登録が有効なとき、ログインページに**アカウント作成**リンクが表示されます。

## テスト済み

ユニット（プロファイル検証、`SelfRegister`ロールガード、アクティブ遷移、単一使用トークン、消去）、統合（デフォルトで無効404、承認フロー、メール確認低下、anti-enumeration、abuseガード、必要な属性、プロビジョニング+ bad secret）、E2E（デフォルトオフログインにサインouplinkがない；`/register`ページがそのブランド化された閉じた状態を描画）。
