---
slug: /for-cloud-providers
title: 클라우드 및 VPS 제공자를 위한 cMind
description: 클라우드 또는 VPS 제공자가 관리형 cMind 호스팅을 제공해야 하는 이유 — algo 트레이더, 브로커 및 프로프irms을 위한 준비된 차별화 제품, 컴퓨팅, 화이트라벨 재판매 및 관리형 AI를 monetizing하는 명확한 방법.
keywords:
  - managed hosting
  - VPS provider
  - cloud provider
  - trading platform hosting
  - white-label reseller
  - managed AI hosting
sidebar_position: 7
---

# 클라우드 및 VPS 제공자를 위한 cMind

이미 컴퓨트를 임대합니다. cMind는 컴퓨트 주위에 래핑할 수 있는 준비된 오픈소스 제품입니다: **관리형 cMind 호스팅을 제공**하고 고가치, 끈적한, 컴퓨트를 많이 사용하는 워크로드를 확보합니다 — algo 트레이더, 브로커, 프로프irms 및 플랫폼을 실행하고 싶지만 ops 팀이 되고 싶지 않은 트레이딩 커뮤니티.

:::tip[TL;DR]
무상태 티어 + Postgres + 노드 플릿을 실행합니다; 고객에게 브랜드 URL을 제공합니다. 구독, 컴퓨팅, 화이트라벨 및 AI를 monetizing합니다. → [클라우드에 배포](./deployment/cloud.md)
:::

## 관리형 cMind를 제공해야 하는 이유

- **빌드 비용 없음.** MIT 라이선스의 오픈소스로 이미 문서화, 테스트 및 컨테이너화되어 있습니다. 패키지化和 운영합니다 — 빌드하지 않습니다.
- **유리한 니치을 위한 차별화된 제품.** Algo 거래는 컴퓨트를 많이 사용합니다: 백테스트 및 라이브 노드가 CPU를 consume하며 이는 이미 판매하는 billable 사용량입니다.
- **끈적한 고객.** 플랫폼 내에서 전략을 구축하고 실행하는 트레이더는 가볍게 churn하지 않습니다.
- **caveat를 upsell으로 전환.** cMind는 자체 호스팅용으로 설계되었습니다 — "ops 팀이 되고 싶지 않은" 고객을 위해 *당신이* 답입니다.

## 관리형 cMind를 구매하는 사람

- **개별 퀀트 및 트레이더** — 호스팅을 원합니다. → [거래자용](./for-traders.md)
- **cTrader 브로커** — 고객을 위한 화이트라벨용. → [브로커용](./for-brokers.md)
- **프로프irms 및 카피 트레이딩 비즈니스** — 브랜드화된 감사 가능한 인프라가 필요합니다.

## "관리형 cMind"를 실행한다는 것은 무슨 의미인가

세 티어를 운영합니다; 고객은 브랜드 URL을 얻습니다:

| 티어 | 내용 | 실행 위치 |
|---|---|---|
| 무상태 (Web + MCP) | 앱 + API + MCP 서버 | 모든 컨테이너 플랫폼, 오토스케일 |
| 데이터베이스 | PostgreSQL | 관리형 Postgres (RDS / Flexible Server / 자체) |
| 노드 플릿 | cTrader 컨테이너 빌드 및 실행 | **VM 또는 Kubernetes — 권한 있는 Docker 필요** |

:::warning[미리 범위를 정할 한 가지]
노드 에이전트는 cTrader 컨테이너를 빌드하고 실행하므로 **권한 있는 Docker가 필요합니다**. 이것은 에이전트에 대해 Azure Container Apps, AWS Fargate와 같은 서버리스 컨테이너 런타임을 배제합니다 — [Kubernetes](./deployment/kubernetes.md), VM 또는 EC2에서 에이전트를 실행합니다. 무상태 티어는 어디서나 실행됩니다.
:::

실제 복사-붙여넣기 배포 가이드가 이를 구체화합니다: [클라우드 개요](./deployment/cloud.md) · [Azure](./deployment/cloud-azure.md) · [AWS](./deployment/cloud-aws.md) · [Kubernetes](./deployment/kubernetes.md) · [확장](./deployment/scaling.md).

## monetizing하는 방법

