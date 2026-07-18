---
description: "Setup-Katalog für jeden KI-Provider, den cMind unterstützt – Anthropic, OpenAI, Azure OpenAI, Google Gemini und jeden OpenAI-kompatiblen Endpoint einschließlich lokaler Modelle (Ollama, LM Studio, vLLM, llama.cpp, LocalAI) und OpenAI-kompatiblen Clouds."
---

# KI-Provider – Setup-Katalog

cMind's KI-Layer ist Provider-agnostisch (siehe [KI-Features](../features/ai.md)). Konfiguriere einen Provider auf zwei Wegen:

1. **UI (Owner):** Settings → AI → **Add provider** → wähle Kind, Base URL, Model, Key (optional für lokal), Capability Toggles, **Set active** → **Test connection**.
2. **Config/Env (Ops):** seed `App:Ai:Providers[]` und `App:Ai:ActiveProvider` – beim ersten Start importiert, wenn keine Anmeldedaten vorhanden sind. Beispiel (Env, Provider-Index `0`):

   ```
   App__Ai__ActiveProvider=OpenAiCompatible
   App__Ai__Providers__0__Kind=OpenAiCompatible
   App__Ai__Providers__0__BaseUrl=http://localhost:11434/v1/
   App__Ai__Providers__0__Model=llama3.1:8b
   # App__Ai__Providers__0__ApiKey=...   (omit für schlüssellose lokale Endpoints)
   ```

Genau ein Provider ist gleichzeitig aktiv. Schlüssel werden verschlüsselt gespeichert; ein lokaler Endpoint braucht keinen.

## Sicherheit: http vs https

Klartext `http://` wird **nur** für Loopback/Private (Intranet) Hosts akzeptiert – der Local-LLM-Fall (Ollama, LM Studio, vLLM, ein On-Prem-Box). Jeder auf dem öffentlichen Internet routbare Host **muss** `https://` sein, daher wird ein API-Schlüssel nie unverschlüsselt versendet. Air-Gapped/On-Prem: zeige die Base-URL auf deinen internen Endpoint (Loopback oder private IP) und lass den Schlüssel leer, wenn die Runtime unauthentifiziert ist.

## Eingebaute lokale KI (ONNX, versendet)

cMind versendet ein **echtes In-Process lokales LLM** (Microsoft.ML.OnnxRuntimeGenAI), das **standardmäßig aktiviert** ist – kein Schlüssel, kein externer Service. Beim ersten Start, wenn kein Provider konfiguriert ist und `App:Branding:AllowBuiltInAi` ist `true`, wird es automatisch geseedet und aktiviert.

- **Config:** `App:Ai:BuiltIn:Enabled` (Standard `true`), `App:Ai:BuiltIn:ModelPath` (Standard `models/onnx`, relativ zum App-Basis-Verzeichnis), `App:Ai:BuiltIn:MaxTokens` (Standard `1024`).
- **Model-Dateien:** zeige `ModelPath` auf ein Verzeichnis mit einem ONNX GenAI-Modell – `genai_config.json`, der Tokenizer und die `.onnx`-Gewichte. Eine CPU **Phi-3-mini** Build funktioniert gut, z.B.:

  ```bash
  pip install huggingface_hub
  huggingface-cli download microsoft/Phi-3-mini-128k-instruct-onnx \
    --include cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/* \
    --local-dir ./models
  # dann setze App:Ai:BuiltIn:ModelPath zu diesem Ordner (enthält genai_config.json)
  ```

  Bündel den Ordner mit deinem Deployment-Image / Helm-Volume oder mounten ihn zur Laufzeit. Wenn die Dateien abwesend sind, degradiert das Built-In zu einer klaren "Model nicht installiert"-Nachricht – die App läuft immer noch; konfiguriere einen anderen Provider oder installiere das Modell.
- **GPU:** tausche das CPU-Paket/Modell für einen CUDA/DirectML ONNX GenAI-Build; der Code-Pfad ist unverändert.

## White-Label: KI begrenzen

Setze unter `App:Branding` (durchgesetzt Server-seitig – ein verbotenes Upsert gibt `400` zurück):

- `AllowBuiltInAi: false` – entferne das versendet Built-In-Modell komplett.
- `AllowLocalProviders: false` – verbiete lokale/selbst-gehostete Endpoints (Ollama/LM Studio/vLLM und jeden Loopback/Private OpenAI-kompatiblen URL).
- `AllowedAiProviderKinds: ["Anthropic","OpenAiCompatible"]` – erlauben nur diese Arten (Leer = alle).

