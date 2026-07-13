---
slug: /for-cloud-providers
title: 클라우드 & VPS 제공자를 위한 cMind
description: 클라우드 또는 VPS 제공자가 관리형 cMind 호스팅을 제공해야 하는 이유 — algo 트레이더, 브로커, prop firms, 분명한 수익화 방법과 함께, 컴퓨팅, 화이트라벨 리셀링, 관리 AI를 위한 즉시 사용 가능한, 차별화된 제품.
keywords:
  - 관리형 호스팅
  - VPS 제공자
  - 클라우드 제공자
  - 거래 플랫폼 호스팅
  - 화이트라벨 리셀러
  - 관리 AI 호스팅
sidebar_position: 7
---

# 클라우드 & VPS 제공자를 위한 cMind 🖥️

당신은 이미 컴퓨팅을 임대합니다. cMind는 즉시 사용 가능한, 오픈 소스 제품입니다 당신이 컴퓨팅을 감싸고 있습니다: **관리형 cMind 호스팅을 제공하세요** 그리고 높은 가치, sticky, 계산량이 많은 워크로드를 확보합니다 — algo 트레이더, 브로커, prop firms, 그리고 플랫폼을 자신들이 운영 팀이 되지 않고도 실행하고 싶은 거래 커뮤니티.

:::tip TL;DR
stateless 계층 + Postgres + 노드 플릿을 실행하세요; 고객에게 브랜드 URL을 주세요. 구독, 컴퓨팅, 화이트라벨, AI를 수익화하세요. → [클라우드에 배포](./deployment/cloud.md)
:::

## 관리형 cMind를 제공하는 이유

- **빌드 비용 없음.** 오픈 소스, MIT 라이센스, 그리고 이미 문서화, 테스트, 컨테이너화. 당신은 패키지 및 운영합니다 — 당신은 빌드하지 않습니다.
- **lucrative niche를 위한 차별화된 제품.** Algo 거래는 계산량이 많습니다: 백테스트 및 실시간 노드는 CPU를 태웁니다, 이는 *청구 가능한 사용*입니다 당신이 이미 판매합니다.
- **Sticky 고객.** 플랫폼 안에서 전략을 빌드하고 실행하는 트레이더는 casual하게 이탈하지 않습니다.
- **한 가지 주의를 upsell로 전환합니다.** cMind는 설계상 자체 호스팅입니다 — "운영 팀이 되기 싫은" 고객의 경우, *당신*이 답입니다.

## 누가 당신으로부터 관리형 cMind를 사는가

- **개별 quants & 트레이더** 호스팅된 것을 원하는. → [트레이더를 위해](./for-traders.md)
- **cTrader 브로커** 클라이언트를 위해 화이트라벨을 실행. → [브로커를 위해](./for-brokers.md)
- **Prop firms & 복사 거래 비즈니스** 브랜드, 감사 가능한 인프라가 필요한.

## "관리형 cMind"는 운영할 의미

당신은 3개의 계층을 운영합니다; 고객은 브랜드 웹 URL을 얻습니다:

| 계층 | 무엇인가 | 어디서 실행 |
|---|---|---|
| Stateless (Web + MCP) | 앱 + API + MCP 서버 | 모든 컨테이너 플랫폼, autoscaled |
| 데이터베이스 | PostgreSQL | 관리되는 Postgres (RDS / Flexible Server / 당신의 것) |
| 노드 플릿 | cTrader 컨테이너를 빌드 & 실행 | **VMs 또는 Kubernetes — privileged Docker 필요** |

:::warning 미리 범위 지정할 한 가지
노드 에이전트는 cTrader 컨테이너를 빌드하고 실행하므로, **privileged Docker**가 필요합니다. 그것은 serverless 컨테이너 런타임(Azure Container Apps, AWS Fargate)을 제외합니다 *에이전트를 위해* — [Kubernetes](./deployment/kubernetes.md), VM, 또는 EC2에서 실행하세요. stateless 계층은 어디서나 실행됩니다.
:::

실제, copy-paste 배포 가이드는 이것을 구체적으로 만듭니다: [클라우드 개요](./deployment/cloud.md) · [Azure](./deployment/cloud-azure.md) · [AWS](./deployment/cloud-aws.md) · [Kubernetes](./deployment/kubernetes.md) · [스케일링](./deployment/scaling.md).

## 당신이 그것을 수익화하는 방법

- **관리형 호스팅 구독.** Monthly Starter / Team / Business 계획은 노드 플릿 및 백테스트 동시성으로 크기 조정.
- **사용 & 컴퓨팅 미터링.** 백테스트 시간, 실시간 노드 시간, 저장소를 청구하세요 — 당신이 이미 실행하는 컨테이너 플릿으로 자연스럽게 미터링.
- **화이트라벨 리셀러 계층.** 전체 리브랜드(로고, 색상, PWA, `ShowSiteLink=false`)를 위해 더 많이 청구하고 [기능 토글](./features/feature-toggles.md)을 통해 프리미엄 기능을 활성화. → [화이트라벨](./features/white-label.md)
- **관리 AI.** 기본 AI 제공자 키를 번들해서 모든 고객의 사용자가 설정 없이 AI를 얻으세요, 사용을 마크업하세요 — 또는 bring-your-own-key를 제공합니다. → [AI 기능](./features/ai.md)
- **Prop-firm & 복사 거래 수익 공유.** 도전과 성과 수수료를 실행하고 플랫폼 컷을 취하는 호스트 회사. → [Prop-firm](./features/prop-firm.md) · [성과 수수료](./features/copy-performance-fees.md) · [제공자 마켓플레이스](./features/copy-provider-marketplace.md)
- **설정, 온보딩 & SLA.** 전문 서비스 및 프리미엄 지원을 첨부합니다.

## 멀티테넌트 패턴

- **배포별 테넌트(권장).** 고객별 하나의 브랜드 인스턴스 — 강한 격리, 테넌트별 브랜딩 및 데이터베이스, 테넌트별 구별되는 노드 조인 토큰. 브랜딩은 `IOptionsMonitor`에서 읽으므로, 각 인스턴스는 자신의 정체성을 전달합니다. → [멀티테넌트 브랜딩](./white-label-for-business.md#multi-tenant-per-customer-branding) · [노드 디스커버리](./operations/node-discovery.md)
- **공유 제어 평면(고급).** 당신의 자신의 프로비저닝 계층에서 많은 인스턴스를 구동하고, 프로그래매틱하게 테넌트별로 브랜딩 및 기능을 시딩.

## 청구를 위해 사용 측정

소유자/관리자 전용 **`GET /api/usage`** 엔드포인트는 제공자가 폴링하고 청구할 수 있는 읽기 전용 요약을 반환합니다 — 새로운 도메인 또는 지속성 없이, 기존 상태를 프로젝트:

```json
{
  "users": { "total": 42 },
  "nodes": { "total": 6, "online": 5 },
  "instances": { "total": 1280, "backtestsRunning": 3, "runsRunning": 11 },
  "cbots": { "total": 210 },
```
