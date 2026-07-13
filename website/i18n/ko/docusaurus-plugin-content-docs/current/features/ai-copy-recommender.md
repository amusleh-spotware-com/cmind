---
description: "AI 도우미. 팔로어 리스크 프로필 + 소스 (마스터) 계정 설명에서 안전한 복사 트레이딩 대상 설정을 권장합니다. REST API, MCP 도구, 복사 트레이딩 페이지에 공개됩니다."
---

# AI 복사 프로필 권장

AI 도우미. 팔로어 리스크 프로필 + 소스 (마스터) 계정 설명에서 안전한 복사 트레이딩 대상 설정을 권장합니다. REST API, MCP 도구, 복사 트레이딩 페이지에 공개됩니다. 어드바이저리만 해당 — 절대로 프로필 생성/변형 안 함; 후속 MCP 호출 또는 사람이 설정을 적용합니다.

## 모델

- `IAiFeatureService.RecommendCopyProfileAsync(riskProfile, sourceDescription, ct)` — `AiPrompts.CopyProfileSystem` 프롬프트에서 요청을 빌드하고 `AiResult`를 반환하며 텍스트 = 제안된 설정의 JSON 객체: `riskMode` (`MoneyManagementMode` 이름), `riskParameter`, `maxDrawdownPercent`, `dailyLossLimit`, `direction`, `copyStopLoss`, `copyTakeProfit`, `slippagePips`, 짧은 `rationale`.
- 모든 AI 기능과 마찬가지로 `App:Ai:ApiKey`에 게이트됩니다: 키 없음 → 호출이 `AiResult.Fail(disabled)`를 반환하고 앱은 영향받지 않습니다.

## 표면

| 표면 | 진입점 |
|---------|---------|
| REST | `POST /api/ai/recommend-copy-profile` `{ riskProfile, sourceDescription }` → `AiResult` (기능 `Ai`, 역할 User+) |
| MCP | `CopyTools.RecommendCopyProfile(riskProfile, sourceDescription)` (기능 `CopyTrading`, AI 서비스에 위임) |
| UI | 복사 트레이딩 페이지 → **AI 권장** 버튼; 권장이 인라인 경고에 렌더링됩니다 |

권장이 의도적으로 자동 적용되지 않음: 팔로어가 검토한 다음 일반 복사 트레이딩 대화상자 (또는 MCP 클라이언트가 JSON 파싱 + 생성 엔드포인트 호출)로 프로필 / 대상을 생성합니다.

## 테스트

- **단위** — `UnitTests/Ai/AiFeatureServiceRecommendTests.cs`: 리스크 프로필 + 소스 설명이 복사 프로필 시스템 프롬프트로 AI 클라이언트에 전달됩니다 (NSubstitute).
- **통합** — `IntegrationTests/AiRecommendDisabledTests.cs`: API 키 없음 → 실제 `AnthropicAiClient` + `AiFeatureService`가 실패 결과로 Degradation합니다 (키 없이 앱 실행).
- **E2E** — `E2ETests/AiCopyRecommendTests.cs`: **AI 권장** 버튼이 엔드포인트를 호출 + 결과를 렌더링합니다 (테스트 환경에서 "구성되지 않음" 메시지 우아하게 표시), UI → 엔드포인트 → AI 경로 입증.
