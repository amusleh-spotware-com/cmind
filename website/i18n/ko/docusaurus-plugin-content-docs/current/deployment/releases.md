---
description: "GitHub 릴리스: 버전이 지정된 컨테이너 이미지(GHCR), Helm 차트, CtraderCliNode 바이너리 — 릴리스를 받아 앱을 실행하는 방법."
---

# 릴리스 및 릴리스 실행

cMind는 버전이 지정된 **GitHub 릴리스**로 배포됩니다. 각 릴리스는 하나의 SemVer 태그에 대해 다음을 게시합니다:

- **컨테이너 이미지**(GHCR) — `ghcr.io/amusleh-spotware-com/cmind-{web,mcp,node-agent,copy-agent,tests}`,
  버전(예: `1.0.0-alpha.1`)과 `sha-<commit>`로 태그됩니다. 빌드 출처 증명 및 SPDX SBOM과 함께 서명(cosign keyless)됩니다.
- **Helm 차트** — `oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind`로 푸시되고 릴리스에
  `cmind-<version>.tgz`로 첨부됩니다.
- **CtraderCliNode 바이너리** — .NET SDK 없이 원격 노드 에이전트를 실행하기 위한 플랫폼별 자체 포함 ZIP
  (`linux-x64`, `linux-arm64`, `win-x64`, `osx-arm64`).
- 첨부된 모든 아티팩트를 포함하는 **`SHA256SUMS.txt`**.

> **알파.** 현재 모든 릴리스는 프리릴리스(`-alpha.N`)입니다. 알파 간에 호환성이 깨지는 변경이 있을 수 있으며,
> 아직 업그레이드/마이그레이션 보장이 없습니다. 정확한 버전을 고정하세요 — `latest`는 사용하지 마세요.

## 버전 관리

SemVer 2.0.0. 태그 형식은 `vX.Y.Z[-suffix]`. 접미사(`-alpha.N`, `-beta.N`, `-rc.N`)는 GitHub **프리릴리스**를
게시합니다. 이미지 태그와 Helm 차트 버전은 모두 선행 `v`를 제외한 버전과 동일합니다. 실행 중인 앱은 이를
`GET /version` 및 UI 푸터(`Core.VersionInfo`)에 표시합니다.

## 릴리스 선택

**[Releases](https://github.com/amusleh-spotware-com/cmind/releases)**를 살펴보고 원하는 태그(예:
`v1.0.0-alpha.1`)를 복사합니다. 실행하기 전에 이미지를 검증합니다:

```bash
VERSION=1.0.0-alpha.1
cosign verify ghcr.io/amusleh-spotware-com/cmind-web:$VERSION \
  --certificate-identity-regexp 'https://github.com/amusleh-spotware-com/cmind/.github/workflows/release.yml@.*' \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

## 실행 — Kubernetes(Helm, 권장)

차트의 `appVersion`이 이미 해당 이미지 태그를 고정하므로 차트 버전만 전달하면 됩니다.

```bash
VERSION=1.0.0-alpha.1

helm install cmind oci://ghcr.io/amusleh-spotware-com/cmind/charts/cmind \
  --version $VERSION \
  --namespace cmind --create-namespace \
  --set secrets.pgPassword='<strong>' \
  --set secrets.ownerEmail='owner@example.com' \
  --set secrets.ownerPassword='<Strong-Pass!>' \
  --set secrets.discoveryJoinToken='<32자 이상 클러스터 시크릿>'
```

비공개 GHCR 패키지에는 이미지 풀 시크릿이 필요합니다 — 하나 생성하여 전달합니다:

```bash
kubectl create secret docker-registry ghcr --namespace cmind \
  --docker-server=ghcr.io --docker-username=<gh-user> --docker-password=<read:packages 권한 PAT>
helm upgrade cmind ... --set image.pullSecrets='{ghcr}'
```

전체 차트 옵션, 인그레스, 외부 Postgres, 스케일링은 **[Kubernetes 배포](kubernetes.md)**와
**[스케일링](scaling.md)**을 참조하세요. 확인:

```bash
kubectl -n cmind get pods
kubectl -n cmind port-forward svc/cmind-web 8080:8080
# → http://localhost:8080 ; GET /version 은 릴리스 버전을 반환합니다
```

## 실행 — Docker(단일 호스트, 빠른 확인)

릴리스 이미지에서 Web 호스트를 직접 실행합니다. Postgres와 Docker 소켓이 필요합니다(Web 호스트는 로컬 Docker
CLI를 통해 cBot을 빌드/실행합니다).

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

`http://localhost:8080`을 엽니다. MCP 서버(`cmind-mcp`)와 노드 에이전트도 같은 방식으로 추가합니다. 완전한
멀티 서비스 토폴로지는 Helm 차트를 사용하세요. 릴리스 대신 소스에서 작업할 때의 Aspire `dotnet run` 경로는
**[로컬 개발](local.md)**을 참조하세요.

## 바이너리에서 원격 노드 에이전트 실행

실행/백테스트 용량을 제공하는 원격 호스트는 .NET 설치 없이 `CtraderCliNode`를 실행할 수 있습니다. 릴리스에서
플랫폼 ZIP을 다운로드하여 압축을 풀고 실행합니다 — Web 호스트에 자동 등록하고 하트비트를 보냅니다.

```bash
VERSION=1.0.0-alpha.1
curl -LO https://github.com/amusleh-spotware-com/cmind/releases/download/v$VERSION/ctrader-cli-node-$VERSION-linux-x64.zip
sha256sum -c ctrader-cli-node-$VERSION-linux-x64.zip.sha256
unzip ctrader-cli-node-$VERSION-linux-x64.zip -d cmind-node && cd cmind-node

NodeAgent__MainBaseUrl='https://<your-web-host>' \
NodeAgent__DiscoveryJoinToken='<동일한 32자 이상 클러스터 시크릿>' \
./CtraderCliNode
```

호스트는 Docker를 실행해야 합니다(에이전트는 Docker CLI를 통해 cTrader 콘솔 이미지를 실행합니다). 노드
에이전트를 특권 파드로 실행하려면 **[Kubernetes 배포](kubernetes.md)**를 참조하세요.

## 릴리스 만들기(관리자)

릴리스는 푸시된 `v*` 태그에 대해 `.github/workflows/release.yml`에 의해 생성됩니다 — 절차는 저장소 루트의
**[RELEASING.md](https://github.com/amusleh-spotware-com/cmind/blob/main/RELEASING.md)**에 있습니다.
