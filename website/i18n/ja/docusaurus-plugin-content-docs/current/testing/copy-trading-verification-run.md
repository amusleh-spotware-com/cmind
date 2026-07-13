---
description: "残りのコピートレーディング作業の完全な検証 — 以下はすべて実際に実行され、作成のみ而非。"
---

# コピートレーディング検証ラン（2026-07-10）

残りのコピートレーディング作業の完全な検証 — 以下はすべて**実際に実行**、作成のみ而非。

## ライブ（実際のcTraderデモアカウント） — 8/8合格
1:1 · 1:many · reverse · cross-cID · partial-close · **pending limit + cancel** · **trailing stop** · token-refresh。
ライブシナリオ追加 `RunPendingAsync` / `RunTrailingAsync`（+ `LoadSpotPriceAsync`、`OpenPositionSnapshot.StopLoss/TrailingStopLoss`）。

## 統合（real Postgres、Testcontainers） — 合格
- `CopyNodeAffinityTests` — スーパーバイザーの実際のアトミッククレーム：最初のノードがすべての実行中プロファイルをクレーム、2番目が**0**をクレーム（重複コピーなし）；一時停止が解放 + 回収。
- `TokenRotationSignatureTests` — 実際のトークンローテーションでのみシグネチャが変更。

## クラスター内（kind + Helm） — 合格
実際のkindクラスターに対して`scripts/k8s-e2e.sh`を実行：
- **決定論的 Job: 101合格**クラスター内。
- **ライブ Job: 8合格**クラスター内（init-container `seed-secrets`がSecret → 書き込み可能なemptyDirにコピー、本当のデモアカウント）。
- Job `Complete 1/1`、スクリプト exit 0。

## 検証中に発見されたバグ（修正 + 再検証済み）
- **保留中イベント**: cTraderは*cTraderに接続する*建値注文/stop `ORDER_ACCEPTED`/`CANCELLED`に**非オープンポジションプレースホルダ**を添付します。`SourceExecutionsAsync`は posicion branchの前にplacement/cancelを注文イベントとして分類するようになりましたが、limit/stop *fill*（例：stop-lossトリガー close）はcloseパスを通り抜けます。
- **単一使用リフレッシュトークン**: cTraderは每一次のリフレッシュでリフレッシュトークンをローテートします。永続化できない読み取り専用キャッシュは自己無効化します。ライブK8s JobthereforeSecretsを書き込み可能なemptyDirにコピーします（必須 — 単一使用リフレッシュトークンを永続化可能）； Jobはデフォルトで決定論的スイートを使用。`SaveTokens`は今_best-effort。ライブシンボルはFXに強制されました（BTCUSD trailingはbroker-rejected）。
- スクリプトイメージ名前付けがHelm `registry/repository`スプリット + `pullPolicy=Never`と一致するように修正されました。

## 高度なミラーリング + トークンライフサイクル + スケーリングプログラム（2026-07-10） — 決定論的層が合格

フォローアッププログラムは注文タイプフィルタリング、保留中注文有効期限コピー、市場範囲/stop-limitスリッページミラーリング、SL/TPコピートグル、優雅なインプレーストークンスワップ（cIDごとに1つの有効なトークン）、cTrader-faithfulシミュレーター、自己修復ノードリーース、統一された開発認証情報ファイルを追加します。

- **ユニット — 210合格**（`dotnet test tests/UnitTests`）。新しいコピーcoverage：注文タイプフィルタ（オープン + 保留中）、市場範囲スリッagemirror + ベース価格、有効期限コピーのオン/オフ、stop-limitスリッページ、保留中修正、マスターオープンで開始、切断→マスタートレード→再接続再同期（欠落オープン + 閉じる孤立）、インプレーストークンスワップ（再起動なし）、cross-cID無効化、ドメイン不変量、lease所有権、トークンバージョンバンプ。
- **統合（real Postgres、Testcontainers） — 合格**: `CopyNodeAffinityTests`（アトミッククレーム、重複コピーなし、一時停止解放、**期限切れleaseの別のノードによる回収**）、`TokenRotationSignatureTests`（トークンバージョンバンプでシグネチャが変更）、`OpenApiAuthorizationPersistenceTests`（TokenVersionが永続化 + リフレッシュでインクリメント）。
- **E2E**（`tests/E2ETests`）：宛先オプラウンドトリップは注文タイプフィルタ、有効期限、コピースリッページを現在主張alongsideフルライフサイクル。
- **ビルド**: `TreatWarningsAsErrors`でクリーン； 変更されたファイルのRider `get_file_problems`クリーン。

保留stop、市場範囲、有効期限、マスターオープンでの開始、ミッドランタイロテーションのライブシナリオ（本当のcTraderデモアカウント）は統一された`secrets/dev-credentials.local.json`に対して作成されました； [dev-credentials.md](dev-credentials.md)参照。

## 既知のフォローアップ
クラスター内ライブランは単一使用トークンをローテートしました； `CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`でローカルキャッシュを再生成
（cTraderがそのOAuthページをラン直後にスロットル — クリア的时候就再試行）。
