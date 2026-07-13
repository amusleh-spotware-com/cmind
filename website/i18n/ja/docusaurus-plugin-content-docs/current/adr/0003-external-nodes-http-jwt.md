---
title: 0003 — cTrader CLI ノード HTTP + JWT、SSH/シェル なし
description: なぜ リモート ノード エージェント 公開 HTTP API のみ HS256 JWT + 決してシェル。
---

# 0003 — cTrader CLI ノード HTTP + JWT、SSH/シェル なし

## コンテキスト

バックテスト/実行 コンテナ実行 リモートホスト。明白 アプローチ — SSH スタンバイ docker 実行 — メイン アプリ 任意遠隔コード実行 + 長生存認証情報 すべてノード。大きい 影響 半径 すべてのシステム 不信トレード cBot 実行。

## 決定

各リモート ホスト 実行 スタンドアロン `CtraderCliNode` **HTTP エージェント** **SSH なし + シェル なし**。メイン アプリ 呼び出し エージェント HTTP 越し; すべてリクエスト キャリア 短生存 **HS256 JWT** (5 分、`iss=app-main` / `aud=app-node`) 署名 そのノード秘密。エージェント:

- 唯一 実行 イメージ マッチング `AllowedImagePrefix` (パス境界で `ghcr.io/spotware` マッチ不可 `ghcr.io/spotware-evil/...");
- exec docker 経由 `ArgumentList` — 決してシェル 文字列;
- **ステートレス**、コンテナ 見つけ `app.instance` ラベル;
- 自己登録 + ハートビート `POST /api/nodes/register`; メイン アプリ upsert `CtraderCliNode` **名前で**、ノード IP 変更 生き残る。

## 結果

- リークリクエスト トークン 有効期限切れ 分; 盗まれる スタンディングシェル 認証情報 なし。
- エージェント 能力 バウンド "許可イメージ実行" — ターン不可 汎用リモートシェル。
- ノード ID 名前ベース、 reprivisioning ノード 新規IP orphan 不可 履歴。
