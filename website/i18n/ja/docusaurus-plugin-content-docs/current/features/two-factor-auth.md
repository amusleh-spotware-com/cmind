---
description: "オプション TOTP 二要素認証 オーセンティケータアプリ登録、単一使用バックアップコード、ホワイトラベルスイッチ すべてのユーザー 必須作成。"
---

# 二要素認証 (2FA)

アカウント 保護可能 パスワード トップ **タイムベース ワンタイムパスワード (TOTP)** 二要素認証。**オプトイン** ユーザープロファイルから デフォルト、+ ホワイトラベル デプロイ **必須** 作成可能 すべて。任意 RFC 6238 オーセンティケータアプリ 作動 — Google Authenticator、Microsoft Authenticator、Authy、Aegis、FreeOTP — 実装 標準 のため (SHA-1、6 桁、30 秒ステップ); 所有権なし サーバコンポーネント。

## 動作方法

- **ドメイン。** MFA ライブ `AppUser` 集約 (アクセスコンテキスト)。ユーザー登録 経由意図明確方法 — `BeginMfaEnrollment`, `ConfirmMfaEnrollment`, `ConsumeBackupCode`, `RegenerateBackupCodes`, `DisableMfa` — 不変式 (シークレット 確認 前 活性化; バックアップコード 単一使用) 実行 1 場所。
- **TOTP。** 生成 + 検証 座る コア `ITotpAuthenticator` インタフェース、実装 **Otp.NET** ライブラリ。検証 寛容 ±1 タイムステップ クロック スキュー。
- **シークレット rest。** オーセンティケータシークレット 保存 **暗号化** `ISecretProtector` (`EncryptionPurposes.MfaSecret`) — プレーンテキスト決して。
- **バックアップコード。** 10 個 単一使用 回復コード 発行 登録、示す **1 回のみ**、保存 SHA-256 ハッシュ のみ (`MfaBackupCodes`)。各 作動 正確に 1 回; 使用済みコード 却下 その後。

## 有効化 (プロファイル)

**アカウント** ページで (`/account`) *二要素認証* セクション 示す 現在ステータス:

1. **有効化 二要素** 開く MudBlazor ダイアログ QR コード (レンダー サーバ側 SVG 経由 `Net.Codecrete.QrCodeGenerator`) + マニュアル セットアップ キー。
2. スキャン、入力 6 桁コード 確認 — こう 検証 保留中シークレット 前 活性化。
3. ダイアログ 次に 示す **バックアップコード**; 保存。2FA 今 オン。

同セクション しましょう 登録ユーザー **再生成 バックアップコード** または **ターンオフ** 2FA — 両者 必須 アカウント パスワード 確認。

## サインイン 2FA

ログイン **2 ステップ** フロー 一度 2FA 有効:

1. **パスワード ステップ** (`POST /api/auth/login`)。成功時 認証クッキー **付与される前に**; 代わり 短生存 (5 分)、暗号化 *保留中* クッキー 設定 + ユーザー 送信 `/login/2fa`。
2. **チャレンジ ステップ** (`POST /api/auth/login/verify-2fa`)。ユーザー 入力 TOTP コード **または** 任意 未使用バックアップコード。成功時 保留中クッキー ドロップ + 実 認証クッキー 発行。

失敗 2 番目要素 試み カウント 既存アカウント **ロックアウト** (`AuthLockout`)、+ 認証エンドポイント レート制限。

## 必須 2FA ホワイトラベル デプロイ

規制リセラー 要求可能 2FA **すべて** アカウント:

```jsonc
// appsettings / 環境
"App": { "Branding": { "RequireMfa": true } }   // App__Branding__RequireMfa=true
```

`RequireMfa` オン + ユーザー 2FA なし サインイン、パスワード ステップ レポート `mfaSetupRequired` + `MfaEnforcementMiddleware` リダイレクト ページナビ `/account` までに彼ら完了 登録。デフォルト `false`、未設定 デプロイ キープ 2FA オプション。[ホワイトラベル](white-label.md) 参照。

## エンドポイント

| メソッド & ルート | 目的 |
| --- | --- |
| `POST /api/auth/login` | パスワードステップ; 戻り `mfaRequired` (チャレンジ) または サインイン |
| `POST /api/auth/login/verify-2fa` | 2 番目要素ステップ (TOTP または バックアップコード) |
| `GET /api/auth/mfa/status` | `MfaEnabled`、保留中、残り バックアップコード数 |
| `POST /api/auth/mfa/setup` | 登録開始 — 戻し シークレット、`otpauth://` URI、QR SVG |
| `POST /api/auth/mfa/confirm` | コード確認、活性化、戻しバックアップコード |
| `POST /api/auth/mfa/disable` | ターンオフ (パスワード確認) |
| `POST /api/auth/mfa/backup-codes/regenerate` | 発行 新鮮セット (パスワード確認) |

## テスト

- **ユニット** — `UnitTests/Access/OtpNetTotpAuthenticatorTests.cs` (RFC 6238 ベクトル)、`AppUserMfaTests.cs` (登録/遷移/単一使用不変式)、`MfaBackupCodesTests.cs`。
- **統合** — `IntegrationTests/MfaPersistenceTests.cs` (登録 → 確認 → 消費、カスケード削除) + `MfaFlowTests.cs` (完全HTTP 2 ステップ ログイン TOTP + バックアップコード、+ 必須登録ゲート)。
- **E2E** — `E2ETests/MfaFlowTests.cs`: プロファイルから有効化 (QR + 確認 + バックアップコード) + サインイン チャレンジ完成、デスクトップ + モバイルビューポート。
