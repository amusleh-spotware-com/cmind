---
description: "cMind AI는 제공자 불가지론입니다 — Anthropic, OpenAI, Azure OpenAI, Google Gemini, 그리고 로컬 모델을 포함한 모든 OpenAI 호환 엔드포인트(Ollama, LM Studio, vLLM). 제공자, 모델, 엔드포인트를 선택하세요; 모든 AI 기능은 변경되지 않은 상태로 작동합니다."
---

# AI 기능

cMind의 AI 계층은 **제공자 불가지론적**입니다. 모든 기능은 단일 제공자 중립 seam(`IAiClient.CompleteAsync`)과 통신합니다; **라우팅 클라이언트**는 활성 제공자 자격 증명을 해결하고 일치하는 와이어 어댑터로 디스패치합니다. 당신은 제공자 + 모델 + 엔드포인트를 선택합니다(그리고, 제공자가 필요하면, 키); 모든 기존 기능은 동일한 게이팅, 암호화, 복원력, 저하 상태로 작동합니다.

**배터리 포함:** **빌트인 로컬 LLM이 앱과 함께 배포되고 기본적으로 활성화됩니다**(Microsoft.ML.OnnxRuntimeGenAI, 예를 들어 Phi-3-mini) — 그래서 모든 배포는 **API 키 없고 외부 서비스 없이** 작동하는 AI를 가집니다. 화이트라벨 배포는 그것을 제거하고 사용자가 추가할 수 있는 제공자를 제한할 수 있습니다. 빌트인을 넘어, 모든 외부 제공자를 연결하세요.

지원되는 제공자:

- **빌트인 로컬 AI** (`BuiltInOnnx`) — 인프로세스 ONNX GenAI 모델, 키 없음, 배포됨 + 기본 켜짐.
- **Anthropic**(Claude — Messages API)
- **OpenAI** 그리고 **Azure OpenAI**(Chat Completions)
- **Google Gemini** (`generateContent`)
- **모든 OpenAI 호환 엔드포인트**, 포함하여 **로컬 모델**(Ollama, LM Studio, vLLM, llama.cpp `server`, LocalAI) 그리고 OpenAI 호환 클라우드(OpenRouter, Groq, Together, Mistral, DeepSeek) — 모두 하나의 OpenAI 호환 어댑터를 통해, base URL + 모델 + 키만 다릅니다.

정확히 **하나의** 제공자가 한 번에 활성화됩니다. 자격 증명은 **암호화**되어 저장됩니다(`AiProviderCredential` 애그리게이트 + `IAiProviderStore` + `ISecretProtector`, `EncryptionPurposes.AiApiKey`); 로컬 엔드포인트는 **키가 필요 없습니다**. **활성 제공자가 없으면**, 모든 기능은 비활성화된 결과를 반환하고 나머지 앱은 변경되지 않은 상태로 실행됩니다(빌드, 테스트, 플랫폼 실행에 키 필요 없음).

**Back-compat:** 기존 배포의 레거시 `App:Ai:ApiKey`(또는 old 암호화된 `ai.api_key` 설정)은 기본 활성 **Anthropic** 제공자로 자동으로 명예롭습니다 — 조치 필요 없음.

AI 미구성 → AI 페이지는 작업을 흐리고 배너 더하기 **설정 → AI**에서 제공자를 추가할 일회용 프롬프트를 보여줍니다(`AiFeatureNotice`). 상태는 `GET /api/ai/status`(`{ enabled, kind, model }`); 제공자는 관리되고(소유자 전용) `GET/PUT /api/ai/providers`, `POST /api/ai/providers/{id}/activate`, `DELETE /api/ai/providers/{id}`, 그리고 `POST /api/ai/providers/test` 연결 ping을 통해.

## 배포 기본값 vs 사용자의 자신의 제공자

AI 자격 증명은 두 가지 스코프를 가집니다:

