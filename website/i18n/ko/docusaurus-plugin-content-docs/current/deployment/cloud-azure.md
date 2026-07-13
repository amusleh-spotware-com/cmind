---
description: "deploy/azure/main.bicep는 Azure Container Apps + Postgres Flexible Server + Log Analytics에 무상태 계층을 프로비저닝합니다."
---

# Azure 배포 — 단계별

`deploy/azure/main.bicep`는 **Azure Container Apps** 더하기 **Postgres Flexible Server** + Log Analytics에 무상태 계층을 프로비저닝합니다.

## 1. 사전 조건

- Azure CLI (`az login` 완료), 구독, 리소스 그룹 생성 권한.
- Azure가 풀 수 있는 레지스트리로 푸시된 3개 이미지 (예: GHCR 공개 또는 ACR).

## 2. 리소스 그룹 만들기

```bash
az group create -n cmind-rg -l westeurope
```

## 3. Bicep 배포

```bash
az deployment group create -g cmind-rg -f deploy/azure/main.bicep \
  -p imageRegistry=ghcr.io/your-org/cmind imageTag=1.0.0 \
     ownerEmail=you@example.com \
     ownerPassword='Change-Me-Str0ng!' \
     pgPassword="$(openssl rand -hex 16)" \
     discoveryJoinToken="$(openssl rand -hex 24)"
```

생성: Container Apps 환경, Web (외부 수신), MCP (외부 수신), Postgres Flexible Server + `appdb`, Log Analytics, **워크스페이스 기반 Application Insights** 구성 요소. Web에서 발견이 켜져 있습니다. 연결 문자열은 Web + MCP에 `APPLICATIONINSIGHTS_CONNECTION_STRING`으로 주입되므로 추적 + 메트릭은 App Insights로 기본적으로 내보내고 로그는 동일한 Log Analytics 작업공간에 착륙합니다 — 수집기 필요 없음. OTLP 수집기로 **또한** 전달하려면 `-p otlpEndpoint=...`를 전달합니다.

## 4. URL 가져오기

```bash
az deployment group show -g cmind-rg -n main --query properties.outputs
# webUrl, mcpUrl
```

`webUrl`을 열고 소유자로 로그인합니다 (첫 로그인 시 암호 변경 강제).

## 5. 노드 에이전트 추가 (별도)

Container Apps는 권한이 있는/DinD를 실행할 수 없으므로 다른 곳에서 에이전트를 실행하고 `webUrl`을 가리킵니다:

- **AKS** — Helm 차트 ([kubernetes.md](kubernetes.md)) `nodeAgent.privileged=true`로 배포하고, 해당 곳의 에이전트 계층만 원하면 Web/MCP를 0으로 축소합니다.
- **VM / VMSS** — `cmind-node-agent` 이미지 `--privileged`를 `NodeAgent:MainUrl=<webUrl>`, `NodeAgent:AdvertiseUrl=<vm reachable url>`, `NodeAgent:JwtSecret=<discoveryJoinToken>`로 실행합니다.

에이전트는 하나의 하트비트 간격 내에 자체 등록합니다 — [../operations/node-discovery.md](../operations/node-discovery.md)를 참조하십시오.
