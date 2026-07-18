---
description: "Catalog ตั้งค่าสำหรับทุก AI provider ที่ cMind รองรับ — Anthropic, OpenAI, Azure OpenAI, Google Gemini และทุก OpenAI-compatible endpoint รวมทั้งโมเดลท้องถิ่น (Ollama, LM Studio, vLLM, llama.cpp, LocalAI) และคลาวด์ OpenAI-compatible"
---

# AI providers — catalog ตั้งค่า

ชั้น AI ของ cMind เป็น provider-agnostic (ดู [AI features](../features/ai.md)) กำหนดค่า provider สองวิธี:

1. **UI (owner):** Settings → AI → **Add provider** → เลือก kind base URL model key (optional สำหรับ local) capability toggles **Set active** → **Test connection**
2. **Config/env (ops):** seed `App:Ai:Providers[]` และ `App:Ai:ActiveProvider` — นำเข้าเข้าไปในร้านค้าในเมื่อ startup ครั้งแรกเมื่อไม่มีข้อมูลประจำตัวอยู่ ตัวอย่าง (env provider index `0`):

   ```
   App__Ai__ActiveProvider=OpenAiCompatible
   App__Ai__Providers__0__Kind=OpenAiCompatible
   App__Ai__Providers__0__BaseUrl=http://localhost:11434/v1/
   App__Ai__Providers__0__Model=llama3.1:8b
   # App__Ai__Providers__0__ApiKey=...   (omit สำหรับ keyless local endpoints)
   ```

Provider หนึ่งเป็นอยู่ในเวลา exactly หนึ่ง กุญแจจะถูกจัดเก็บเข้ารหัส; endpoint ท้องถิ่นต้องการ none

## ความปลอดภัย: http vs https

Plaintext `http://` ยอมรับ **เท่านั้น** สำหรับ loopback / private (intranet) hosts — กรณี local-LLM (Ollama LM Studio vLLM an on-prem box) โฮสต์ใด ๆ ที่สามารถสวนที่เป็นสาธารณะ **ต้อง** เป็น `https://` เพื่อให้ API key ไม่เคยส่งในอื่น ๆ Air-gapped/on-prem: ชี้ base URL ที่ endpoint ภายในของคุณ (loopback หรือ private IP) และปล่อยคีย์ว่างถ้า runtime เป็น unauthenticated

## Built-in local AI (ONNX, shipped)

cMind ships a **real in-process local LLM** (Microsoft.ML.OnnxRuntimeGenAI) ที่ **enabled by default** — ไม่มีคีย์ ไม่มีบริการภายนอก ในการ startup ครั้งแรกเมื่อไม่มีการกำหนดค่า provider และ `App:Branding:AllowBuiltInAi` คือ `true` มันจะถูกปลูกแล้วและ activate โดยอัตโนมัติ

- **Config:** `App:Ai:BuiltIn:Enabled` (default `true`) `App:Ai:BuiltIn:ModelPath` (default `models/onnx` relative ต่อโฮสต์แอป base directory) `App:Ai:BuiltIn:MaxTokens` (default `1024`)
- **Model files:** ชี้ `ModelPath` ที่ไดเรกทอรี่ที่มี ONNX GenAI model — `genai_config.json` tokenizer และ `.onnx` weights CPU **Phi-3-mini** build ทำงานได้ดี เช่น:

  ```bash
  pip install huggingface_hub
  huggingface-cli download microsoft/Phi-3-mini-128k-instruct-onnx \
    --include cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/* \
    --local-dir ./models
  # then set App:Ai:BuiltIn:ModelPath to that folder (contains genai_config.json)
  ```

  Bundle โฟลเดอร์ด้วย deployment image / Helm volume หรือ mount ที่ runtime เมื่อไฟล์ไม่มี built-in degrades ไปยัง clear "model not installed" message — แอปยังคงทำงาน; กำหนดค่า provider อื่นหรือติดตั้งแบบจำลอง
- **GPU:** สลับแพคเกจ CPU/model สำหรับ CUDA/DirectML ONNX GenAI build; code path ไม่เปลี่ยนแปลง

## White-label: จำกัด AI

ตั้งค่าภายใต้ `App:Branding` (บังคับใช้ server-side — forbidden upsert returns `400`):

- `AllowBuiltInAi: false` — ลบโมเดล built-in ที่ shipped ออกไปอย่างสิ้นเชิง
- `AllowLocalProviders: false` — ห้าม local/self-hosted endpoints (Ollama/LM Studio/vLLM และ loopback/private OpenAI-compatible URL ใด ๆ)
- `AllowedAiProviderKinds: ["Anthropic","OpenAiCompatible"]` — อนุญาต kinds เหล่านี้เท่านั้น (empty = all)

