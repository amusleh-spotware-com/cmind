---
title: 0005 — AI 클라이언트는 raw HTTP를 사용합니다, Anthropic SDK가 아닙니다
description: IAiClient가 왜 공식 SDK가 아닌 typed HttpClient를 통해 Anthropic API를 호출하는지, 그리고 왜 AI가 완전히 키에 게이트되는지.
---

# 0005 — AI 클라이언트는 raw HTTP를 사용합니다, Anthropic SDK가 아닙니다

## 컨텍스트

모든 AI 기능(전략 생성, 자체 수리, 위험 가드, 사후 검토)은 Anthropic API를 호출합니다. SDK 의존성은 우리가 제어하지 않는 transitive 표면을 추가하고, 우리의 릴리스 cadence를 그들의 것과 커플합니다, 그리고 복원력과 비용에 대해 추론해야 하는 정확한 와이어 계약을 숨깁니다.

## 결정

`IAiClient`는 **raw HTTP를 통해** typed `HttpClient`를 통해 Anthropic을 호출합니다 — 의도적으로 **SDK가 아닙니다**. `AiFeatureService`는 Web 엔드포인트, MCP `AiTools`, `AiRiskGuard`를 통해 공유되는 단일 오케스트레이터입니다. 전체 표면은 **`AppOptions.Ai.ApiKey`에 게이트됩니다**: 키가 없으면, 모든 기능은 `AiResult.Fail`을 반환하고 앱은 변경되지 않습니다.

## 결과

- 키는 빌드, 테스트, 또는 E2E에 필요하지 않습니다 — CI 및 로컬 dev는 AI 없이 전체 앱을 실행합니다.
- 우리는 요청/응답 형태, 재시도/타임아웃 정책, 토큰 회계를 명시적으로 소유합니다.
- 새로운 Anthropic 기능은 손으로 배선되어야 합니다; 우리는 편의를 제어 및 더 작은 의존성 표면을 위해 거래합니다. 현재 모델 id 및 파라미터에 대해 `claude-api` 레퍼런스를 참조하세요.
