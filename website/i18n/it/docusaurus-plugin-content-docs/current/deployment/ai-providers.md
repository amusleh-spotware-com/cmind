---
description: "Catalogo di configurazione per tutti i provider AI supportati da cMind — Anthropic, OpenAI, Azure OpenAI, Google Gemini e ogni endpoint compatibile con OpenAI, inclusi i modelli locali (Ollama, LM Studio, vLLM, llama.cpp, LocalAI) e i cloud compatibili con OpenAI."
---

# Provider AI — catalogo di configurazione

Il layer AI di cMind è agnostico rispetto al provider (vedi [funzionalità AI](../features/ai.md)). Configurare un provider in due modi:

1. **UI (proprietario):** Impostazioni → AI → **Aggiungi provider** → scegli tipo, URL base, modello, chiave (opzionale per locale), toggle funzionalità, **Imposta attivo** → **Testa connessione**.
2. **Config/env (ops):** seeded `App:Ai:Providers[]` e `App:Ai:ActiveProvider` — importati nell'archivio al primo avvio quando non esistono credenziali. Esempio (env, indice provider `0`):

   ```
   App__Ai__ActiveProvider=OpenAiCompatible
   App__Ai__Providers__0__Kind=OpenAiCompatible
   App__Ai__Providers__0__BaseUrl=http://localhost:11434/v1/
   App__Ai__Providers__0__Model=llama3.1:8b
   # App__Ai__Providers__0__ApiKey=...   (omettere per endpoint locali senza chiave)
   ```

Un solo provider è attivo alla volta. Le chiavi sono memorizzate crittografate; un endpoint locale non ne richiede nessuna.

## Sicurezza: http vs https

Il testo chiaro `http://` è accettato **solo** per host loopback / privati (intranet) — il caso LLM locale
(Ollama, LM Studio, vLLM, un box on-prem). Qualsiasi host instradabile su internet pubblica **deve** essere
`https://`, così una chiave API non viene mai inviata in chiaro. Air-gapped/on-prem: puntare l'URL base al proprio
endpoint interno (loopback o IP privato) e lasciare vuota la chiave se il runtime non è autenticato.

## AI locale integrata (ONNX, spedita)

cMind include un **vero LLM locale in-process** (Microsoft.ML.OnnxRuntimeGenAI) che è **abilitato per
default** — nessuna chiave, nessun servizio esterno. Al primo avvio, quando nessun provider è configurato e
`App:Branding:AllowBuiltInAi` è `true`, viene seeded e attivato automaticamente.

