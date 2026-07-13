---
description: "リテール FX/CFD/暗号ブローカレッジ 法務 + レコード保管義務実行。4つの業界標準柱実装: リスク開示同意…"
---

# 法務 & コンプライアンス

リテール FX/CFD/暗号ブローカレッジ 法務 + レコード保管義務実行。4つの業界標準柱実装: **リスク開示同意**、**改ざん防止監査証跡**、**MiFID/ESMA スタイル レコード保管**、**GDPR データ権**。すべて `Compliance` 機能フラグでゲート。

## 1. バージョン管理 法令文書 + 同意

- `LegalDocument` (集約) — バージョン管理 利用規約、CFD **リスク開示**、またはプライバシーポリシー。バージョン起案、次に **公開**; 公開バージョン **不変** (編集スロー)、正確なテキスト ユーザー同意 常に回復可能。型のアクティブドキュメント = 最高公開バージョン。
- `ConsentRecord` (集約) — 不変レコード ユーザー 受け入れ 特定ドキュメント バージョン タイム、発信元 IP。
- **強制:** `RouteGroupBuilder/RouteHandlerBuilder.RequireConsent(type)` ブロック アクション `403` に その型の公開ドキュメント 存在し ユーザー アクティブ バージョン 同意しない時。**コピープロファイル作成** に適用 (`RiskDisclosure`)。なし公開 → アクション許可 — なし同意 まだ — モジュール有効化 ブロック 遡及的に 開示 実際公開まで。

## 2. 改ざん防止 監査証跡

`AuditLog` エントリ ハッシュチェーン: 各行 保存 `PrevHash` と `Hash = SHA-256(prev | 正規フィールド)`。`AuditChainInterceptor` チェーン適用 透過的に `SaveChanges`、既存監査呼び出し側 変更なし。`IAuditTrailVerifier.VerifyAsync` 再ウォーク チェーン、レポート 最初行 保存ハッシュ または バックリンク 一致しない — 検出 任意編集 または 削除 過去レコード。オーナー エンドポイント: `GET /api/compliance/audit/verify`。

## 3. レコード保管 (MiFID II / ESMA RTS)

レコード保管 充足 **不変、ハッシュチェーン監査ログ** + **保持同意レコード** + ソフト削除 (決してハード削除しない) ドメイン レコード。UTC タイムスタンプ 注入 `TimeProvider`。同意レコード 保管 ドキュメント バージョン + IP; 公開法令文書 決してミューテート。保持 = これらのテーブル 削除なし (追記のみ / ソフト削除)。

## 4. GDPR データ権

- `GET /api/compliance/export` — マシン可読 エクスポート 呼び出し元データ (プロファイル、同意、コピープロファイル、プロップファーム チャレンジ)。
- `POST /api/compliance/erase` — 削除権: `AppUser.Anonymize()` スクラブ PII (メール、MFA) 行ソフト削除、保持 参照/監査 履歴 一貫性。

## API サマリ

| メソッド | ルート | ロール | 目的 |
|--------|-------|------|---------|
| GET | `/api/compliance/documents/active` | User+ | アクティブ公開ドキュメント |
| GET | `/api/compliance/consent/status` | User+ | 未解決 同意 |
| POST | `/api/compliance/consent` | User+ | ドキュメントのアクティブ バージョン 受け入れ |
| GET | `/api/compliance/export` | User+ | GDPR データ エクスポート |
| POST | `/api/compliance/erase` | User+ | GDPR 削除 自分のアカウント |
| POST | `/api/compliance/documents` | Owner | ドキュメント起案 |
| POST | `/api/compliance/documents/{id}/publish` | Owner | バージョン公開 |
| GET | `/api/compliance/audit/verify` | Owner | 監査 ハッシュ チェーン 検証 |

UI: `/settings/legal` (ナビ *設定 → 法務 & プライバシー*、`Compliance` でゲート) 未解決 契約 受け入れボタン表示 + GDPR エクスポート/削除 アクション。

## テスト

- **ユニット** — `UnitTests/Compliance/LegalDocumentTests.cs` (起案/公開/不変性、同意 キャプチャ)、`AuditChainTests.cs` (ハッシュリンク、改ざん検出、コンテンツ 感度)。
- **統合** — `IntegrationTests/CompliancePersistenceTests.cs` (アクティブバージョン + 同意クエリ 実 Postgres)、`AuditChainIntegrityTests.cs` (チェーン検証 無傷、次に SQL レベル改ざん検出)、`ComplianceFlowTests.cs` (WebApplicationFactory、分離 DB: 同意ゲート ブロック コピー作成 まで リスク開示 受け入れ; GDPR エクスポート; 監査 検証)。
- **E2E** — `E2ETests/ComplianceTests.cs`: 法務 & プライバシー ページ レンダリング GDPR エクスポート 戻す ユーザーデータ 実ブラウザ。
