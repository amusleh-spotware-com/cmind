---
description: "cMind AI는 공급자 비종속 — Anthropic, OpenAI, Azure OpenAI, Google Gemini 및 로컬 모델(Ollama, LM Studio, vLLM)을 포함한 모든 OpenAI 호환 엔드포인트를 지원합니다. 공급자와 모델, 엔드포인트를 선택하면 모든 AI 기능이 동일한 게이팅, 암호화, 회복력, 저하로 변경 없이 작동합니다."
---

# AI 기능

cMind의 AI 레이어는 **공급자 비종속**입니다. 모든 기능은 단일 공급자 중립적 Seam(`IAiClient.CompleteAsync`)과 통신합니다; **라우팅 클라이언트**가 활성 공급자 자격 증명을 해결하고 일치하는 와이어 어댑터로 디스패치합니다. 공급자 + 모델 + 엔드포인트를 선택하면(공급자에 따라 키 필요), 모든 기존 기능이 동일한 게이팅, 암호화, 회복력, 저하로 변경 없이 작동합니다.

**기본 포함:** 앱과 함께 제공되고 **기본적으로 활성화된** **기본 제공 로컬 LLM**이 제공됩니다(Microsoft.ML.OnnxRuntimeGenAI, 예: Phi-3.5-mini) — 따라서 모든 배포가 **API 키와 외부 서비스 없이** 작동하는 AI를 갖습니다. 화이트레이블 배포는 이를 제거하고 사용자가 추가할 수 있는 공급자를 제한할 수 있습니다. 기본 제공 외에 외부 공급자를 연결하세요.

지원 공급자:

- **기본 제공 로컬 AI** (`BuiltInOnnx`) — 인프로세스 ONNX GenAI 모델, 키 불필요, 제공 + 기본 활성화.
- **Anthropic** (Claude — Messages API)
- **OpenAI** 및 **Azure OpenAI** (Chat Completions)
- **Google Gemini** (`generateContent`)
- **OpenAI 호환 엔드포인트** — 로컬 모델(Ollama, LM Studio, vLLM, llama.cpp `server`, LocalAI) 및 OpenAI 호환 클라우드(OpenRouter, Groq, Together, Mistral, DeepSeek) 포함 — 모두 하나의 OpenAI 호환 어댑터를 통해, 기본 URL + 모델 + 키만 다름.

**정확히 하나**의 공급자가 한 번에 활성 상태입니다. 자격 증명은 **암호화되어 저장**됩니다(`AiProviderCredential` aggregate + `IAiProviderStore` + `ISecretProtector`, `EncryptionPurposes.AiApiKey`); 로컬 엔드포인트는 **키 불필요**. 활성 공급자가 **없으면** 모든 기능은 비활성화 결과를 반환하고 앱의 나머지 부분은 변경 없이 실행됩니다(플랫폼 빌드, 테스트, 실행에 키 불필요).

**하위 호환:** 기존 배포의 레거시 `App:Ai:ApiKey`(또는 이전 암호화된 `ai.api_key` 설정)는 기본 활성 **Anthropic** 공급자로 자동으로 인식됩니다 — 필요한 작업 없음.

AI 미구성 → AI 페이지는 작업을 흐리게 하고 배너와 함께 **Settings → AI**에서 공급자를 추가하도록 일회성 안내를 표시합니다. `GET /api/ai/status`에서 상태 확인(`{ enabled, kind, model }`); 공급자 관리는 오직 소유자만 가능(`GET/PUT /api/ai/providers`, `POST /api/ai/providers/{id}/activate`, `DELETE /api/ai/providers/{id}`, `POST /api/ai/providers/test` 연결 핑 포함).

## 배포 기본값 vs 사용자의 자체 공급자

AI 자격 증명은 두 범위가 있습니다:

