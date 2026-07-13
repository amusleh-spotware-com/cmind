---
slug: /features
title: 기능 — 전체 투어
description: cMind가 할 수 있는 모든 것 — 복사 거래, AI, 빌드 & 백테스트, prop-firm 가드, 화이트라벨, PWA, MCP, 및 더.
sidebar_label: 개요
---

# 기능 — 전체 투어 🧭

큰 투어에 오신 것을 환영합니다. cMind는 한 개의 앱에 *많은* 것을 채우므로, 여기는 맵입니다. 각 기능은 자신의 깊은 사이 문서를 가집니다 — 당신이 긁는 모든 것을 클릭하세요.

## 🔁 복사 거래

왕관 보석. 마스터 계정을 많은 곳으로 미러링하고, 인터넷이 misbehave할 때에도 그들을 동기 유지합니다.

- **[복사 거래](./copy-trading.md)** — 핵심: 미러링, 주문 타입, SL/TP, 슬리피지, desync/resync.
- **[실행 투명성](./copy-execution-transparency.md)** — 정확히 무엇이 복사되었는지, 언제, 그리고 왜인지 보세요.
- **[성과 수수료](./copy-performance-fees.md)** — 당신의 신호에 대해 비용을 청구하세요, high-water-mark 스타일.
- **[제공자 마켓플레이스](./copy-provider-marketplace.md)** — 트레이더가 제공자를 발견하고 따르도록 하세요.
- **[알림](./copy-notifications.md)** — 무언가가 당신이 필요할 때 당신에게 말해지세요.
- **[AI 복사 추천자](./ai-copy-recommender.md)** — AI가 누구를 복사할지 제안하도록 하세요.
- **[Open API 토큰 라이프사이클](./token-lifecycle.md)** — cMind가 어떻게 cID당 정확히 하나의 유효한 토큰을 유지하는지.

## 📊 당신의 홈 베이스

- **[대시보드](./dashboard.md)** — 실시간, 모바일 우선 명령 센터: sparklines를 가진 KPI, 활동 차트, 상태 링, 실시간 피드, 그리고(관리자용) 클러스터 건강. 그것은 자신을 새로 고칩니다.

## 🧠 AI 코어

측면에 bolted된 채팅 박스가 아닙니다 — 실제로 *일을 하는* AI.

- **[AI 어시스턴트, 에이전트, 위험 가드 & 경고](./ai.md)** — 전략 생성, 자체 수리 빌드, 봇을 자동 중지할 수 있는 백그라운드 위험 가드, 스마트 경고.

## 🛠️ 빌드 & 실행

- **[cBot 빌드 & 백테스트](./build-and-backtest.md)** — 브라우저 내 Monaco IDE, C#/Python 템플릿, 샌드박싱된 빌드, 실시간 자산 곡선.
- **[MCP 서버](./mcp.md)** — HTTP + SSE를 통해 cMind의 도구를 노출하므로 AI 클라이언트가 그것을 구동할 수 있습니다.

## 🏢 비즈니스로 실행합니다

- **[화이트라벨 / 브랜딩](./white-label.md)** — config를 통해 모든 표면을 리브랜드하세요.
- **[Prop-firm 도전 시뮬레이션](./prop-firm.md)** — 실시간 자산을 사용하여 일일 손실, 드로우다운, 목표 규칙을 시행합니다.
- **[기능 토글](./feature-toggles.md)** — 각 배포/테넌트가 무엇을 볼지 결정합니다.
- **[준수 / 법적](./compliance.md)** — 감사 증적 및 법적 표면.

## 📱 경험

- **[설치 가능한 앱(PWA)](./pwa.md)** — 모바일 우선, 오프라인 셸, 홈 화면에 추가.
- **[UI 설계 시스템 & 모바일 우선](../ui-guidelines.md)** — 모양 뒤의 설계 토큰 및 규칙.

## ⚙️ 후드 아래

모든 것을 실행하는 운영 비트:

- **[노드 플릿 & 디스커버리](../operations/node-discovery.md)** — 노드가 어떻게 자체 등록되고 치유하는지.
- **[수평 스케일링](../deployment/scaling.md)** — 레플리카 추가, 외부 조정자 필요 없음.
- **[로깅 & 감사](../operations/logging.md)** — 구조화된 로그 + OpenTelemetry.
- **[배포](../deployment/local.md)** — 어디든지 실행하게 하세요.

:::note 문서를 정직하게 유지하기
모든 기능 문서는 코드와 lockstep으로 유지됩니다 — 행동 변경, 문서 업데이트, 동일한 커밋. 당신이 드리프트를 발견하면, 그것은 버그입니다: 당신은 [이슈를 열거나](https://github.com/amusleh-spotware-com/cmind/issues/new/choose) PR을 전송하세요. 🙏
:::
