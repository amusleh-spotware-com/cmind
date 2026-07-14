---
description: "GitHub リリース: バージョン管理されたコンテナイメージ (GHCR)、Helm チャート、CtraderCliNode バイナリ — リリースの取得方法とそこからのアプリ実行方法。"
---

# リリースとリリースの実行

cMind はバージョン管理された **GitHub リリース** として配布されます。各リリースは 1 つの SemVer タグに対して以下を公開します:

- **コンテナイメージ**（GHCR）— `ghcr.io/amusleh-spotware-com/cmind-{web,mcp,node-agent,copy-agent,tests}`。
  バージョン（例: `1.0.0-alpha.1`）と `sha-<commit>` でタグ付けされます。ビルド来歴の証明と SPDX SBOM 付きで
  署名（cosign keyless）されています。
- **Helm チャート** — `oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind` にプッシュされ、リリースに
  `cmind-<version>.tgz` として添付されます。
- **CtraderCliNode バイナリ** — .NET SDK なしでリモートノードエージェントを実行するためのプラットフォーム別
  自己完結型 ZIP（`linux-x64`、`linux-arm64`、`win-x64`、`osx-arm64`）。
- **`SHA256SUMS.txt`** — 添付されたすべての成果物をカバーします。

> **アルファ。** 現時点ではすべてのリリースがプレリリース（`-alpha.N`）です。アルファ間では破壊的変更が
> 予想され、アップグレード/移行の保証はまだありません。正確なバージョンを固定してください — `latest` は使わないこと。

## バージョニング

SemVer 2.0.0。タグ形式は `vX.Y.Z[-suffix]`。サフィックス（`-alpha.N`、`-beta.N`、`-rc.N`）は GitHub の
**プレリリース** を公開します。イメージタグと Helm チャートのバージョンはどちらも先頭の `v` を除いたバージョンと
一致します。実行中のアプリは `GET /version` と UI フッター（`Core.VersionInfo`）でこれを表示します。

## リリースを選ぶ

**[Releases](https://github.com/amusleh-spotware-com/cmind/releases)** を参照し、目的のタグ（例:
`v1.0.0-alpha.1`）をコピーします。実行前にイメージを検証します:

```bash
VERSION=1.0.0-alpha.1
cosign verify ghcr.io/amusleh-spotware-com/cmind-web:$VERSION \
  --certificate-identity-regexp 'https://github.com/amusleh-spotware-com/cmind/.github/workflows/release.yml@.*' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

## 実行 — Kubernetes（Helm、推奨）

チャートの `appVersion` はすでに対応するイメージタグを固定しているため、渡すのはチャートのバージョンだけです。

```bash
VERSION=1.0.0-alpha.1

helm install cmind oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind \
  --version $VERSION \
  --namespace cmind --create-namespace \
  --set secrets.pgPassword='<strong>' \
  --set secrets.ownerEmail='owner@example.com' \
  --set secrets.ownerPassword='<Strong-Pass!>' \
  --set secrets.discoveryJoinToken='<32文字以上のクラスターシークレット>'
```

プライベートな GHCR パッケージにはイメージプルシークレットが必要です — 作成して渡します:

```bash
kubectl create secret docker-registry ghcr --namespace cmind \
  --docker-server=ghcr.io --docker-username=<gh-user> --docker-password=<read:packages 権限の PAT>
helm upgrade cmind ... --set image.pullSecrets='{ghcr}'
```

チャートの全オプション、Ingress、外部 Postgres、スケーリングについては
**[Kubernetes デプロイ](kubernetes.md)** と **[スケーリング](scaling.md)** を参照してください。確認:

```bash
kubectl -n cmind get pods
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# → http://localhost:8080 ; GET /version はリリースバージョンを返します
```

## 実行 — Docker（単一ホスト、簡易確認）

Web ホストをそのリリースイメージから直接実行します。Postgres と Docker ソケットが必要です（Web ホストは
ローカルの Docker CLI を介して cBot をビルド/実行します）。

```bash
VERSION=1.0.0-alpha.1
docker network create cmind

docker run -d --name cmind-pg --network cmind \
  -e POSTGRES_PASSWORD=change-me -e POSTGRES_DB=cmind postgres:17

docker run -d --name cmind-web --network cmind -p 8080:8080 \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -e ConnectionStrings__Default='Host=cmind-pg;Database=cmind;Username=postgres;Password=change-me' \
  -e App__Owner__Email='owner@example.com' \
  -e App__Owner__Password='Change-Me-Str0ng!' \
  ghcr.io/amusleh-spotware-com/cmind-web:$VERSION
```

`http://localhost:8080` を開きます。MCP サーバー（`cmind-mcp`）とノードエージェントも同様に追加します。
完全なマルチサービス構成には Helm チャートを使用してください。リリースではなくソースから作業する場合の
Aspire `dotnet run` の手順は **[ローカル開発](local.md)** を参照してください。

## バイナリからリモートノードエージェントを実行する

実行/バックテストの能力を提供するリモートホストは、.NET をインストールせずに `CtraderCliNode` を実行できます。
リリースからプラットフォーム用 ZIP をダウンロードして展開し、実行します — Web ホストに自動登録してハートビートを
送信します。

```bash
VERSION=1.0.0-alpha.1
curl -LO https://github.com/amusleh-spotware-com/cmind/releases/download/v$VERSION/ctrader-cli-node-$VERSION-linux-x64.zip
sha256sum -c ctrader-cli-node-$VERSION-linux-x64.zip.sha256
unzip ctrader-cli-node-$VERSION-linux-x64.zip -d cmind-node && cd cmind-node

NodeAgent__MainBaseUrl='https://<あなたの-web-ホスト>' \
NodeAgent__DiscoveryJoinToken='<同じ32文字以上のクラスターシークレット>' \
./CtraderCliNode
```

ホストは Docker を実行している必要があります（エージェントは Docker CLI を介して cTrader コンソールイメージを
実行します）。ノードエージェントを特権 Pod として実行するには **[Kubernetes デプロイ](kubernetes.md)** を参照してください。

## リリースの作成（メンテナー）

リリースはプッシュされた `v*` タグに対して `.github/workflows/release.yml` によって生成されます — 手順は
リポジトリルートの **[RELEASING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/RELEASING.md)**
にあります。