## Erweiterung mit zukünftigen Built-In-Modellen

Der Provider-Layer ist Adapter-basiert (`IAiProvider` schlüsselt durch `AiProviderKind`), daher wird eine zukünftige Built-In-Modell-Runtime ohne Anfassen eines KI-Features hinzugefügt: füge eine Art hinzu, implementiere einen Adapter, registriere ihn. Das ONNX Built-In ist die Referenz-Implementierung. Siehe [KI-Features → Erweiterung](../features/ai.md#extending-future-built-in-models).

## Cloud-Provider

### Anthropic (Claude)

- Schlüssel: <https://console.anthropic.com/> → API keys.
- Base URL: `https://api.anthropic.com/` · Model: z.B. `claude-opus-4-8`.
- Capabilities: Web-Suche + Vision standardmäßig an.

### OpenAI

- Schlüssel: <https://platform.openai.com/api-keys>.
- Base URL: `https://api.openai.com/v1/` · Model: z.B. `gpt-4o`.
- Kind: **OpenAiCompatible**. Aktiviere Vision im Dialog, wenn du ein Vision-Modell verwendest.

### Azure OpenAI

- Schlüssel + Endpoint: Azure-Portal → deine Azure OpenAI-Ressource.
- Base URL: `https://<resource>.openai.azure.com/` · Model: dein **Deployment-Name**.
- Kind: **AzureOpenAi** (verwendet den `api-key`-Header + `api-version`-Query und den Deployment-Pfad).

### Google Gemini

- Schlüssel: <https://aistudio.google.com/app/apikey>.
- Base URL: `https://generativelanguage.googleapis.com/` · Model: z.B. `gemini-2.0-flash`.
- Kind: **Gemini**. Web-Suche Grounding + Vision standardmäßig an.

### Andere OpenAI-kompatible Clouds (OpenRouter, Groq, Together, Mistral, DeepSeek)

- Kind: **OpenAiCompatible**. Base URL = der Provider's OpenAI-kompatibler Endpoint, Model = seine Modell-ID, ApiKey = der Provider-Schlüssel. Kein cMind-Änderung nötig – ein Adapter serviert sie alle.

## Lokale Modelle (kein Schlüssel)

Alle lokale Runtimes exposieren den OpenAI Chat Completions-Draht, daher verwende **Kind: OpenAiCompatible** mit der Runtime's Base-URL und dem serviert Modell-Namen; lass den Schlüssel leer.

### Ollama

```
# installiere von https://ollama.com, dann:
ollama pull llama3.1:8b
```

- Base URL: `http://localhost:11434/v1/` · Model: der gepullte Name (z.B. `llama3.1:8b`, `qwen2.5-coder`).
- Kein API-Schlüssel. Capabilities Standard-Text-Only; aktiviere Vision nur für ein Vision-Modell.

### LM Studio

- Starte den lokalen Server (Developer → Start server).
- Base URL: `http://localhost:1234/v1/` · Model: die geladen Modell-ID. Kein API-Schlüssel.

### vLLM / llama.cpp `server` / LocalAI

- Serviere einen OpenAI-kompatiblen Endpoint (jeder versendet einen).
- Base URL: deine serviert URL (z.B. `http://localhost:8000/v1/`) · Model: der serviert Modell-Name. Kein Schlüssel, außer du stellst Auth vor.

## Überprüfung

- **Test connection** im Dialog führt einen winzig Ping-Completion aus und berichtet Erfolg + Latenz – ideal zur Bestätigung eines lokalen Endpoints.
- Automatisiert: die App's E2E-Suite fahrt jedes KI-Feature gegen einen In-Process Fake OpenAI-kompatiblen Server standardmäßig, oder deinen echten Provider, wenn `AI_E2E_BASEURL` (+ optional `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`) gesetzt. Siehe [KI-Features → Testing](../features/ai.md#testing-with-the-fake-local-llm).

## Wechsel / Rotation

- **Switch aktiven Provider:** Settings → AI → **Set active** auf einer anderen Karte (Aktivieren eines deaktiviert die Rest).
- **Rotiere einen Schlüssel:** bearbeite den Provider und liefere einen neuen Schlüssel (lass leer, um den gespeicherten zu behalten).
- **Entfernen:** lösche die Karte. Mit keinem aktiven Provider, deaktivieren KI-Features und der Rest der App läuft unverändert.
