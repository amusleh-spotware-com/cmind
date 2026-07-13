---
title: クラウドにデプロイ
description: cMind を Azure、AWS、または Kubernetes にデプロイ。どのプラットフォーム適切、前提条件、ステップバイステップガイド。
sidebar_position: 2
---

# クラウドにデプロイ ☁️

ラップトップから卒業? 実 インフラに cMind 配置 時間。良いニュース: ほぼ ゼロ オペレータ セレモニー スケールアウト 設計 — ZooKeeper なし、リーダー選出 なし、レプリカ + データベース。

**前もって知ること 1 つ:** ステートレス ティア (Web + MCP) 実行 快適に *任意* コンテナプラットフォーム、しかし **ノードエージェント 特権Docker 必要** (ビルド + cTrader コンテナ実行)。サーバレス ランタイム (Azure Container Apps、AWS Fargate) エージェント向けに 排除 — 実行 [Kubernetes](./kubernetes.md)、VM、または EC2 Web URL を指す。

パス選択:

- 🟦 **[Azure](./cloud-azure.md)** — Container Apps + Postgres Flexible Server (Bicep)。
- 🟧 **[AWS](./cloud-aws.md)** — ECS Fargate + ALB + RDS (Terraform)。
- ⎈ **[Kubernetes](./kubernetes.md)** — Helm チャート、AKS / EKS / 任意所で作動。
- 📈 **[スケーリング](./scaling.md)** — すべて スケール 自己回復 どのようにに、アップ後。

ステートレス ティア (Web + MCP) 実行 任意 コンテナプラットフォーム; Postgres = 管理 データベース。**ノードエージェント 特権 Docker (DinD) 必要** — サーバレス コンテナ ランタイム (Azure Container Apps、AWS Fargate) ブロック。実行 Kubernetes ([kubernetes.md](kubernetes.md)) または VM/EC2、Web URL を指す。

| クラウド | ステートレス ティア | データベース | ガイド |
| ----- | -------------- | -------- | ----- |
| Azure | Container Apps (Bicep) | Postgres Flexible Server | [cloud-azure.md](cloud-azure.md) |
| AWS | ECS Fargate + ALB (Terraform) | RDS Postgres | [cloud-aws.md](cloud-aws.md) |

共通 前提条件、両方:

1. ビルド + プッシュ 3 個 イメージ レジストリ クラウド プル可能 (`cmind-web`, `cmind-mcp`, `cmind-node-agent`)。
2. ピック シークレット: DB パスワード、オーナー メール/パスワード、**発見ジョイントークン** (≥ 32 文字) Web アプリ + すべてのノードエージェント 共有。
3. デプロイ IaC (下)、次に ノードエージェント アップ 別途 (K8s/VM) `NodeAgent__MainUrl` = デプロイ Web URL、`NodeAgent__JwtSecret` = ジョイントークン。

発見、ログ、プローブ 動作 同じ ローカル/K8s セットアップ — [../operations/node-discovery.md](../operations/node-discovery.md) と [../operations/logging.md](../operations/logging.md) 参照。
