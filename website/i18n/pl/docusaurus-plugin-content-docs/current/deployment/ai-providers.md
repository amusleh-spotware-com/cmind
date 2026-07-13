---
description: "Katalog konfiguracji dla każdego dostawcy AI obsługiwanego przez cMind — Anthropic, OpenAI, Azure OpenAI, Google Gemini i każdego kompatybilnego endpoint'u OpenAI, w tym modeli lokalnych (Ollama, LM Studio, vLLM, llama.cpp, LocalAI) i chmur kompatybilnych z OpenAI."
---

# Dostawcy AI — katalog konfiguracji

Warstwa AI cMind jest niezależna od dostawcy (patrz [Funkcje AI](../features/ai.md)). Konfiguruj dostawcę na dwa sposoby:

1. **UI (właściciel):** Ustawienia → AI → **Dodaj dostawcę** → wybierz rodzaj, podstawowy URL, model, klucz (opcjonalnie dla lokalnego), przełączniki możliwości, **Ustaw aktywny** → **Test połączenia**.
2. **Config/env (ops):** nasadź `App:Ai:Providers[]` i `App:Ai:ActiveProvider` — importowany do sklepu przy pierwszym uruchomieniu, gdy nie ma poświadczeń. Przykład (env, indeks dostawcy `0`):

   ```
   App__Ai__ActiveProvider=OpenAiCompatible
   App__Ai__Providers__0__Kind=OpenAiCompatible
   App__Ai__Providers__0__BaseUrl=http://localhost:11434/v1/
   App__Ai__Providers__0__Model=llama3.1:8b
   # App__Ai__Providers__0__ApiKey=...   (pomiń dla bezklawiszowych endpoint'ów lokalnych)
   ```

Dokładnie jeden dostawca jest aktywny w danym momencie. Klucze są przechowywane zaszyfrowane; endpoint lokalny nie potrzebuje żadnych.

## Bezpieczeństwo: http vs https

Plaintext `http://` jest akceptowany **tylko** dla loopback / prywatnych (intranet) hostów — przypadek lokalnego LLM (Ollama, LM Studio, vLLM, box na prem). Każdy host, który można trasa na publiczny internet **musi** być `https://`, więc klucz API nigdy nie jest wysyłany w czystości. Air-gapped/on-prem: wskaż podstawowy URL na wewnętrzny endpoint (loopback lub prywatny IP) i zostaw klucz pusty, jeśli runtime nie jest nieuwierzytelniany.

## Wbudowana lokalna AI (ONNX, wysłana)

cMind wysyła **rzeczywisty LLM lokalny w procesie** (Microsoft.ML.OnnxRuntimeGenAI), który jest **włączony domyślnie** — brak klucza, brak usługi zewnętrznej. Przy pierwszym uruchomieniu, gdy nie ma skonfigurowanego dostawcy i `App:Branding:AllowBuiltInAi` jest `true`, jest nasadzony i aktywowany automatycznie.

- **Config:** `App:Ai:BuiltIn:Enabled` (domyślnie `true`), `App:Ai:BuiltIn:ModelPath` (domyślnie `models/onnx`, względem bazy danych app), `App:Ai:BuiltIn:MaxTokens` (domyślnie `1024`).
- **Pliki modelu:** wskaż `ModelPath` na katalog zawierający model ONNX GenAI — `genai_config.json`, tokenizer i wagi `.onnx`. CPU **Phi-3-mini** build działa dobrze, np.:

  ```bash
  pip install huggingface_hub
  huggingface-cli download microsoft/Phi-3-mini-4k-instruct-onnx \
    --include cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/* \
    --local-dir ./models
  # następnie ustaw App:Ai:BuiltIn:ModelPath na ten folder (zawiera genai_config.json)
  ```

  Pakuj folder z obrazem wdrażania / woluminem Helm, lub montuj przy runtime. Gdy pliki są nieobecne, wbudowany degraduje do jasnej wiadomości "model nie zainstalowany" — aplikacja nadal działa; konfiguruj innego dostawcę lub zainstaluj model.
- **GPU:** zamień pakiet CPU/model na budowę CUDA/DirectML ONNX GenAI; ścieżka kodu jest niezmieniona.

## White-label: limitowanie AI

Ustaw pod `App:Branding` (egzekwowane server-side — zabroniony upsert zwraca `400`):

- `AllowBuiltInAi: false` — usuń całkowicie wysłany wbudowany model.
- `AllowLocalProviders: false` — zabroń endpoint'om lokalnym/samodzielnie hostowanym (Ollama/LM Studio/vLLM i każdy loopback/prywatny URL kompatybilny z OpenAI).
- `AllowedAiProviderKinds: ["Anthropic","OpenAiCompatible"]` — pozwól tylko te rodzaje (pusty = wszystko).

## Rozszerzanie z przyszłymi wbudowanymi modelami

Warstwa dostawcy jest oparty na adapterze (`IAiProvider` zaindeksowany przez `AiProviderKind`), więc przyszły wbudowany runtime modelu jest dodawany bez dotykania żadnej funkcji AI: dodaj rodzaj, implementuj jeden adapter, rejestruj go. Wbudowany ONNX jest implementacją odniesienia. Patrz [Funkcje AI → Rozszerzanie](../features/ai.md#extending-future-built-in-models).

## Dostawcy chmury

### Anthropic (Claude)

- Klucz: <https://console.anthropic.com/> → Klucze API.
- Podstawowy URL: `https://api.anthropic.com/` · Model: np. `claude-opus-4-8`.
- Możliwości: wyszukiwanie sieciowe + wizja domyślnie włączone.

### OpenAI

- Klucz: <https://platform.openai.com/api-keys>.
- Podstawowy URL: `https://api.openai.com/v1/` · Model: np. `gpt-4o`.
- Rodzaj: **OpenAiCompatible**. Włącz wizję w dialogu, jeśli używasz modelu wizji.

### Azure OpenAI

- Klucz + endpoint: Portal Azure → zasób Azure OpenAI.
- Podstawowy URL: `https://<resource>.openai.azure.com/` · Model: **nazwa wdrażania**.
- Rodzaj: **AzureOpenAi** (używa nagłówka `api-key` + zapytania `api-version` i ścieżki wdrażania).

### Google Gemini

- Klucz: <https://aistudio.google.com/app/apikey>.
- Podstawowy URL: `https://generativelanguage.googleapis.com/` · Model: np. `gemini-2.0-flash`.
- Rodzaj: **Gemini**. Uziemienie wyszukiwania sieciowego + wizja domyślnie włączone.

### Inne chmury kompatybilne z OpenAI (OpenRouter, Groq, Together, Mistral, DeepSeek)

- Rodzaj: **OpenAiCompatible**. Podstawowy URL = kompatybilny endpoint OpenAI dostawcy, Model = jego id modelu, ApiKey = klucz dostawcy. Żadna zmiana cMind nie jest wymagana — jeden adapter służy je wszystkim.