- **Configurazione:** `App:Ai:BuiltIn:Enabled` (default `true`), `App:Ai:BuiltIn:ModelPath` (default
  `models/onnx`, relativo alla directory base dell'app), `App:Ai:BuiltIn:MaxTokens` (default `1024`).
- **File del modello:** puntare `ModelPath` a una directory contenente un modello ONNX GenAI — `genai_config.json`,
  il tokenizer e i pesi `.onnx`. Una build CPU **Phi-3.5-mini-instruct** funziona bene (il shipped
  default), ad es.:

  ```bash
  pip install huggingface_hub
  huggingface-cli download microsoft/Phi-3.5-mini-instruct-onnx \
    --include cpu_and_mobile/cpu-int4-awq-block-128-acc-level-4/* \
    --local-dir ./models
  # poi impostare App:Ai:BuiltIn:ModelPath sulla cartella (contiene genai_config.json)
  ```

  Includere la cartella con l'immagine di deployment / volume Helm, o montarla a runtime. Quando i file sono
  assenti il modulo integrato degrada a un chiaro "modello non installato" — l'app continua a funzionare; configurare
  un altro provider o installare il modello.
- **GPU:** sostituire il pacchetto CPU/modello con una build ONNX GenAI CUDA/DirectML; il percorso del codice è invariato.

## White-label: limitare l'AI

Impostare sotto `App:Branding` (applicato lato server — un upsert proibito restituisce `400`):

- `AllowBuiltInAi: false` — rimuovere completamente il modello integrato spedito.
- `AllowLocalProviders: false` — proibire endpoint locali/self-hosted (Ollama/LM Studio/vLLM e qualsiasi
  URL OpenAI-compatibile loopback/privato).
- `AllowedAiProviderKinds: ["Anthropic","OpenAiCompatible"]` — consentire solo questi tipi (vuoto = tutti).

## Estendere con futuri modelli integrati

Il layer provider è basato su adapter (`IAiProvider` keyed by `AiProviderKind`), quindi un futuro runtime
modello integrato viene aggiunto senza toccare nessuna funzionalità AI: aggiungere un tipo, implementare un adapter,
registrarlo. L'integrato ONNX è l'implementazione di riferimento. Vedere [funzionalità AI → Estendere](../features/ai.md#extending-future-built-in-models).

## Provider cloud

### Anthropic (Claude)

- Chiave: <https://console.anthropic.com/> → API keys.
- URL base: `https://api.anthropic.com/` · Modello: es. `claude-opus-4-8`.
- Funzionalità: ricerca web + visione attive per default.

### OpenAI

- Chiave: <https://platform.openai.com/api-keys>.
- URL base: `https://api.openai.com/v1/` · Modello: es. `gpt-4o`.
- Tipo: **OpenAiCompatible**. Abilitare la visione nel dialogo se si usa un modello vision.

### Azure OpenAI

- Chiave + endpoint: portale Azure → propria risorsa Azure OpenAI.
- URL base: `https://<resource>.openai.azure.com/` · Modello: il proprio **nome deployment**.
- Tipo: **AzureOpenAi** (usa l'header `api-key` + query `api-version` e il path del deployment).

### Google Gemini

- Chiave: <https://aistudio.google.com/app/apikey>.
- URL base: `https://generativelanguage.googleapis.com/` · Modello: es. `gemini-2.0-flash`.
- Tipo: **Gemini**. Ground grounding ricerca web + visione attivi per default.

### Altri cloud compatibili con OpenAI (OpenRouter, Groq, Together, Mistral, DeepSeek)

- Tipo: **OpenAiCompatible**. URL base = l'endpoint compatibile OpenAI del provider, Modello = il suo id modello,
  ApiKey = la chiave provider. Nessun cambiamento in cMind — un solo adapter li serve tutti.

## Modelli locali (senza chiave)

Tutti i runtime locali espongono il wire OpenAI Chat Completions, quindi usare **Kind: OpenAiCompatible** con
l'URL base del runtime e il nome del modello servito; lasciare vuota la chiave.

### Ollama

```
# installare da https://ollama.com, poi:
ollama pull llama3.1:8b
```

- URL base: `http://localhost:11434/v1/` · Modello: il nome pulled (es. `llama3.1:8b`, `qwen2.5-coder`).
- Nessuna chiave API. Funzionalità default solo testo; abilitare visione solo per un modello vision.

### LM Studio

- Avviare il server locale (Developer → Start server).
- URL base: `http://localhost:1234/v1/` · Modello: l'id del modello caricato. Nessuna chiave API.

### vLLM / llama.cpp `server` / LocalAI

- Servire un endpoint compatibile OpenAI (ciascuno ne include uno).
- URL base: l'URL servito (es. `http://localhost:8000/v1/`) · Modello: il nome del modello servito. Nessuna chiave
  a meno che non si metta auth davanti.

## Verifica

- **Testa connessione** nel dialogo esegue una piccola ping completion e riporta successo + latenza — ideale
  per confermare un endpoint locale.
- Automatizzato: la suite E2E dell'app guida ogni funzionalità AI contro un server fake OpenAI-compatibile
  in-process per default, o il proprio provider reale quando `AI_E2E_BASEURL` (+ opzionale `AI_E2E_API_KEY` /
  `AI_E2E_KIND` / `AI_E2E_MODEL`) è impostato. Vedere [funzionalità AI → Testing](../features/ai.md#testing-with-the-fake-local-llm).

## Cambiare / ruotare

- **Cambia provider attivo:** Impostazioni → AI → **Imposta attivo** su un'altra scheda (attivandone uno disattiva
  gli altri).
- **Ruota una chiave:** modifica il provider e fornisci una nuova chiave (lascia vuoto per mantenere quella memorizzata).
- **Rimuovi:** elimina la scheda. Senza provider attivo, le funzionalità AI si disabilitano e il resto dell'app funziona invariato.