- **관리형 호스팅 구독.** 노드 플릿 및 백테스트 동시성을 기준으로 한 Monthly Starter / Team / Business 플랜.
- **사용량 및 컴퓨팅 미터링.** 백테스트 시간, 라이브 노드 시간 및 스토리지를 Bill back — 이미 실행 중인 컨테이너 플릿으로 자연스럽게 측정됩니다.
- **화이트라벨 리셀러 티어.** 전체 리브랜드 (로고, 색상, PWA, `ShowSiteLink=false`) 및 [기능 토글](./features/feature-toggles.md)를 통해 프리미엄 기능 활성화를 위해 더 많이 청구합니다. → [화이트라벨](./features/white-label.md)
- **관리형 AI.** 모든 고객의 사용자가 설정 없이 AI를 얻도록 기본 AI 공급자 키를 번들로 제공하고 사용량을 표시하거나 BYOK를 제공합니다. → [AI 기능](./features/ai.md)
- **프로펌 및 카피 트레이딩 수익 공유.** 챌린지 및 성과 수수료를 실행하고 플랫폼 커트를 가져가는 firms를 Host합니다. → [프로펌](./features/prop-firm.md) · [성과 수수료](./features/copy-performance-fees.md) · [공급자 마켓플레이스](./features/copy-provider-marketplace.md)
- **설정, 온보딩 및 SLA.** 전문 서비스 및 프리미엄 지원을 연결합니다.

## 멀티 테넌트 패턴

- **배포당 테넌트 (권장).** 고객당 하나의 브랜드 인스턴스 — 강력한 격리, 테넌트당 브랜딩 및 데이터베이스, 테넌트당 고유한 노드 조인 토큰. 브랜딩은 `IOptionsMonitor`에서 읽으므로 각 인스턴스가 자체 ID를 전달합니다. → [멀티 테넌트 브랜딩](./white-label-for-business.md#multi-tenant-per-customer-branding) · [노드 검색](./operations/node-discovery.md)
- **공유 컨트롤 플레인 (고급).** 많은 인스턴스를 자체 프로비저닝 레이어에서 구동하여 프로그래밍 방식으로 테넌트별로 브랜딩 및 기능을 시딩합니다.

## 청구를 위한 사용량 미터링

소유자/관리자 전용 **`GET /api/usage`** 엔드포인트는 공급자가 폴링하여 청구를驱动할 수 있는 읽기 전용 요약을 반환합니다 — 새로운 도메인이나 지속성 없이, 기존 상태를 проекция합니다:

```json
{
  "users": { "total": 42 },
  "nodes": { "total": 6, "online": 5 },
  "instances": { "total": 1280, "backtestsRunning": 3, "runsRunning": 11 },
  "cbots": { "total": 210 },
  "tradingAccounts": { "total": 88 }
}
```

테넌트 배포당 폴링하여 좌석 기반, 플릿 기반 또는 워크로드 기반 가격책정을 drive합니다. 세분화된 컴퓨팅 미터링을 위해 [로깅 및 관찰 가능성](./operations/logging.md)과 쌍을 이룹니다.

## 마진 예측 가능성 유지

수요에 따라 노드를 스케일하고 Postgres 티어를 공유하며 무상태 티어를 오토스케일합니다. 필요한 운영 표면은 이미 있습니다:

- [확장 및 자체 복구](./deployment/scaling.md)
- [로깅 및 관찰 가능성](./operations/logging.md)
- [백업 및 재해 복구](./operations/backup-recovery.md)

## 시작하세요

1. [클라우드 가이드](./deployment/cloud.md)에서 참조 배포를 설정합니다.
2. 테넌트별로 템플릿화합니다 (브랜딩 + 조인 토큰 + DB) 및 컴퓨팅 사용량을 bill하도록 청구 시스템을 연결합니다.
3. 나열합니다 — 이제 판매할 관리형 algo 트레이딩 플랫폼이 있습니다.

## 기여

스케일에서 cMind를 실행하면 가장 먼저 날카로운 가장자리에 도달합니다. 운영 수정 및 IaC 개선을 업스트림하면 플릿을 저렴하게 유지하는 데 도움이 됩니다 — [기여 가이드](./contributing.md)에서 시작하세요.
