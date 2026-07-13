---
description: "deploy/aws = Terraformモジュール: ECS Fargate (Web + MCP) ALBの背後、RDS Postgres、CloudWatch ログ。"
---

# AWS デプロイメント — ステップバイステップ

`deploy/aws` = Terraformモジュール: **ECS Fargate** (Web + MCP) **ALB**の背後、**RDS Postgres**、CloudWatch ログ。

## 1. 前提条件

- Terraform ≥ 1.5 + AWS認証情報（`aws configure` / 環境変数）VPCスコープリソース、ECS、RDS、ALB、IAM作成権限
- レジストリ内の3つのイメージ ECSが取得可能（ECR、またはGHCR公開）

## 2. 初期化

```bash
cd deploy/aws
terraform init
```

## 3. 適用

```bash
terraform apply \
  -var image_registry=ghcr.io/your-org/cmind \
  -var image_tag=1.0.0 \
  -var owner_email=you@example.com \
  -var owner_password='Change-Me-Str0ng!' \
  -var pg_password="$(openssl rand -hex 16)" \
  -var discovery_join_token="$(openssl rand -hex 24)"
```

作成: RDS Postgres (`appdb`)、ECSクラスタ、Web + MCP用Fargateサービス、ALB (Web `ルート`、MCP `/mcp`)、セキュリティグループ、CloudWatch ロググループ、**ADOT (AWS OpenTelemetry ディストリビューション) コレクタサイドカー**各タスク内。アプリはOTLPをサイドカーにエクスポート、**X-Ray**にトレース、**CloudWatch** (EMF、名前空間 `cmind`) にメトリクス、ログは `awslogs` ドライバとしてコンパクトJSON。Web用に検出ON。タスク役割はサイドカーX-Ray + CloudWrite書き込みアクセスを付与—自分で実行するコレクタなし。

> 簡潔性のためアカウントのデフォルトVPC/サブネットを使用。本番環境向けに独自VPC、プライベートサブネット、HTTPSリスナー（ACM証明書）を設定。

## 4. URLを取得

```bash
terraform output web_url   # ALB ルート
terraform output mcp_url   # ALB /mcp
```

`web_url` を開く、オーナー（初回ログイン時にパスワード変更強制）でサインイン。

## 5. ノードエージェントを追加（別途）

Fargateは特権/DinDを許可しないため、エージェント`web_url`を指す他の場所で実行:

- **ECS on EC2** — 容量プロバイダー `privileged = true` タスク定義 `cmind-node-agent` 実行
- **EKS** — Helm チャート ([kubernetes.md](kubernetes.md)) `nodeAgent.privileged=true` 付き

`NodeAgent__MainUrl=<web_url>` 設定、`NodeAgent__AdvertiseUrl=<agent reachable url>`、`NodeAgent__JwtSecret=<discovery_join_token>`。エージェント自己登録 — [../operations/node-discovery.md](../operations/node-discovery.md) 参照。

## 6. 検証

```bash
aws logs tail /ecs/cmind --since 5m         # コンパクト JSON ログ
curl -s "$(terraform output -raw web_url)/version"
```

## 本番環境向け注記

- HTTPS リスナー + ACM 証明書追加; ALB セキュリティグループ制限。
- AWS Secrets Manager / SSM にシークレット保存、プレーンテキスト `environment` の代わりにタスク定義 `secrets` を経由注入。
- RDS マルチ AZ + バックアップ有効化。
- トレース (X-Ray)、メトリクス (CloudWatch EMF)、ログ (CloudWatch ログ) ADOT サイドカー経由自動配線; `trace_id` で相関。[../operations/logging.md](../operations/logging.md#aws--x-ray--cloudwatch-adot-sidecar) 参照。
- アプリは既にタスク内サイドカー `OTEL_EXPORTER_OTLP_ENDPOINT` を指す; 外部コレクタへのリポイント（集約化希望）。

## コピートレーディングエージェント + Secrets Manager (S5)

`deploy/aws/copy-agent.tf` が **copy-agent** ECS Fargate サービス `CopyEngineSupervisor` ホスト (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) **ALBなし** 追加—ワーカー長生存cTraderソケット。DB接続文字列 **AWS Secrets Manager** 格納、タスクの `secrets` ブロック経由注入（実行ロール `secretsmanager:GetSecretValue` のみそのシークレット上で付与）、プレーンテキスト環境ではなし。各タスク `NodeName` デフォルトコンテナホスト名（Fargateタスクごとにユニーク）、DB リース属性は実行プロファイルをタスクごとに — 2つのタスクが1つをホストしない。`copy_agent_count` スケール でコピー容量追加; DataProtection キーリング Postgres 経由共有、任意のタスクが格納Open APIトークン復号化可能。
