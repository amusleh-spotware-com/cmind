---
description: "Releases do GitHub: imagens de contêiner versionadas (GHCR), chart Helm e binários CtraderCliNode — como obter uma release e executar o app a partir dela."
---

# Releases e executar uma release

O cMind é entregue como **Releases do GitHub** versionadas. Cada release publica, para uma tag SemVer:

- **Imagens de contêiner** no GHCR — `ghcr.io/amusleh-spotware-com/cmind-{web,mcp,node-agent,copy-agent,tests}`,
  marcadas com a versão (ex.: `1.0.0-alpha.1`) e `sha-<commit>`. Assinadas (cosign keyless) com atestações
  de proveniência de build e uma SBOM SPDX.
- **Chart Helm** — enviado para `oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind` e anexado à
  release como `cmind-<version>.tgz`.
- **Binários CtraderCliNode** — ZIPs self-contained por plataforma (`linux-x64`, `linux-arm64`, `win-x64`,
  `osx-arm64`) para executar um agente de nó remoto sem o SDK .NET.
- **`SHA256SUMS.txt`** cobrindo cada artefato anexado.

> **Alpha.** Por ora, toda release é uma pré-release (`-alpha.N`). Espere mudanças incompatíveis entre as
> alphas; ainda não há garantia de upgrade/migração. Fixe uma versão exata — nunca `latest`.

## Versionamento

SemVer 2.0.0. Forma da tag `vX.Y.Z[-suffix]`. Um sufixo (`-alpha.N`, `-beta.N`, `-rc.N`) publica uma
**pré-release** do GitHub; a tag da imagem e a versão do chart Helm são iguais à versão sem o `v` inicial.
O app em execução a expõe em `GET /version` e no rodapé da UI (`Core.VersionInfo`).

## Escolher uma release

Navegue pelas **[Releases](https://github.com/amusleh-spotware-com/cmind/releases)** e copie a tag
desejada (ex.: `v1.0.0-alpha.1`). Verifique uma imagem antes de executá-la:

```bash
VERSION=1.0.0-alpha.1
cosign verify ghcr.io/amusleh-spotware-com/cmind-web:$VERSION \
  --certificate-identity-regexp 'https://github.com/amusleh-spotware-com/cmind/.github/workflows/release.yml@.*' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

## Executar — Kubernetes (Helm, recomendado)

O `appVersion` do chart já fixa a tag de imagem correspondente, então você passa apenas a versão do chart.

```bash
VERSION=1.0.0-alpha.1

helm install cmind oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind \
  --version $VERSION \
  --namespace cmind --create-namespace \
  --set secrets.pgPassword='<strong>' \
  --set secrets.ownerEmail='owner@example.com' \
  --set secrets.ownerPassword='<Strong-Pass!>' \
  --set secrets.discoveryJoinToken='<segredo de cluster de 32+ caracteres>'
```

Pacotes privados do GHCR precisam de um secret de pull de imagem — crie um e o passe:

```bash
kubectl create secret docker-registry ghcr --namespace cmind \
  --docker-server=ghcr.io --docker-username=<gh-user> --docker-password=<PAT-com-read:packages>
helm upgrade cmind ... --set image.pullSecrets='{ghcr}'
```

Opções completas do chart, ingress, Postgres externo e escalonamento: veja
**[Implantação no Kubernetes](kubernetes.md)** e **[Escalonamento](scaling.md)**. Verificar:

```bash
kubectl -n cmind get pods
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# → http://localhost:8080 ; GET /version retorna a versão da release
```

## Executar — Docker (host único, olhada rápida)

Execute o host Web diretamente da sua imagem de release. Ele precisa de Postgres e do socket do Docker (o
host Web compila/executa cBots via a CLI local do Docker).

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

Abra `http://localhost:8080`. Adicione o servidor MCP (`cmind-mcp`) e os agentes de nó da mesma forma;
para a topologia multisserviço completa, use o chart Helm. Veja **[Desenvolvimento local](local.md)**
para o caminho Aspire `dotnet run` ao trabalhar a partir do código-fonte em vez de uma release.

## Executar um agente de nó remoto a partir de um binário

Hosts remotos que fornecem capacidade de run/backtest podem executar `CtraderCliNode` sem o .NET
instalado. Baixe o ZIP da plataforma na release, descompacte e execute — ele se registra automaticamente
no host Web e envia heartbeats.

```bash
VERSION=1.0.0-alpha.1
curl -LO https://github.com/amusleh-spotware-com/cmind/releases/download/v$VERSION/ctrader-cli-node-$VERSION-linux-x64.zip
sha256sum -c ctrader-cli-node-$VERSION-linux-x64.zip.sha256
unzip ctrader-cli-node-$VERSION-linux-x64.zip -d cmind-node && cd cmind-node

NodeAgent__MainBaseUrl='https://<seu-host-web>' \
NodeAgent__DiscoveryJoinToken='<o mesmo segredo de cluster de 32+ caracteres>' \
./CtraderCliNode
```

O host precisa executar Docker (o agente executa a imagem do console cTrader via CLI do Docker). Veja
**[Implantação no Kubernetes](kubernetes.md)** para executar agentes de nó como pods privilegiados.

## Criar uma release (mantenedores)

As releases são produzidas por `.github/workflows/release.yml` em qualquer tag `v*` enviada — o processo
está em **[RELEASING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/RELEASING.md)** na raiz
do repositório.
