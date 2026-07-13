---
title: ローカルで実行
description: Docker Compose（または開発向け .NET Aspire）で数分以内で cMind をあなたのマシンで実行。
sidebar_position: 1
---

# cMind をローカルで実行 🖥️

これが cMind を実際に見る最速の方法—あなたのマシンの完全なインスタンス。コーヒーを手に; サインイン前に冷めるでしょう。

:::tip 終了時に持つもの
**localhost:8080** で実行 Web アプリ、**localhost:8081** で MCP サーバ、Postgres データベース、cBots を構築およびバックテストする準備ができたローカルワーカーノード。すべてあなたのマシンに、すべてあなたのもの。
:::

**開始前に、これらの 1 つが必要:**

- **Docker のみ** → オプション A 使用 (.NET SDK 不要)。初回確認向け推奨。
- **.NET 10 SDK + Docker** → コード上でハック希望なら オプション B 使用。

両パス クロスプラットフォーム (Windows / macOS / Linux)。

## オプション A — Docker Compose (.NET SDK 不要)

前提条件: Docker Desktop (または Docker Engine + compose プラグイン)。

```bash
cp .env.example .env        # PG_PASSWORD、OWNER_EMAIL、OWNER_PASSWORD 編集
docker compose up --build
```

- Web UI: <http://localhost:8080> (.env からオーナーでサインイン; 初回ログインで強制パスワード変更)。
- MCP サーバ: <http://localhost:8081/mcp>。
- Postgres データ `pgdata` ボリュームで保持; スキーマ 起動時 自動マイグレーション。

Web コンテナ ホスト Docker ソケットマウント (`/var/run/docker.sock`) ブラウザ内ビルダー と シード **LocalNode** cTrader コンソールコンテナ 構築 + ビルド実行 あなたのマシン上。

**クロスプラットフォーム 注記**
- Docker Desktop (Windows/macOS) ソケット `/var/run/docker.sock` で公開 — compose マウント として機能。
- Linux: あなたのユーザー ソケット アクセス できることを確認、または compose 充分な権限 で実行。
- Web イメージ `linux/amd64`; Apple Silicon Docker エミュレーション下実行。

停止 & 削除:

```bash
docker compose down          # データ保持
docker compose down -v       # また データベースボリューム削除
```

## オプション B — .NET Aspire (開発向け)

前提条件: .NET 10 SDK + Docker。

```bash
dotnet run --project src/AppHost
```

Aspire Postgres、Web、MCP、pgAdmin ベル; 接続文字列 + OTLP 配線; ダッシュボード開く。オーナー認証情報 Aspire パラメータ設定 (`OwnerEmail`, `OwnerPassword`)。

既存 Postgres に対してのみ Web アプリ実行:

```bash
dotnet run --project src/Web
```

## ローカルでワーカーノード追加

シード LocalNode 既にあなたのマシン上で作業実行。**自動検出** 演習するため、ノードエージェント起動 Web アプリを指す ([ノード検出](../operations/node-discovery.md) 参照) `NodeAgent:MainUrl=http://host.docker.internal:8080` と マッチング `JoinToken`。

## トラブルシューティング 🔧

Docker は意見をもっています。通常の容疑者は以下です:

| 症状 | 原因の可能性 & 修正 |
|---|---|
| 8080/8081 で `ポートは既に割り当て` | 別のもの ポート使用。停止、または `docker-compose.yml` でマッピング変更。 |
| Web 開始 しかし ビルド/バックテスト失敗 | Docker ソケット マウント または アクセス不可。Linux で、ユーザー `/var/run/docker.sock` 到達 できることを確認。 |
| `ソケットでアクセス拒否` (Linux) | ユーザーを docker グループ追加 (`sudo usermod -aG docker $USER`) と 再ログイン、または 充分な権限 で実行。 |
| 非常に遅い初回実行 | 初回ビルド イメージプル と コンパイル — その後の実行 より高速。Apple Silicon の `linux/amd64` Web イメージ エミュレーション下実行。 |
| サインイン できない | `.env` で `OWNER_EMAIL` / `OWNER_PASSWORD` 確認。初回ログイン 強制パスワード変更。 |
| アップグレード後 データベース 不具合 | `docker compose down -v` ボリューム ワイプ クリーンスレート (ローカルデータ 失う)。 |

それでも詰まった? [Discussion を開く](https://github.com/amusleh-spotware-com/cmind/discussions) — 親切です。次の停止: [本番 デプロイ →](./cloud.md)
