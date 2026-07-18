---
description: "Katalog konfiguracji dla każdego dostawcy AI obsługiwanego przez cMind — Anthropic, OpenAI, Azure OpenAI, Google Gemini i każdy endpoint kompatybilny z OpenAI, w tym modele lokalne (Ollama, LM Studio, vLLM, llama.cpp, LocalAI) oraz chmury OpenAI-kompatybilne."
---

# Dostawcy AI — katalog konfiguracji

Warstwa AI cMind jest niezależna od dostawcy (patrz [funkcje AI](../features/ai.md)). Dostawcę można
skonfigurować na dwa sposoby:

1. **UI (właściciel):** Ustawienia → AI → **Dodaj dostawcę** → wybierz rodzaj, base URL, model, klucz
   (opcjonalny dla lokalnego), przełączniki możliwości, **Ustaw jako aktywny** → **Testuj połączenie**.
2. **Config/env (ops):** zaseeduj `App:Ai:Providers[]` i `App:Ai:ActiveProvider` — importowane do sklepu
   przy pierwszym starcie, gdy brak poświadczeń. Przykład (env, indeks dostawcy `0`):

   ```
   App__Ai__ActiveProvider=OpenAiCompatible
   App__Ai__Providers__0__Kind=OpenAiCompatible
   App__Ai__Providers__0__BaseUrl=http://localhost:11434/v1/
   App__Ai__Providers__0__Model=llama3.1:8b
   # App__Ai__Providers__0__ApiKey=...   (pomiń dla lokalnych endpointów bez klucza)
   ```

Dokładnie jeden dostawca jest aktywny na raz. Klucze są przechowywane zaszyfrowane; lokalny endpoint
ich nie wymaga.

## Bezpieczeństwo: http vs https

Nieszyfrowane `http://` jest akceptowane **tylko** dla loopback / hostów prywatnych (intranet) —
przypadek lokalnego LLM (Ollama, LM Studio, vLLM, on-prem box). Każdy host routowany w publicznym
internecie **musi** być `https://`, więc klucz API nigdy nie jest wysyłany tekstem. Air-gapped/on-prem:
wskarz base URL na swój wewnętrzny endpoint (loopback lub prywatny IP) i zostaw klucz pusty, jeśli
runtime nie wymaga autoryzacji.

## Wbudowany lokalny AI (ONNX, wysyłany)

cMind wysyła **rzeczywisty lokalny LLM w procesie** (Microsoft.ML.OnnxRuntimeGenAI), który jest
**domyślnie włączony** — bez klucza, bez zewnętrznej usługi. Przy pierwszym starcie, gdy brak
skonfigurowanego dostawcy i `App:Branding:AllowBuiltInAi` jest `true`, jest automatycznie seedaowany i
aktywowany.

- **Konfiguracja:** `App:Ai:BuiltIn:Enabled` (domyślnie `true`), `App:Ai:BuiltIn:ModelPath` (domyślnie
  `models/onnx`, relatywnie do katalogu bazowego aplikacji), `App:Ai:BuiltIn:MaxTokens` (domyślnie
  `1024`).
- **Pliki modelu:** wskaż `ModelPath` na katalog zawierający model ONNX GenAI — `genai_config.json`,
  tokenizer i wagi `.onnx`. Build CPU **Phi-3.5-mini-instruct** działa dobrze, np.:

  ```bash
  pip install huggingface_hub
  huggingface-cli download microsoft/Phi-3.5-mini-instruct-onnx \
    --include cpu_and_mobile/cpu-int4-awq-block-128-acc-level-4/* \
    --local-dir ./models
  # następnie ustaw App:Ai:BuiltIn:ModelPath na ten folder (zawiera genai_config.json)
  ```

  Dołącz folder do obrazu wdrożeniowego / wolumenu Helm lub zamontuj w runtime. Gdy pliki są
  nieobecne, wbudowany model degraderuje do jasnego komunikatu „model nie zainstalowany" — aplikacja
  nadal działa; skonfiguruj innego dostawcę lub zainstaluj model.
- **GPU:** zamień pakiet/model CPU na build ONNX GenAI CUDA/DirectML; ścieżka kodu bez zmian.

## White-label: ograniczanie AI

Ustaw pod `App:Branding` (wymuszane po stronie serwera — niedozwolony upsert zwraca `400`):

- `AllowBuiltInAi: false` — całkowicie usuwa wysłany wbudowany model.
- `AllowLocalProviders: false` — zabrania lokalnych/self-hosted endpointów (Ollama/LM Studio/vLLM i
  dowolny URL OpenAI-kompatybilny loopback/prywatny).