- **배포 기본값(소유자 관리).** 소유자가 제공자를 구성합니다(또는 `App:Ai:Providers[]` / 레거시 `App:Ai:ApiKey`를 통해 배포합니다). 그것은 **모든 사용자를 위한 공유 기본값**이 됩니다 — 그래서 브로커 또는 호스팅 제공자는 **사용자별 설정 없고 사용자별 한계 없이** 모든 사용자를 위해 AI를 자금 조달할 수 있습니다. 위의 소유자 전용 `/api/ai/providers` 경로를 통해 관리됨.
- **사용자의 자신의 제공자(셀프 서비스).** 서명된 모든 사용자는 `GET/PUT /api/ai/my-providers`, `POST /api/ai/my-providers/{id}/activate`, `DELETE /api/ai/my-providers/{id}`에서 자신의 제공자를 추가할 수 있습니다. 현재일 때, 그들의 **자신의 활성 제공자는 배포 기본값을 자신의 AI 기능에 대해 재정의합니다**; 제거하는 것은 기본값으로 폴백합니다.

**해결 순서**(`AiProviderStore`, 요청 사용자당): 사용자의 자신의 활성 자격 증명 → 배포 기본값 → 레거시 config 키 → 없음(AI 비활성화). 정확히 하나의 자격 증명이 **스코프당** 활성화됩니다(per `OwnerUserId`마다 부분 고유 인덱스), 그리고 각 스코프는 독립적으로 해결되므로, 사용자가 자신의 키를 활성화하는 것은 절대 공유 기본값을 방해하지 않습니다. 백그라운드/non-Web 컨텍스트(요청 사용자 없음)는 항상 배포 기본값을 해결합니다.

## 제공자 기능 행렬

기능은 제공자별로 기본 설정되고 소유자 재정의 가능합니다. 기능이 꺼져 있을 때 기능은 **저하되고, 절대 throw하지 않습니다**: 웹 검색은 조용히 드롭됩니다; 비전은 typed capability-unsupported 실패를 반환합니다.

| 제공자 | 종류 | 기본 base URL | 키 필수 | 웹 검색 | 비전 | 노트 |
|---|---|---|---|---|---|---|
| 빌트인 로컬 AI | `BuiltInOnnx` | n/a(인프로세스) | 아니오 | ✖ | ✖ | 배포된 ONNX GenAI 모델, 기본 켜짐 |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | 예 | ✅ | ✅ | Messages API, `web_search` 도구 |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | 예 | 선택 | 선택 | Chat Completions |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | 예 | ✅ | ✅ | 배포 경로 + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | 예 | ✅ | ✅ | `generateContent`, `google_search` grounding |
| Ollama(로컬) | `OpenAiCompatible` | `http://localhost:11434/v1/` | 아니오 | ✖ | 모델 의존 | OpenAI 호환 어댑터를 통해 |
| LM Studio(로컬) | `OpenAiCompatible` | `http://localhost:1234/v1/` | 아니오 | 모델 의존 | 모델 의존 | OpenAI 호환 어댑터를 통해 |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | 당신의 제공 URL | 아니오 | ✖ | 모델 의존 | OpenAI 호환 어댑터를 통해 |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | 제공자 URL | 예 | ✖ | 모델 의존 | OpenAI 호환 어댑터를 통해 |

전체 제공자별 설정 가이드(키, URL, 모델 id, UI 단계): [AI 제공자 — 설정 카탈로그](../deployment/ai-providers.md)를 참조하세요.

## 빌트인 로컬 AI(배포됨, 기본 켜짐)

