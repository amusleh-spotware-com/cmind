---
description: "GitHub 发布：带版本的容器镜像 (GHCR)、Helm chart 和 CtraderCliNode 二进制文件 — 如何获取一个发布并从中运行应用。"
---

# 发布与运行一个发布

cMind 以带版本的 **GitHub 发布** 形式交付。每个发布针对一个 SemVer 标签发布以下内容：

- **容器镜像**（GHCR）— `ghcr.io/amusleh-spotware-com/cmind-{web,mcp,node-agent,copy-agent,tests}`，
  以版本（例如 `1.0.0-alpha.1`）和 `sha-<commit>` 打标签。已签名（cosign keyless），带有构建来源证明和 SPDX SBOM。
- **Helm chart** — 推送到 `oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind`，并作为
  `cmind-<version>.tgz` 附加到发布中。
- **CtraderCliNode 二进制文件** — 按平台提供的自包含 ZIP（`linux-x64`、`linux-arm64`、`win-x64`、
  `osx-arm64`），用于在没有 .NET SDK 的情况下运行远程节点代理。
- **`SHA256SUMS.txt`** — 覆盖每个附加的产物。

> **Alpha。** 目前每个发布都是预发布（`-alpha.N`）。alpha 之间可能有破坏性变更；尚无升级/迁移保证。
> 请固定确切版本 — 切勿使用 `latest`。

## 版本管理

SemVer 2.0.0。标签形式为 `vX.Y.Z[-suffix]`。后缀（`-alpha.N`、`-beta.N`、`-rc.N`）会发布一个 GitHub
**预发布**；镜像标签和 Helm chart 版本都等于去掉前导 `v` 的版本。运行中的应用会在 `GET /version` 和 UI
页脚（`Core.VersionInfo`）中显示它。

## 选择一个发布

浏览 **[Releases](https://github.com/amusleh-spotware-com/cmind/releases)** 并复制你想要的标签（例如
`v1.0.0-alpha.1`）。在运行镜像之前先验证它：

```bash
VERSION=1.0.0-alpha.1
cosign verify ghcr.io/amusleh-spotware-com/cmind-web:$VERSION \
  --certificate-identity-regexp 'https://github.com/amusleh-spotware-com/cmind/.github/workflows/release.yml@.*' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

## 运行 — Kubernetes（Helm，推荐）

chart 的 `appVersion` 已经固定了对应的镜像标签，因此你只需传入 chart 版本。

```bash
VERSION=1.0.0-alpha.1

helm install cmind oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind \
  --version $VERSION \
  --namespace cmind --create-namespace \
  --set secrets.pgPassword='<strong>' \
  --set secrets.ownerEmail='owner@example.com' \
  --set secrets.ownerPassword='<Strong-Pass!>' \
  --set secrets.discoveryJoinToken='<至少32个字符的集群密钥>'
```

私有 GHCR 包需要镜像拉取密钥 — 创建一个并传入：

```bash
kubectl create secret docker-registry ghcr --namespace cmind \
  --docker-server=ghcr.io --docker-username=<gh-user> --docker-password=<带 read:packages 的 PAT>
helm upgrade cmind ... --set image.pullSecrets='{ghcr}'
```

完整的 chart 选项、ingress、外部 Postgres 和扩缩容：请参见 **[Kubernetes 部署](kubernetes.md)** 和
**[扩缩容](scaling.md)**。验证：

```bash
kubectl -n cmind get pods
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# → http://localhost:8080 ；GET /version 返回发布版本
```

## 运行 — Docker（单主机，快速查看）

直接从其发布镜像运行 Web 主机。它需要 Postgres 和 Docker 套接字（Web 主机通过本地 Docker CLI 构建/运行
cBot）。

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

打开 `http://localhost:8080`。以相同方式添加 MCP 服务器（`cmind-mcp`）和节点代理；完整的多服务拓扑请使用
Helm chart。当从源码而非发布工作时的 Aspire `dotnet run` 路径，请参见 **[本地开发](local.md)**。

## 从二进制文件运行远程节点代理

提供运行/回测能力的远程主机可以在未安装 .NET 的情况下运行 `CtraderCliNode`。从发布中下载平台 ZIP，解压并
运行 — 它会自动向 Web 主机注册并发送心跳。

```bash
VERSION=1.0.0-alpha.1
curl -LO https://github.com/amusleh-spotware-com/cmind/releases/download/v$VERSION/ctrader-cli-node-$VERSION-linux-x64.zip
sha256sum -c ctrader-cli-node-$VERSION-linux-x64.zip.sha256
unzip ctrader-cli-node-$VERSION-linux-x64.zip -d cmind-node && cd cmind-node

NodeAgent__MainBaseUrl='https://<你的-web-主机>' \
NodeAgent__DiscoveryJoinToken='<相同的至少32个字符的集群密钥>' \
./CtraderCliNode
```

主机必须运行 Docker（代理通过 Docker CLI 执行 cTrader 控制台镜像）。要将节点代理作为特权 Pod 运行，请参见
**[Kubernetes 部署](kubernetes.md)**。

## 制作一个发布（维护者）

发布由 `.github/workflows/release.yml` 在任何推送的 `v*` 标签上生成 — 流程见仓库根目录的
**[RELEASING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/RELEASING.md)**。