- **배포 기본값(소유자 관리).** 소유자가 공급자를 구성(또는 `App:Ai:Providers[]` / 레거시 `App:Ai:ApiKey`로 제공)합니다. 이는 **모든 사용자의 공유 기본값**이 됩니다 — 중개소나 호스팅 제공자가 **사용자별 설정 및 사용자별 제한 없이** 모든 사용자를 위한 AI를 제공할 수 있습니다. 소유자 전용 `/api/ai/providers` 경로를 통해 관리.
- **사용자의 자체 공급자(셀프 서비스).** 로그인한 사용자는 `GET/PUT /api/ai/my-providers`, `POST /api/ai/my-providers/{id}/activate`, `DELETE /api/ai/my-providers/{id}`에서 자신의 공급자를 추가할 수 있습니다. 존재할 경우 **자신의 활성 공급자가 자신의 AI 기능에 대한 배포 기본값을 덮어씁니다**; 제거하면 기본값으로 돌아갑니다.

**해석 순서**(`AiProviderStore`, 요청 사용자별): 사용자의 자체 활성 자격 증명 → 배포 기본값 → 레거시 구성 키 → 없음(AI 비활성화). 범위당 정확히 하나의 자격 증명이 활성 상태이고(`OwnerUserId`별 부분 고유 인덱스), 각 범위가 독립적으로 해석되므로 사용자가 자신의 키를 활성화해도 공유 기본값이 방해받지 않습니다. 배경/비-Web 컨텍스트(요청 사용자 없음)는 항상 배포 기본값을 확인합니다.

## 공급자 기능 매트릭스

기능은 공급자별 기본값이 있으며 소유자가 덮어쓸 수 있습니다. 기능이 꺼지면 **저하, 절대 예외 없음**: 웹 검색은 자동으로 삭제됨; 비전은 유형화된 기능 미지원 실패를 반환합니다.

| 공급자 | 종류 | 기본 기본 URL | 키 필요 | 웹 검색 | 비전 | 참고 |
|---|---|---|---|---|---|---|
| 기본 제공 로컬 AI | `BuiltInOnnx` | n/a (인프로세스) | 아니오 | ✖ | ✖ | 제공되는 ONNX GenAI 모델, 기본 활성화 |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | 예 | ✅ | ✅ | Messages API, `web_search` 도구 |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | 예 | 선택 | 선택 | Chat Completions |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | 예 | ✅ | ✅ | 배포 경로 + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | 예 | ✅ | ✅ | `generateContent`, `google_search` 그라운딩 |
| Ollama (로컬) | `OpenAiCompatible` | `http://localhost:11434/v1/` | 아니오 | ✖ | 모델 의존 | OpenAI 호환 어댑터를 통해 |
| LM Studio (로컬) | `OpenAiCompatible` | `http://localhost:1234/v1/` | 아니오 | 모델 의존 | 모델 의존 | OpenAI 호환 어댑터를 통해 |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | 제공된 URL | 아니오 | ✖ | 모델 의존 | OpenAI 호환 어댑터를 통해 |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | 공급자 URL | 예 | ✖ | 모델 의존 | OpenAI 호환 어댑터를 통해 |

공급자별 설정 가이드(키, URL, 모델 ID, UI 단계): [AI providers — setup catalog](../deployment/ai-providers.md) 참조.

## 기본 제공 로컬 AI (제공, 기본 활성화)

