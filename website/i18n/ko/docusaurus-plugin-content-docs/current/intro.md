---
slug: /intro
title: cMind에 오신 것을 환영합니다
description: cMind에 대한 친절한 소개 — cTrader를 위한 오픈소스이자 자체 호스팅 가능한 트레이딩 운영 플랫폼.
sidebar_position: 1
---

# cMind에 오신 것을 환영합니다 👋

:::warning[알파 소프트웨어 — 프로덕션 사용 불가]
cMind는 현재 활발히 개발 중입니다. 버전 간 호환성 변경, 개발 중인 기능, 그리고 크고 작은 문제들이 존재할 수 있습니다. **커뮤니티 테스터, 버그 제보자, 그리고 초기 기여자**가 필요합니다 — 함께 만들어 나갑시다. 문제가 생기면 [여기에 제보해 주세요](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) — 실제 사용자의 피드백이 지금 가장 소중한 기여입니다.
:::

그러니까 트레이딩 봇을 만들고, 노트북을 녹이지 않고 백테스트하고, 여러 대의 머신에서 실행하고, 거래를
수십 개의 계정에 미러링하며, 잠자는 동안 AI가 리스크를 지켜보게 하고 싶으신 거군요. **바로 제대로 찾아
오셨습니다.**

cMind는 **cTrader를 위한 오픈소스이자 자체 호스팅 가능한 트레이딩 운영 플랫폼**입니다. 작성, 실행, 컴퓨트
플릿, 카피 트레이딩, 그리고 AI 코어 — 즉 당신의 트레이딩 데스크 전체를, 차분하고 어둡고 모바일 친화적인
하나의 앱에 담아, 처음부터 끝까지 당신이 소유합니다.

:::tip[한 문장으로]
자체 서버에서, 자체 브랜드로, AI를 내장한 채 cTrader 전략을 대규모로 구축 → 백테스트 → 실행 → 복제하세요.
:::

## 실제로 무엇을 할 수 있나요?

| 하고 싶은 것 | cMind가 해내는 것 | 더 보기 |
|---|---|---|
| 브라우저에서 cBot 작성 | Monaco IDE + C#/Python 템플릿, 샌드박스 빌드 | [빌드 및 백테스트](./features/build-and-backtest.md) |
| 여러 머신에 걸쳐 백테스트 | 자가 복구 노드 플릿이 가장 한가한 머신을 선택 | [스케일링](./deployment/scaling.md) |
| 한 계정을 여러 계정으로 복사 | 재동기화를 갖춘 견고한 미러링, 중복 거래 없음 | [카피 트레이딩](./features/copy-trading.md) |
| AI에게 궂은일 맡기기 | 전략 생성, 자가 복구, 리스크 가드, 사후 분석 | [AI 코어](./features/ai.md) |
| 프롭 펌 규칙 안에 머무르기 | 실시간 자본 추적 + 챌린지 규칙 시뮬레이션 | [프롭 펌](./features/prop-firm.md) |
| 백테스트 우위 검증 | PSR / DSR / t-stat 과적합 보정 | [백테스트 무결성 랩](./features/backtest-integrity.md) |
| 자신의 매매 습관 이해 | 행동 누수 탐지 + AI 코치 | [트레이딩 저널](./features/trading-journal.md) |
| 전략에 거시 이벤트 추적 | 시점 정확 캘린더, 뉴스 차단, cBot API | [경제 달력](./features/economic-calendar.md) |
| 통화 거시 강도 점수화 | 모든 페어에 걸친 AI 포워드 전망 | [통화 강도](./features/currency-strength.md) |
| 2FA로 계정 보안 강화 | TOTP 인증 앱 + 백업 코드 | [이중 인증](./features/two-factor-auth.md) |
| 소유자가 런타임에 설정 | 모든 화이트라벨 옵션을 설정 → 배포에서 실시간 변경 | [소유자 설정](./features/white-label-owner-settings.md) |
| 어떤 언어로도 실행 | 23개 언어 (RTL 포함) — 키 누락 시 빌드 실패 | [현지화](./features/localization.md) |
| *당신의* 제품으로 출시 | 완전한 화이트라벨: 이름, 색상, 로고, 파비콘 | [화이트라벨](./features/white-label.md) |
| 휴대폰에서 실행 | 설치 가능한 모바일 우선 PWA | [PWA](./features/pwa.md) |
| AI 클라이언트에서 구동 | 내장 MCP 서버 (HTTP + SSE) | [MCP](./features/mcp.md) |

## 5분 경로 ⏱️

Docker와 5분이 있다면, 지금 바로 실제 cMind 인스턴스를 만져볼 수 있습니다:

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
cp .env.example .env        # set OWNER_EMAIL + OWNER_PASSWORD
docker compose up --build
```

그런 다음 **<http://localhost:8080>** 을 열고 로그인하면 시작입니다. (Docker가 필연적으로 고집을 부릴
때를 위한 문제 해결을 포함한) 전체 안내는 **[로컬에서 실행](./deployment/local.md)** 에 있습니다.

## 처음이신가요? 노란 벽돌길을 따라가세요 🟡

1. **[누구를 위한 것인가요?](./audience.md)** — 당신이 우리 부류의 골칫거리인지 확인하세요.
2. **[로컬에서 실행](./deployment/local.md)** — 실제 인스턴스를 띄우세요.
3. **[기능](./features/README.md)** — 내부의 모든 것을 둘러보는 전체 투어.
4. **[제대로 배포](./deployment/cloud.md)** — Docker, Kubernetes, Azure, AWS.
5. **[당신의 것으로 만들기](./white-label-for-business.md)** — 비즈니스를 위해 화이트라벨을 적용하세요.
6. **[기여하기](./contributing.md)** — PR(사람 *및* AI 지원)을 매우 환영합니다.

## 돈에 관한 짧은 한마디 💸

cMind는 **실제 자본**을 다룹니다. 우리는 이를 진지하게 여깁니다 — 모든 변경은 실패 경로(연결 끊김, 주문
거부, 노드 다운)를 포함한 단위, 통합, 엔드투엔드 테스트와 함께 제공됩니다. 당신도 진지하게 여겨야 합니다:
**먼저 데모 계정에서 테스트하고**, 실제 자금에 연결하기 전에 [컴플라이언스 참고 사항](./features/compliance.md)
을 읽으세요. 트레이딩은 위험합니다. 이 소프트웨어는 도구이지 금융 자문이 아닙니다.

자 — 서론은 이쯤 하죠. 뭔가 만들러 가봅시다. →