- `AllowedAiProviderKinds: ["Anthropic","OpenAiCompatible"]` — dozwolone tylko te rodzaje (puste =
  wszystkie).

## Rozszerzanie o przyszłe wbudowane modele

Warstwa dostawców jest oparta na adapterach (`IAiProvider` key-by `AiProviderKind`), więc przyszły
wbudowany runtime modelu jest dodawany bez dotykania żadnej funkcji AI: dodaj kind, zaimplementuj jeden
adapter, zarejestruj go. Wbudowany ONNX jest implementacją referencyjną. Patrz [funkcje AI →
Rozszerzanie](../features/ai.md#extending-future-built-in-models).

## Dostawcy chmurowi

### Anthropic (Claude)

- Klucz: <https://console.anthropic.com/> → klucze API.
- Base URL: `https://api.anthropic.com/` · Model: np. `claude-opus-4-8`.
- Możliwości: web search + wizja domyślnie włączone.

### OpenAI

- Klucz: <https://platform.openai.com/api-keys>.
- Base URL: `https://api.openai.com/v1/` · Model: np. `gpt-4o`.
- Kind: **OpenAiCompatible**. Włącz wizję w dialogu, jeśli używasz modelu wizyjnego.

### Azure OpenAI

- Klucz + endpoint: portal Azure → zasób Azure OpenAI.
- Base URL: `https://<resource>.openai.azure.com/` · Model: twoja **nazwa deploymentu**.
- Kind: **AzureOpenAi** (używa nagłówka `api-key` + zapytania `api-version` i ścieżki deployment).

### Google Gemini

- Klucz: <https://aistudio.google.com/app/apikey>.
- Base URL: `https://generativelanguage.googleapis.com/` · Model: np. `gemini-2.0-flash`.
- Kind: **Gemini**. Web-search grounding + wizja domyślnie włączone.

### Inne chmury OpenAI-kompatybilne (OpenRouter, Groq, Together, Mistral, DeepSeek)

- Kind: **OpenAiCompatible**. Base URL = endpoint OpenAI-kompatybilny dostawcy, Model = jego id
  modelu, ApiKey = klucz dostawcy. Bez zmiany w cMind — jeden adapter obsługuje ich wszystkich.

## Modele lokalne (bez klucza)

Wszystkie lokalne runtime'y eksponują wire OpenAI Chat Completions, więc użyj **Kind:
OpenAiCompatible** z base URL runtime'u i nazwą serwowanego modelu; zostaw klucz pusty.

### Ollama

```
# install from https://ollama.com, then:
ollama pull llama3.1:8b
```

- Base URL: `http://localhost:11434/v1/` · Model: ściągnięta nazwa (np. `llama3.1:8b`, `qwen2.5-coder`).
- Bez klucza API. Możliwości domyślnie text-only; włącz wizję tylko dla modelu wizyjnego.

### LM Studio

- Uruchom lokalny serwer (Developer → Start server).
- Base URL: `http://localhost:1234/v1/` · Model: załadowane id modelu. Bez klucza API.

### vLLM / llama.cpp `server` / LocalAI

- Serwuj OpenAI-kompatybilny endpoint (każdy wysyła jeden).
- Base URL: twój served URL (np. `http://localhost:8000/v1/`) · Model: nazwa serwowanego modelu. Bez
  klucza, chyba że masz auth przed nim.

## Weryfikacja

- **Testuj połączenie** w dialogu uruchamia tiny ping completion i raportuje sukces + latencję —
  idealne do potwierdzenia lokalnego endpointu.
- Zautomatyzowane: zestaw E2E aplikacji wykonuje każdą funkcję AI przeciwko in-process fake
  OpenAI-compatible server domyślnie, lub twojemu realnemu dostawcy gdy ustawione jest
  `AI_E2E_BASEURL` (+ opcjonalnie `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`). Patrz [funkcje
  AI → Testowanie](../features/ai.md#testing-with-the-fake-local-llm).

## Przełączanie / rotacja

- **Przełącz aktywnego dostawcę:** Ustawienia → AI → **Ustaw jako aktywny** na innej karcie
  (aktywacja jednego dezaktywuje resztę).
- **Rotuj klucz:** edytuj dostawcę i podaj nowy klucz (zostaw puste, aby zachować zapisany).
- **Usuń:** usuń kartę. Bez aktywnego dostawcy funkcje AI są wyłączone, a reszta aplikacji działa
  bez zmian.