cMind는 [Microsoft.ML.OnnxRuntimeGenAI](https://onnxruntime.ai/docs/genai/)를 통해 **인프로세스에서 실행되는 실제 로컬 LLM**을 제공합니다(Phi-3.5-mini와 같은 소형 명령 모델). **API 키와 외부 서비스가 필요하지 않으며**, 첫 시작 시 — 공급자가 구성되지 않고 화이트레이블 게이트가 허용하는 경우 — **자동으로 시드 및 활성화**되어 모든 배포가 즉시 작동하는 AI를 갖습니다.

- 모델 디렉토리(`genai_config.json` + 토크나이저 + 가중치)는 `App:Ai:BuiltIn:ModelPath`(기본값 `models/onnx`, 앱 기본 디렉토리 기준)로 구성됩니다. 모델 파일이 없으면 공급자는 설치 힌트와 함께 유형화된 실패로 **저하**됩니다 — 예외를 던지지 않으며 앱의 나머지 부분에 영향 없습니다.
- 모든 텍스트 AI 기능을 구동합니다. 소형 모델이므로 텍스트 전용(서버측 웹 검색 또는 비전 없음)이며 생성은 직렬화됩니다(하나의 모델 인스턴스, 지연 로드 후 재사용).
- **여러 기본 제공 모델이 공존할 수 있습니다.** 각 다운로드된 모델은 `ModelPath/<key>` 아래에 있으며, 큐레이트된 카탈로그(기본값 Phi-3.5-mini, Phi-3-mini-128k 포함)는 **Settings → AI**에서 다운로드하고 전환할 수 있습니다. 기본 제공 부모 모델 선택은 인프로세스에서 로드합니다. 모델 획득/번들: [AI providers → built-in](../deployment/ai-providers.md#built-in-local-ai-onnx-shipped) 참조.

## 화이트레이블 컨트롤

화이트레이블 배포는 `App:Branding`(모든 공급자 업서트 시 서버측 적용)을 통해 AI를 제한합니다:

- `AllowBuiltInAi`(기본값 `true`) — `false`로 설정하면 **기본 제공 모델을 완전히 제거**합니다.
- `AllowLocalProviders`(기본값 `true`) — `false`로 설정하면 로컬/자체 호스팅 엔드포인트(루프백/비공개 OpenAI 호환, 예: Ollama/LM Studio/vLLM)를 금지합니다.
- `AllowedAiProviderKinds`(기본값 빈 = 모두) — 배포에서 승인한 종류만 나열(예: `["Anthropic","OpenAiCompatible"]`)하여 사용자가 추가할 수 있는 공급자를 제한합니다.
- `AllowAiModelManagement`(기본값 `true`) — `false`로 설정하면 **모델 탐색**, **페이지별 모델 선택기**, **기능별 모델 바인딩**을 숨깁니다. 모두 소유자가 **Settings → Deployment**에서 런타임에 조정 가능하며(`IOptionsMonitor`에 라이브 오버레이) `WhiteLabelCatalog`에 카탈로그됩니다.

## 확장: 향후 기본 제공 모델

AI 레이어는 **어댑터 기반이며 확장 가능**합니다. 각 공급자는 `AiProviderKind`로 선택되는 `IAiProvider`이며, 기능 대면 Seam(`IAiClient`/`AiFeatureService`)은 변경되지 않습니다. 나중에 새 기본 제공 모델 런타임 추가(다른 ONNX 모델, 다른 인프로세스 엔진, GGUF/llama.cpp 인프로세스 등)는 로컬화된 변경 사항입니다: `AiProviderKind` 추가, 하나의 `IAiProvider` 어댑터 구현, 등록, (선택 사항으로) 기본 시딩 + 대화상자 옵션 연결 — 기능, 엔드포인트 또는 MCP 도구 변경 불필요. 기본 제공 ONNX 공급자는 이 패턴의 참조 구현입니다.

## 기능

- **cBot 빌드** — 일반 영어 프롬프트 → **생성 → 빌드 → AI 수정** 자체 복구 루프를 통해 실행 가능한 cBot(`build-strategy`), `/ai/build`에서. **생성된 소스 코드는 빌드 완료 시에 표시**됩니다(복사 버튼 포함). 빌드 로그와 함께 — 성공 시와 실패 시 모두 — AI가 작성한 내용을 항상 확인할 수 있으며, 오류만 표시되지 않습니다.
- **페이지별 모델 선택** — 모든 AI 기능 페이지 및 대화상자에는 사용할 수 있는 모델(자신의 공급자 + 배포 기본값)을 나열하는 **모델 선택기**가 표시됩니다. 설정된 경우 기능의 저장된 바인딩을 미리 선택하고, 그렇지 않으면 **기본** 모델을 선택하며, 선택한 모델은 해당 작업 하나에 적용됩니다(`?modelId=`로 전송되고 `RoutingAiClient`에 의해 해당 호출에 강제됨). 배포에서 모델 관리를 비활성화하면 숨겨집니다.
- **모델 탐색 및 기능별 선택** — 공급자 엔드포인트가 광고하는 모델 탐색(`GET /v1/models` on LM Studio / Ollama / vLLM / llama.cpp 또는 기본 제공 카탈로그) 대신 손으로 ID를 입력하고, **각 AI 기능을 다른 모델에 바인딩**하여 여러 모델이 동시에 다른 기능을 제공합니다(바인딩되지 않은 기능은 범위의 기본 공급자로 폴백).
- **파라미터 최적화** — 폐쇄 루프: AI가 파라미터 세트 제안, 각각 지속됨 + 노드에서 백테스트(`optimize-run` / `optimize-params`).
- **자율적 포트폴리오 에이전트** — 명령 기반 제안, 완전한 결정 저널 포함(`AgentMandate` → `AgentProposal`).
- **실행 중 리스크 가드** — `AiRiskGuard` 백그라운드 서비스가 실행 중인 봇을 평가하고 중요한 리스크 시 **자동 중지** 가능(옵션).
- **Prop-firm 노출 가디언** — 드로우다운/노출 제한 및 자동 플래튼.
- **시장 알림** — AI 감성(공급자가 지원 시 웹 검색 그라운딩)이 포함된 `AlertRule` 엔진.
- **분석** — cBot 리뷰, 백테스트 분석, 사후 분석, 시장 심리, 차트 비전 설계, 마켓플레이스 큐레이션.

## 노출

- `/api/ai/*` 아래의 Web 엔드포인트(build-strategy, generate-project, review, analyze-backtest, optimize-params, optimize-run, post-mortem, sentiment, vision, curate, …). 모든 기능 엔드포인트는 선택적 `?modelId=<credential>`을 수용하여 선택한 모델에서 해당 하나의 호출을 실행합니다. 또한 **모델 검색**(`/api/ai/models/probe`, `/api/ai/usable-models`) 및 **기능별 바인딩**(`/api/ai/feature-bindings`, `/api/ai/my-feature-bindings`).
- MCP 도구(`AiTools`) for AI 클라이언트 — [mcp.md](mcp.md) 참조. 공급자 선택은 MCP 클라이언트에 투명합니다.
- **AI** 내비게이션 그룹 — 기능당 하나의 Blazor **페이지**: Build cBot(`/ai/build`), Review(`/ai/review`), Debate(`/ai/debate`), Market Sentiment(`/ai/sentiment`), Exposure Check(`/ai/exposure`), Portfolio Digest(`/ai/digest`), Tune Advisor(`/ai/tune`), Optimize(`/ai/optimize`), Portfolio Agent, Alerts, MCP Keys 포함. 페이지는 `AiFeaturePageBase` + `AiOutputPanel` + `AiModelSelect`를 공유합니다; 공급자가 구성되지 않으면 각 페이지에 `AiFeatureNotice` 표시합니다.
- **Settings → AI** (`/settings/ai`, 소유자 전용) — 공급자 목록과 **공급자 추가/편집 대화상자**(종류, 종류별 힌트 포함 기본 URL, Ollama/LM Studio localhost 프리셋 포함, 모델, 선택적 키, 기능 토글, "기본값 설정") 및 **연결 테스트** 버튼.

## 구성

`App:Ai`는 레거시 단일 키와 다중 공급자 시딩을 모두 지원합니다:

- 레거시: `ApiKey`, `Model`(기본값 `claude-opus-4-8`), `BaseUrl`, `MaxTokens` — 기본 Anthropic 공급자로 여전히 인식됩니다.
- 다중 공급자: `ActiveProvider`(종류) 및 `Providers[]`(`{ Kind, BaseUrl, Model, ApiKey?, MaxTokens?, Capabilities? }`) — 자격 증명이 아직 존재하지 않으면 시작 시 저장소로 가져오므로 ops 팀이 appsettings/env를 통해 (로컬 LLM 포함) 구성된 배포를 제공할 수 있습니다.

`RiskGuardEnabled`, `RiskGuardAutoStop`, `RiskGuardInterval` 변경 없음. 테스트/개발용 구성 키는 `Ai` 아래 통합 [dev-credentials file](../testing/dev-credentials.md)에 있습니다.

## 안정성

공급자는 신뢰할 수 없는 것으로 취급됩니다 — 공급자가 하는 어떤 일도 앱을 다운시킬 수 없습니다. 이는 클라우드 및 로컬 엔드포인트에 동일하게 적용됩니다(사망한 Ollama는 스로틀된 Anthropic과 정확히 동일하게 재시도 후 저하됩니다):

- **우아한 저하.** 모든 실패 모드(공급자 없음, HTTP 4xx/5xx/429, 시간 초과, 형식 부정확 본문, 빈 콘텐츠, 지원되지 않는 기능)는 유형화된 `AiResult.Fail(reason)`을 반환합니다 — 클라이언트가 페이지, MCP 도구 또는 호스티드 서비스로 예외를 던지지 않습니다.
- **회복력 파이프라인.** `AddAiHttpClient`는 하나 공유 AI `HttpClient`에 일시적 5xx / 네트워크 오류에 대한 제한된 재시도(지수 백오프 + 지터)와 각 시도 및 총 시간 제한(`AiHttp`)을 부여하며, 모든 어댑터에서 재사용됩니다.

## 가짜 로컬 LLM으로 테스트

AI 레이어는 `FakeLocalLlmServer`로 **외부 종속성 없이 종단 간 검증**됩니다 — Ollama/LM Studio/vLLM과 와이어 동일한 결정론적 캐닝 회신을 반환하는 소형 인프로세스 **OpenAI 호환** 엔드포인트. 다음을 지원합니다:

- **단위** — 어댑터별 요청 번역 + 응답 파싱 테스트, 라우팅/기능 저하.
- **통합** — OpenAI 호환 어댑터 종단 간, 모든 어댑터에 대한 매개변화된 회복력 이론, **MCP AI 도구**.
- **E2E** — `AiLocalFixture`가 가짜 서버(또는 개발자가 `AI_E2E_BASEURL` + 선택적 `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL` 설정 시 **실제** 공급자 — 실제 키 우선)를 가리키는 앱을 부팅하고 실제 UI를 통해 모든 AI 기능을 실행합니다. AI 기능 추가 또는 변경은 이 픽스처를 통해 **E2E 테스트가 필요합니다**( repo 테스트 의무 참조). 선택적 레인(`AI_LOCAL_LLM=1`)은 **Ollama** Testcontainer를 통해 하나의 실제 완료를 실행합니다.

## 기본 제공 로컬 AI — 기본값 제로 설정

기본 제공 ONNX 로컬 LLM은 즉시 작동합니다: 모델 디렉토리가 없고 `App:Ai:BuiltIn:AutoDownload`가 `true`(기본값)인 경우 앱이 백그라운드에서 한 번 모델을 다운로드합니다(`App:Ai:BuiltIn:DownloadBaseUrl`에서). 다운로드가 진행되는 동안 AI 호출(및 **Settings → AI**의 **연결 테스트**)은 경고 대신 "모델 다운로드 중(첫 번째 설정)" 메시지를 반환합니다. 에어갭/유료 배포는 `AutoDownload=false`로 설정하고 모델 디렉토리를 사전 프로비저닝합니다(`App:Ai:BuiltIn:ModelPath`). 화이트레이블 `App:Branding:AllowBuiltInAi` 게이트가 계속 적용됩니다.

다운로드는 **기본 제공 모델이 활성 공급자일 때 시작 시 사전 준비**되므로, 첫 AI 클릭 시 "다운로드 중…"으로 실패하는 대신 준비 완료 상태입니다. **Settings → AI**는 기본 제공 공급자 카드에 라이브 설치 상태를 표시합니다 — *모델 준비 완료* / *모델 다운로드 중…* / *모델 설치 안 됨* / *다운로드 실패* — 요청 시 일회성 백그라운드 페치를 시작하는 **모델 다운로드**(또는 **다운로드 재시도**) 버튼 포함(`GET /api/ai/built-in/status`, `POST /api/ai/built-in/install`). Settings에서 기본 제공 공급자를 활성화하면 이미 시딩된 행을 재사용하므로 중복이 추가되지 않아 단일 활성 공급자 제약 조건과 충돌하지 않습니다.
