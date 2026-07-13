---
description: "コピープロバイダーのブラウザ可能なディレクトリ。プロバイダーがverified-liveバッジ（戦略ソースアカウントが本番 돈을 거래.real money，而非demo）+パフォーマンス手数料でコピープロファイルを発行。フォロワーが実行透明性データから計算されたパフォーマンススコアでランキング。"
---

# Copy provider marketplace (フェーズ4)

コピープロバイダーのブラウザ可能なディレクトリ。プロバイダーが**verified-live**バッジ（戦略ソースアカウントがリアルマネーを取引，而非demo）+パフォーマンス手数料でコピープロファイルを発行。フォロワーが実行透明性データからプロジェクトされたパフォーマンススコアでマーケットプレイスを閲覧、ランキング。

## モデル

- `CopyProviderListing` = アグリゲート: `UserId`、`ProfileId`、表示名、説明、パフォーマンス手数料、`VerifiedLive`、`Published` + `PublishedAt`。プロファイルごとに1つのリスト（一意インデックス）。
- **Verified-live**はプロファイルソース`TradingAccount.IsLive`から発行時に派生 — プロバイダーは自己主張できません。
- パフォーマンス統計はリストに**保存されない** — `CopyExecution`透明性ログに対する読み取りモデル射影（fill rate、平均レイテンシ、平均実現スリッページ）、そのためマーケットプレイスは常にライブ実行品質を反映。

## ランキング

`CopyEndpoints.MarketplaceScore(fillRate, avgLatencyMs, avgSlippagePoints, verifiedLive)` → 0–100スコア：fill rateが支配（×60）、低レイテンシ+低スリッページが追加（×20 each）、verified-liveバッジが小さなtrust bonusを追加。决定的+単調 поэтому順序が安定。

## API

- `POST /api/copy/profiles/{id}/publish` — プロファイルリストを発行/更新（`DisplayName`、`Description`、`PerformanceFeePercent`）；verified-liveはソースアカウントから設定。
- `DELETE /api/copy/profiles/{id}/publish` — 発行解除。
- `GET /api/copy/marketplace` — すべて発行済みリスト、ランキング済み、それぞれにパフォーマンスサマリー（実行、fill rate、平均レイテンシ、平均スリッページ、スコア）+ verified-liveバッジ付き。

## テスト

- **ユニット**（`CopyProviderListingTests`） — アグリゲート不変量：表示名が必要；発行がタイムスタンプを設定；発行解除が非表示；更新が表示フィールド+手数料+バッジを交換。
- **統合**（`CopyMarketplaceTests`、real Postgres） — 発行済みリストがバッジ付きで永続化；プロファイルごとに1つのリスト（一意インデックス）；ランキングスコアがverified/高fill rateプロバイダーを優先。

コピーホスト不改変（リスト+読み取りモデルのみ）、そのためコピーDSTストレススイートは影響を受けません。
