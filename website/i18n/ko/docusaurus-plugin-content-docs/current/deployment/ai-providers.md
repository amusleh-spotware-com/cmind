---
description: "cMind이 지원하는 모든 AI 공급자 설정 카탈로그 — Anthropic, OpenAI, Azure OpenAI, Google Gemini, 그리고 Ollama, LM Studio, vLLM, llama.cpp, LocalAI를 포함한 모든 OpenAI 호환 엔드포인트 및 로컬 모델"
---

# AI 공급자 — 설정 카탈로그

cMind의 AI 계층은 공급자 독립적입니다 ([AI 기능](../features/ai.md) 참조). 두 가지 방법으로 공급자를 구성하세요:

1. **UI (소유자):** 설정 → AI → **공급자 추가** → 종류 선택, 기본 URL, 모델, 키 (로컬의 경우 선택 사항), 기능 토글, **활성화 설정** → **연결 테스트**.
2. **구성/환경 (운영):** `App:Ai:Providers[]` 및 `App:Ai:ActiveProvider` 시드 — 자격 증명이 없을 때 첫 시작 시 저장소로 가져옴. 예시 (환경, 공급자 인덱스 `0`):

   ```
   App__Ai__ActiveProvider=OpenAiCompatible
   App__Ai__Providers__0__Kind=OpenAiCompatible
   App__Ai__Providers__0__BaseUrl=http://localhost:11434/v1/
   App__Ai__Providers__0__Model=llama3.1:8b
   # App__Ai__Providers__0__ApiKey=...   (keyless 로컬 엔드포인트는 생략)
   ```

정확히 하나의 공급자가 한 번에 활성화됩니다. 키는 암호화되어 저장되며 로컬 엔드포인트는 키가 필요하지 않습니다.

## 보안: http vs https

일반 텍스트 `http://`는 **로컬루프/프라이빗 (인트라넷) 호스트**에만 수용됩니다 — 로컬 LLM 경우 (Ollama, LM Studio, vLLM, 온프레미스 박스). 공개 인터넷에서 라우팅 가능한 모든 호스트는 **반드시** `https://`이어야 하므로 API 키가 일반 텍스트로 전송되지 않습니다. 에어갭/온프레미스: 기본 URL을 내부 엔드포인트 (로컬루프 또는 프라이빗 IP)로 지정하고 런타임이 인증되지 않은 경우 키를 공백으로 둡니다.

## 기본 제공 로컬 AI (ONNX, 배포됨)

cMind는 **기본적으로 활성화된 실제 인프로세스 로컬 LLM** (Microsoft.ML.OnnxRuntimeGenAI)을 제공합니다 — 키 없음, 외부 서비스 없음. 첫 시작 시 공급자가 구성되지 않았고 `App:Branding:AllowBuiltInAi`이 `true`인 경우 자동으로 시드되고 활성화됩니다.

- **구성:** `App:Ai:BuiltIn:Enabled` (기본값 `true`), `App:Ai:BuiltIn:ModelPath` (기본값 `models/onnx`, 앱 기본 디렉토리에 상대), `App:Ai:BuiltIn:MaxTokens` (기본값 `1024`).
- **모델 파일:** `ModelPath`를 ONNX GenAI 모델을 포함하는 디렉토리로 지정 — `genai_config.json`, 토크나이저 및 `.onnx` 가중치. CPU **Phi-3-mini** 빌드가 잘 작동합니다. 예를 들어:

  ```bash
  pip install huggingface_hub
  huggingface-cli download microsoft/Phi-3-mini-128k-instruct-onnx \
    --include cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/* \
    --local-dir ./models
  # 그 다음 App:Ai:BuiltIn:ModelPath를 해당 폴더로 설정 (genai_config.json 포함)
  ```

  배포 이미지/Helm 볼륨과 함께 폴더를 번들로 묶거나 런타임에 마운트하세요. 파일이 없으면 기본 제공 모델이 "모델이 설치되지 않음"이라는 명확한 메시지로 저하됩니다 — 앱은 여전히 실행되며 다른 공급자를 구성하거나 모델을 설치할 수 있습니다.
- **GPU:** CPU 패키지/모델을 CUDA/DirectML ONNX GenAI 빌드로 교체하세요. 코드 경로는 변경되지 않습니다.

## 화이트라벨: AI 제한

`App:Branding`에서 설정 (서버 측에서 적용 — 금지된 업서트는 `400` 반환):

- `AllowBuiltInAi: false` — 배포된 기본 제공 모델을 완전히 제거합니다.
- `AllowLocalProviders: false` — 로컬/자체 호스트 엔드포인트 (Ollama/LM Studio/vLLM 및 모든 로컬루프/프라이빗 OpenAI 호환 URL)를 금지합니다.
- `AllowedAiProviderKinds: ["Anthropic","OpenAiCompatible"]` — 이 종류만 허용 (빈 = 모두).

