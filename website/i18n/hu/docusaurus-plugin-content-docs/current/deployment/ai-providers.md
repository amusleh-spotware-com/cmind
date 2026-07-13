---
description: "Setup catalog for every AI provider cMind supports — Anthropic, OpenAI, Azure OpenAI, Google Gemini, and every OpenAI-compatible endpoint including local models (Ollama, LM Studio, vLLM, llama.cpp, LocalAI) and OpenAI-compatible clouds."
---

# AI providers — setup catalog

cMind's AI layer is provider-agnostic (see [AI features](../features/ai.md)). Configure a provider two
ways:

1. **UI (owner):** Settings → AI → **Add provider** → pick kind, base URL, model, key (optional for
   local), capability toggles, **Set active** → **Test connection**.
2. **Config/env (ops):** seed `App:Ai:Providers[]` and `App:Ai:ActiveProvider` — imported into the store
   on first startup when no credentials exist. Example (env, provider index `0`):

   ```
   App__Ai__ActiveProvider=OpenAiCompatible
   App__Ai__Providers__0__Kind=OpenAiCompatible
   App__Ai__Providers__0__BaseUrl=http://localhost:11434/v1/
   App__Ai__Providers__0__Model=llama3.1:8b
   # App__Ai__Providers__0__ApiKey=...   (omit for keyless local endpoints)
   ```

Exactly one provider is active at a time. Keys are stored encrypted; a local endpoint needs none.

## Security: http vs https

Plaintext `http://` is accepted **only** for loopback / private (intranet) hosts — the local-LLM case
(Ollama, LM Studio, vLLM, an on-prem box). Any host routable on the public internet **must** be
`https://`, so an API key is never sent in the clear. Air-gapped/on-prem: point the base URL at your
internal endpoint (loopback or private IP) and leave the key blank if the runtime is unauthenticated.

## Built-in local AI (ONNX, shipped)

cMind ships a **real in-process local LLM** (Microsoft.ML.OnnxRuntimeGenAI) that is **enabled by
default** — no key, no external service. On first startup, when no provider is configured and
`App:Branding:AllowBuiltInAi` is `true`, it is seeded and activated automatically.

- **Config:** `App:Ai:BuiltIn:Enabled` (default `true`), `App:Ai:BuiltIn:ModelPath` (default
  `models/onnx`, relative to the app base directory), `App:Ai:BuiltIn:MaxTokens` (default `1024`).
- **Model files:** point `ModelPath` at a directory containing an ONNX GenAI model — `genai_config.json`,
  the tokenizer, and the `.onnx` weights. A CPU **Phi-3-mini** build works well, e.g.:

  ```bash
  pip install huggingface_hub
  huggingface-cli download microsoft/Phi-3-mini-4k-instruct-onnx \
    --include cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/* \
    --local-dir ./models
  # then set App:Ai:BuiltIn:ModelPath to that folder (contains genai_config.json)
  ```

  Bundle the folder with your deployment image / Helm volume, or mount it at runtime. When the files are
  absent the built-in degrades to a clear "model not installed" message — the app still runs; configure
  another provider or install the model.
- **GPU:** swap the CPU package/model for a CUDA/DirectML ONNX GenAI build; the code path is unchanged.

## White-label: limiting AI

Set under `App:Branding` (enforced server-side — a forbidden upsert returns `400`):

- `AllowBuiltInAi: false` — remove the shipped built-in model entirely.
- `AllowLocalProviders: false` — forbid local/self-hosted endpoints (Ollama/LM Studio/vLLM and any
  loopback/private OpenAI-compatible URL).
- `AllowedAiProviderKinds: ["Anthropic","OpenAiCompatible"]` — allow only these kinds (empty = all).

## Extending with future built-in models

The provider layer is adapter-based (`IAiProvider` keyed by `AiProviderKind`), so a future built-in model
runtime is added without touching any AI feature: add a kind, implement one adapter, register it. The
ONNX built-in is the reference implementation. See [AI features → Extending](../features/ai.md#extending-future-built-in-models).

## Cloud providers

### Anthropic (Claude)

- Key: <https://console.anthropic.com/> → API keys.
- Base URL: `https://api.anthropic.com/` · Model: e.g. `claude-opus-4-8`.
- Capabilities: web search + vision on by default.

### OpenAI

- Key: <https://platform.openai.com/api-keys>.
- Base URL: `https://api.openai.com/v1/` · Model: e.g. `gpt-4o`.
- Kind: **OpenAiCompatible**. Enable vision in the dialog if using a vision model.

### Azure OpenAI

- Key + endpoint: Azure portal → your Azure OpenAI resource.
- Base URL: `https://<resource>.openai.azure.com/` · Model: your **deployment name**.
- Kind: **AzureOpenAi** (uses the `api-key` header + `api-version` query and the deployment path).

### Google Gemini

- Key: <https://aistudio.google.com/app/apikey>.
- Base URL: `https://generativelanguage.googleapis.com/` · Model: e.g. `gemini-2.0-flash`.
- Kind: **Gemini**. Web-search grounding + vision on by default.

### Other OpenAI-compatible clouds (OpenRouter, Groq, Together, Mistral, DeepSeek)

- Kind: **OpenAiCompatible**. Base URL = the provider's OpenAI-compatible endpoint, Model = its model id,
  ApiKey = the provider key. No cMind change needed — one adapter serves them all.

## Local models (no key)

All local runtimes expose the OpenAI Chat Completions wire, so use **Kind: OpenAiCompatible** with the
runtime's base URL and served model name; leave the key blank.

### Ollama

```
# install from https://ollama.com, then:
ollama pull llama3.1:8b
```

- Base URL: `http://localhost:11434/v1/` · Model: the pulled name (e.g. `llama3.1:8b`, `qwen2.5-coder`).
- No API key. Capabilities default to text-only; enable vision only for a vision model.

### LM Studio

- Start the local server (Developer → Start server).
- Base URL: `http://localhost:1234/v1/` · Model: the loaded model id. No API key.

### vLLM / llama.cpp `server` / LocalAI

- Serve an OpenAI-compatible endpoint (each ships one).
- Base URL: your served URL (e.g. `http://localhost:8000/v1/`) · Model: the served model name. No key
  unless you put auth in front.

## Verifying

- **Test connection** in the dialog runs a tiny ping completion and reports success + latency — ideal
  for confirming a local endpoint.
- Automated: the app's E2E suite drives every AI feature against an in-process fake OpenAI-compatible
  server by default, or your real provider when `AI_E2E_BASEURL` (+ optional `AI_E2E_API_KEY` /
  `AI_E2E_KIND` / `AI_E2E_MODEL`) is set. See [AI features → Testing](../features/ai.md#testing-with-the-fake-local-llm).

## Switching / rotating

- **Switch active provider:** Settings → AI → **Set active** on another card (activating one deactivates
  the rest).
- **Rotate a key:** edit the provider and supply a new key (leave blank to keep the stored one).
- **Remove:** delete the card. With no active provider, AI features disable and the rest of the app runs
  unchanged.