## ขยายกับ future built-in models

ชั้น provider เป็น adapter-based (`IAiProvider` keyed by `AiProviderKind`) ดังนั้นรันไทม์โมเดล built-in ในอนาคตจะถูกเพิ่มโดยไม่ต้องแตะฟีเจอร์ AI ใด ๆ: เพิ่มประเภท ใช้หนึ่ง adapter ลงทะเบียนมัน ONNX built-in คือการใช้งานอ้างอิง ดู [AI features → Extending](../features/ai.md#extending-future-built-in-models)

## Cloud providers

### Anthropic (Claude)

- Key: <https://console.anthropic.com/> → API keys
- Base URL: `https://api.anthropic.com/` · Model: เช่น `claude-opus-4-8`
- Capabilities: web search + vision on by default

### OpenAI

- Key: <https://platform.openai.com/api-keys>
- Base URL: `https://api.openai.com/v1/` · Model: เช่น `gpt-4o`
- Kind: **OpenAiCompatible** Enable vision ในกล่องโต้ตอบถ้าใช้ vision model

### Azure OpenAI

- Key + endpoint: Azure portal → Azure OpenAI resource ของคุณ
- Base URL: `https://<resource>.openai.azure.com/` · Model: **deployment name** ของคุณ
- Kind: **AzureOpenAi** (ใช้ `api-key` header + `api-version` query และ deployment path)

### Google Gemini

- Key: <https://aistudio.google.com/app/apikey>
- Base URL: `https://generativelanguage.googleapis.com/` · Model: เช่น `gemini-2.0-flash`
- Kind: **Gemini** Web-search grounding + vision on by default

### Other OpenAI-compatible clouds (OpenRouter Groq Together Mistral DeepSeek)

- Kind: **OpenAiCompatible** Base URL = endpoint OpenAI-compatible ของ provider Model = model id ของมัน ApiKey = key ของ provider ไม่จำเป็นต้องมีการเปลี่ยนแปลง cMind — adapter หนึ่งใช้บริการพวกเขาทั้งหมด

## Local models (ไม่มีคีย์)

รันไทม์ท้องถิ่นทั้งหมดเปิดเผย OpenAI Chat Completions wire ดังนั้นใช้ **Kind: OpenAiCompatible** ด้วย base URL ของรันไทม์และชื่อโมเดลที่ให้บริการ; ปล่อยคีย์ว่าง

### Ollama

```
# install from https://ollama.com, then:
ollama pull llama3.1:8b
```

- Base URL: `http://localhost:11434/v1/` · Model: ชื่อที่ pull (เช่น `llama3.1:8b` `qwen2.5-coder`)
- ไม่มี API key Capabilities default เป็น text-only; enable vision เฉพาะสำหรับ vision model

### LM Studio

- เริ่มต้นเซิร์ฟเวอร์ local (Developer → Start server)
- Base URL: `http://localhost:1234/v1/` · Model: model id ที่โหลด ไม่มี API key

### vLLM / llama.cpp `server` / LocalAI

- ให้บริการ OpenAI-compatible endpoint (แต่ละครั้ง ships one)
- Base URL: URL ที่เรียกใช้งาน (เช่น `http://localhost:8000/v1/`) · Model: ชื่อโมเดลที่เรียกใช้งาน ไม่มีคีย์เว้นแต่คุณใส่ auth นอน

## ตรวจสอบ

- **Test connection** ในกล่องโต้ตอบ runs tiny ping completion และ reports success + latency — ideal สำหรับ confirming endpoint ท้องถิ่น
- Automated: E2E suite ของแอปดำเนิน AI feature ทุกฟีเจอร์ต่อเซิร์ฟเวอร์ fake OpenAI-compatible ในกระบวนการโดยค่าเริ่มต้น หรือ provider จริงของคุณเมื่อ `AI_E2E_BASEURL` (+ optional `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`) ถูกตั้งค่า ดู [AI features → Testing](../features/ai.md#testing-with-the-fake-local-llm)

## สลับ / หมุน

- **Switch active provider:** Settings → AI → **Set active** บนการ์ดอื่น (activating one deactivates ส่วนที่เหลือ)
- **Rotate a key:** edit provider และ supply key ใหม่ (ปล่อยว่างเพื่อเก็บแบบที่เก็บไว้)
- **Remove:** ลบการ์ด ไม่มี active provider AI features disable และส่วนที่เหลือของแอปจะทำงาน unchanged