## 향후 기본 제공 모델 확장

공급자 계층은 어댑터 기반 (`AiProviderKind`로 키가 지정된 `IAiProvider`)이므로 향후 기본 제공 모델 런타임은 AI 기능을 터치하지 않고 추가됩니다: 종류를 추가하고 하나의 어댑터를 구현하고 등록합니다. ONNX 기본 제공은 참조 구현입니다. [AI 기능 → 확장](../features/ai.md#extending-future-built-in-models)을 참조하세요.

## 클라우드 공급자

### Anthropic (Claude)

- 키: <https://console.anthropic.com/> → API 키.
- 기본 URL: `https://api.anthropic.com/` · 모델: 예를 들어 `claude-opus-4-8`.
- 기능: 웹 검색 + 비전 기본 활성화.

### OpenAI

- 키: <https://platform.openai.com/api-keys>.
- 기본 URL: `https://api.openai.com/v1/` · 모델: 예를 들어 `gpt-4o`.
- 종류: **OpenAiCompatible**. 비전 모델을 사용하는 경우 대화에서 비전 활성화.

### Azure OpenAI

- 키 + 엔드포인트: Azure 포털 → Azure OpenAI 리소스.
- 기본 URL: `https://<resource>.openai.azure.com/` · 모델: **배포 이름**.
- 종류: **AzureOpenAi** (`api-key` 헤더 + `api-version` 쿼리 및 배포 경로 사용).

### Google Gemini

- 키: <https://aistudio.google.com/app/apikey>.
- 기본 URL: `https://generativelanguage.googleapis.com/` · 모델: 예를 들어 `gemini-2.0-flash`.
- 종류: **Gemini**. 웹 검색 그라운딩 + 비전 기본 활성화.

### 기타 OpenAI 호환 클라우드 (OpenRouter, Groq, Together, Mistral, DeepSeek)

- 종류: **OpenAiCompatible**. 기본 URL = 공급자의 OpenAI 호환 엔드포인트, 모델 = 해당 모델 ID, ApiKey = 공급자 키. cMind 변경 필요 없음 — 하나의 어댑터가 모두 제공합니다.

## 로컬 모델 (키 없음)

모든 로컬 런타임은 OpenAI Chat Completions 와이어를 노출하므로 **Kind: OpenAiCompatible**을 런타임의 기본 URL 및 제공되는 모델 이름과 함께 사용하세요. 키는 공백으로 둡니다.

### Ollama

```
# https://ollama.com에서 설치한 다음:
ollama pull llama3.1:8b
```

- 기본 URL: `http://localhost:11434/v1/` · 모델: 끌어온 이름 (예: `llama3.1:8b`, `qwen2.5-coder`).
- API 키 없음. 기능은 기본적으로 텍스트 전용입니다. 비전 모델에만 비전 활성화.

### LM Studio

- 로컬 서버 시작 (개발자 → 서버 시작).
- 기본 URL: `http://localhost:1234/v1/` · 모델: 로드된 모델 ID. API 키 없음.

### vLLM / llama.cpp `server` / LocalAI

- OpenAI 호환 엔드포인트 제공 (각각 하나씩 배포됨).
- 기본 URL: 제공되는 URL (예: `http://localhost:8000/v1/`) · 모델: 제공되는 모델 이름. 앞에 인증을 넣지 않으면 키 없음.

## 확인

- **테스트 연결**은 대화에서 작은 핑 완성을 실행하고 성공 + 지연 시간을 보고합니다 — 로컬 엔드포인트를 확인하는 데 이상적입니다.
- 자동화됨: 앱의 E2E 스위트는 기본적으로 인프로세스 가짜 OpenAI 호환 서버에 대해 모든 AI 기능을 구동하거나 `AI_E2E_BASEURL` (+ 선택 사항 `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`)이 설정된 경우 실제 공급자를 구동합니다. [AI 기능 → 테스트](../features/ai.md#testing-with-the-fake-local-llm)를 참조하세요.

## 전환 / 로테이션

- **활성 공급자 전환:** 설정 → AI → 다른 카드에서 **활성화 설정** (하나를 활성화하면 나머지는 비활성화됨).
- **키 로테이션:** 공급자를 편집하고 새 키를 입력합니다 (저장된 키를 유지하려면 공백으로 둠).
- **제거:** 카드를 삭제합니다. 활성 공급자가 없으면 AI 기능이 비활성화되고 앱의 나머지 부분은 변경 없이 실행됩니다.
