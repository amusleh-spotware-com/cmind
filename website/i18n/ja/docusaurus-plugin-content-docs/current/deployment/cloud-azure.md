---
description: "deploy/azure/main.bicep ステートレス層を Azure Container Apps にプロビジョン + Postgres Flexible Server + Log Analytics。"
---

# Azure デプロイメント — ステップバイステップ

`deploy/azure/main.bicep` ステートレス層を **Azure Container Apps** + **Postgres Flexible Server** + Log Analytics にプロビジョン。

## 1. 前提条件

- Azure CLI (`az login` 実行)、サブスクリプション、リソースグループ作成権限
- Azure が取得可能なレジストリに3つのイメージプッシュ（例 GHCR public、または ACR）

## 2. リソースグループ作成

```bash
az group create -n cmind-rg -l westeurope
```

## 3. Bicep デプロイ

```bash
az deployment group create -g cmind-rg -f deploy/azure/main.bicep \
  -p imageRegistry=ghcr.io/your-org/cmind imageTag=1.0.0 \
     ownerEmail=you@example.com \
     ownerPassword='Change-Me-Str0ng!' \
     pgPassword="$(openssl rand -hex 16)" \
     discoveryJoinToken="$(openssl rand -hex 24)"
```

作成: Container Apps 環境、Web (外部イングレス)、MCP (外部イングレス)、Postgres Flexible Server + `appdb`、Log Analytics、**ワークスペースベース Application Insights** コンポーネント。Web に検出ON。接続文字列が Web + MCP に `APPLICATIONINSIGHTS_CONNECTION_STRING` として注入、トレース + メトリクス ネイティブに App Insights にエクスポート、ログは同じ Log Analytics ワークスペースに — コレクタ不要。`-p otlpEndpoint=...` でパス*また* OTLP コレクタへのフォワード。

## 4. URL を取得

```bash
az deployment group show -g cmind-rg -n main --query properties.outputs
# webUrl, mcpUrl
```

`webUrl` を開く、オーナー（初回ログイン時にパスワード変更強制）でサインイン。

## 5. ノードエージェントを追加（別途）

Container Apps は特権/DinD を実行できないため、エージェント `webUrl` を指す他の場所で実行:

- **AKS** — Helm チャート ([kubernetes.md](kubernetes.md)) `nodeAgent.privileged=true` デプロイ、Web/MCP をスケール 0 に（エージェント層のみ希望）
- **VM / VMSS** — `cmind-node-agent` イメージ `--privileged` 実行 `NodeAgent:MainUrl=<webUrl>`, `NodeAgent:AdvertiseUrl=<vm reachable url>`, `NodeAgent:JwtSecret=<discoveryJoinToken>`

エージェント自己登録 1ハートビート間隔内 — [../operations/node-discovery.md](../operations/node-discovery.md) 参照。

## 6. 検証

```bash
az containerapp logs show -g cmind-rg -n cmind-web --tail 50   # コンパクト JSON ログ
curl -s <webUrl>/version
```

## 本番環境向け注記

- Azure Front Door / App Gateway で Web フロント TLS + WAF
- シークレット Key Vault に保存; 安定 Data Protection 証明書 (`App__DataProtectionCertBase64` / `...Password`) 渡す キーリング レプリカ再起動 生き残る
- App Insights (トレース+メトリクス) + Log Analytics (ログ) 自動配線; `trace_id` で相関。[../operations/logging.md](../operations/logging.md#azure--application-insights--log-analytics) 参照
- `otlpEndpoint` パラメータ設定（または `OTEL_EXPORTER_OTLP_ENDPOINT` アプリ）*また* コレクタへのフォワード
- Container Apps `scale` ルール (最小/最大) Bicep で配線

## コピートレーディングエージェント + Key Vault (S5)

`deploy/azure/main.bicep` *また* **copy-agent** Container App プロビジョン `CopyEngineSupervisor` ホスト (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) **イングレスなし** — ワーカー長生存cTraderソケット。**ユーザー割り当て管理 ID** (Key Vault Secrets User ロール) 経由 **Azure Key Vault** シークレットから DB 接続文字列読み取り インラインプレーンテキストシークレットではなく。各レプリカ `NodeName` デフォルトコンテナホスト名（ユニーク）、DB リース属性は実行プロファイルをレプリカごとに が 2つのレプリカが1つをホストしない。`minReplicas`/`maxReplicas` スケール コピー容量追加; DataProtection キーリング Postgres 経由共有、任意レプリカが格納Open APIトークン復号化可能。出力: `copyAgentName`, `keyVaultName`。
