---
title: 클라우드에 배포
description: Azure, AWS 또는 Kubernetes에 cMind를 배포합니다. 어떤 플랫폼이 적합한지, 필수 조건 및 단계별 가이드.
sidebar_position: 2
---

# 클라우드에 배포 ☁️

노트북을 벗어났다면? 이제 cMind를 실제 인프라에 배포할 시간입니다. 좋은 소식: ZooKeeper 없이, 리더 선택 없이, 단지 복제본과 데이터베이스만으로 거의 운영 작업 없이 확장하도록 설계되었습니다.

**미리 알아야 할 한 가지:** 무상태 계층 (Web + MCP)은 *모든* 컨테이너 플랫폼에서 잘 작동하지만 **노드 에이전트는 권한 있는 Docker**가 필요합니다 (cTrader 컨테이너를 빌드하고 실행함). 이는 *에이전트*에 대해 Azure Container Apps 및 AWS Fargate와 같은 서버리스 런타임을 제외합니다 — [Kubernetes](./kubernetes.md), VM 또는 EC2에서 실행하고 Web URL을 지정합니다.

경로를 선택하세요:

- 🟦 **[Azure](./cloud-azure.md)** — Container Apps + Postgres Flexible Server (Bicep).
- 🟧 **[AWS](./cloud-aws.md)** — ECS Fargate + ALB + RDS (Terraform).
- ⎈ **[Kubernetes](./kubernetes.md)** — Helm 차트, AKS / EKS / 어디서나 작동.
- 📈 **[확장](./scaling.md)** — 배포 후 모든 것이 어떻게 확장되고 자체 복구되는지.

무상태 계층 (Web + MCP)은 모든 컨테이너 플랫폼에서 실행되며 Postgres = 관리 데이터베이스.
**노드 에이전트는 권한 있는 Docker (DinD)가 필요합니다** — 서버리스 컨테이너 런타임 (Azure Container Apps, AWS Fargate)은 이를 차단합니다. Kubernetes ([kubernetes.md](kubernetes.md)) 또는 VM/EC2에서 에이전트를 실행하고 Web URL을 지정합니다.

| 클라우드 | 무상태 계층 | 데이터베이스 | 가이드 |
| ----- | -------------- | -------- | ----- |
| Azure | Container Apps (Bicep) | Postgres Flexible Server | [cloud-azure.md](cloud-azure.md) |
| AWS | ECS Fargate + ALB (Terraform) | RDS Postgres | [cloud-aws.md](cloud-aws.md) |

공통 필수 조건, 둘 다:

1. 세 개의 이미지를 빌드하고 레지스트리에 푸시합니다 (`cmind-web`, `cmind-mcp`, `cmind-node-agent`).
2. 비밀을 선택합니다: DB 암호, 소유자 이메일/암호, **디스커버리 조인 토큰** (≥ 32자) Web 앱 + 모든 노드 에이전트가 공유.
3. IaC를 배포 (아래)한 다음 노드 에이전트를 별도로 (K8s/VM)에 띄웁니다 `NodeAgent__MainUrl` = 배포된 Web URL, `NodeAgent__JwtSecret` = 조인 토큰.

디스커버리, 로깅, 프로브는 로컬/K8s 설정과 동일하게 작동합니다 — [../operations/node-discovery.md](../operations/node-discovery.md) 및 [../operations/logging.md](../operations/logging.md)를 참조하세요.