cMind는 [Microsoft.ML.OnnxRuntimeGenAI](https://onnxruntime.ai/docs/genai/)를 통해 **인프로세스에서 실행되는 실제 로컬 LLM**을 배포합니다(Phi-3-mini와 같은 compact instruct 모델). 그것은 **API 키가 필요 없고 외부 서비스가 필요 없고**, 첫 시작 시 — 제공자가 구성되지 않았고 화이트라벨 게이트가 허용할 때 — 그것은 **시드되고 자동으로 활성화되므로**, 모든 배포는 상자에서 작동하는 AI를 가집니다.

- 모델 디렉토리(`genai_config.json` + tokenizer + 가중치)는 `App:Ai:BuiltIn:ModelPath`(기본 `models/onnx`, 앱 기본 디렉토리에 대한 상대)로 구성됩니다. 모델 파일이 없을 때 제공자는 **설치 힌트를 가진 typed 실패로 저하됩니다** — 절대 throw하지 않고, 나머지 앱은 영향을 받지 않습니다.
- 그것은 모든 텍스트 AI 기능을 전원합니다. compact 모델이기 때문에, 그것은 텍스트 전용(server-side 웹 검색 또는 비전 없음) 그리고 생성은 serialised입니다(하나의 모델 인스턴스, lazy load 후 재사용).
- 모델 획득/번들: [AI 제공자 → 빌트인](../deployment/ai-providers.md#built-in-local-ai-onnx-shipped)을 참조하세요.

## 화이트라벨 제어

화이트라벨 배포는 `App:Branding`(모든 제공자 upsert에서 server-side 시행)을 통해 AI를 제한합니다:

- `AllowBuiltInAi`(기본 `true`) — **빌트인 모델**을 완전히 제거하려면 `false`로 설정하세요.
- `AllowLocalProviders`(기본 `true`) — 로컬/자체 호스팅 엔드포인트(loopback / private OpenAI 호환, 예를 들어 Ollama/LM Studio/vLLM)를 금지하려면 `false`로 설정하세요.
- `AllowedAiProviderKinds`(기본 empty = all) — 배포가 인정하는 종류만 나열하세요(예를 들어 `["Anthropic","OpenAiCompatible"]`) 사용자가 추가할 수 있는 제공자를 잠그려면.

## 확장: 향후 빌트인 모델

AI 계층은 **어댑터 기반이고 성장하도록 구축됩니다**. 각 제공자는 `AiProviderKind`로 선택된 `IAiProvider`입니다; 기능 대면 seam(`IAiClient`/`AiFeatureService`)은 절대 변경되지 않습니다. 향후 새로운 빌트인 모델 런타임 추가(다른 ONNX 모델, 다른 인프로세스 엔진, GGUF/llama.cpp in-proc, 등)는 로컬화된 변경입니다: `AiProviderKind` 추가, 하나의 `IAiProvider` 어댑터 구현, 등록, 그리고 (옵션) 기본 시딩 + 대화 옵션 배선 — 기능, 엔드포인트, MCP 도구 변경 없음. 빌트인 ONNX 제공자는 이 패턴의 참조 구현입니다.

## 기능

- **cBot 빌드** — 평문 영어 프롬프트 → **generate → build → AI-fix** 자체 수리 루프를 통해 실행 가능한 cBot(`build-strategy`), `/ai/build`에서.
- **파라미터 최적화** — 폐쇄 루프: AI는 param 세트를 제안하고, 각각은 지속되고 노드 전체에서 백테스트됩니다(`optimize-run` / `optimize-params`).
- **자율 포트폴리오 에이전트** — mandate 구동 제안과 전체 결정 저널(`AgentMandate` → `AgentProposal`).
- **작동 위험 가드** — `AiRiskGuard` 백그라운드 서비스는 실행 중인 봇을 평가하고, 임계 위험에서 **자동 중지**할 수 있습니다(선택 사항).
- **Prop-firm 노출 보호자** — drawdown/노출 한계 그리고 자동 평탄화.
- **시장 경고** — `AlertRule` 엔진 그리고 AI sentiment(제공자가 지원할 때 웹 검색으로 grounded).
- **분석** — cBot 검토, 백테스트 분석, 사후 검토, 시장 sentiment, 차트 비전 설계, 마켓플레이스 큐레이션.

## 표면

- `/api/ai/*`(build-strategy, generate-project, review, analyze-backtest, optimize-params, optimize-run, post-mortem, sentiment, vision, curate, …) 아래의 웹 엔드포인트.
- MCP 도구(`AiTools`) AI 클라이언트용 — [mcp.md](mcp.md)를 참조하세요. 제공자 선택은 MCP 클라이언트에 투명합니다.
- **AI** nav 그룹 — 기능당 하나의 Blazor **페이지**: cBot 빌드(`/ai/build`), 검토(`/ai/review`), 토론(`/ai/debate`), 시장 Sentiment(`/ai/sentiment`), 노출 확인(`/ai/exposure`), 포트폴리오 다이제스트(`/ai/digest`), 튜닝 어드바이저(`/ai/tune`), 최적화(`/ai/optimize`), 그리고 포트폴리오 에이전트, 경고, MCP 키. 페이지는 `AiFeaturePageBase` + `AiOutputPanel`를 공유합니다; 각각은 제공자가 구성되지 않았을 때 `AiFeatureNotice`를 보여줍니다.
- **설정 → AI** (`/settings/ai`, 소유자 전용) — **Add / edit provider dialog**(종류, base URL과 per-kind 힌트 포함 incl. Ollama/LM Studio localhost 프리셋, 모델, 옵션 키, 기능 토글, "set active") 그리고 **Test connection** 버튼을 가진 제공자 리스트.

## 구성

`App:Ai`는 레거시 단일 키와 멀티 제공자 시딩을 모두 지원합니다:

- 레거시: `ApiKey`, `Model`(기본 `claude-opus-4-8`), `BaseUrl`, `MaxTokens` — 여전히 기본 Anthropic 제공자로 명예로움.
- 멀티 제공자: `ActiveProvider`(종류) 그리고 `Providers[]`(`{ Kind, BaseUrl, Model, ApiKey?, MaxTokens?, Capabilities? }`) — 자격 증명이 아직 없으면 시작 시 저장소에 임포트되므로, ops 팀은 appsettings/env를 통해 순전히 구성된(incl. local-LLM) 배포를 배포할 수 있습니다.

`RiskGuardEnabled`, `RiskGuardAutoStop`, `RiskGuardInterval` 변경 없음. 테스트/dev의 경우, config 키는 `Ai` 아래의 통합 [dev-credentials 파일](../testing/dev-credentials.md)에 있습니다.

## 신뢰성

제공자는 신뢰할 수 없는 것으로 취급됩니다 — 그것이 하는 아무것도 앱을 떨어뜨릴 수 없습니다. 이것은 클라우드와 로컬 엔드포인트에 대해 동일하게 유지됩니다(죽은 Ollama는 throttled Anthropic처럼 정확히 재시도한 후 저하됩니다):

- **우아한 저하.** 모든 실패 모드(제공자 없음, HTTP 4xx/5xx/429, 타임아웃, malformed body, 빈 콘텐츠, unsupported capability)는 typed `AiResult.Fail(reason)`을 반환합니다 — 클라이언트는 절대 페이지, MCP 도구, hosted service로 throw하지 않습니다.
- **복원력 파이프라인.** `AddAiHttpClient`는 공유된 AI `HttpClient`를 transient 5xx / 네트워크 실패에 제한된 재시도(exponential backoff + jitter) 그리고 generous per-attempt 그리고 total 타임아웃(`AiHttp`)으로 제공하고, 모든 어댑터에서 재사용됩니다.

## fake 로컬 LLM으로 테스팅

AI 계층은 `FakeLocalLlmServer`에 의해 **외부 의존성 없이** 엔드 투 엔드로 증명됩니다 — 결정론적 canned 회신을 반환하는 tiny in-process **OpenAI 호환** 엔드포인트, Ollama/LM Studio/vLLM과 와이어 동일. 그것은 백업:

- **Unit** — per-adapter 요청 번역 + 응답 파싱 테스트, 라우팅/기능 저하.
- **Integration** — OpenAI 호환 어댑터 엔드 투 엔드, 모든 어댑터를 통한 parameterized 복원력 이론, **MCP AI 도구**.
- **E2E** — `AiLocalFixture`는 fake 서버를 가리킨 앱을 부팅합니다(또는 개발자가 `AI_E2E_BASEURL`(+ 옵션 `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`)를 설정할 때 **실제** 제공자 — 실제 자격 증명 승리) 그리고 실제 UI를 통해 모든 AI 기능을 구동합니다. 모든 AI 기능을 추가하거나 변경하는 것은 **이 픽스를 통한 E2E 테스트가 필요합니다**(repo 테스트 mandate를 참조). 선택 사항 레인(`AI_LOCAL_LLM=1`)은 하나의 실제 완료를 **Ollama** Testcontainer를 통해 실행합니다.
