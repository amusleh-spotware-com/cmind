---
description: "Katalog nastavení pro každého poskytovatele AI, kterého cMind podporuje — Anthropic, OpenAI, Azure OpenAI, Google Gemini a všechny kompatibilní koncové body OpenAI včetně lokálních modelů (Ollama, LM Studio, vLLM, llama.cpp, LocalAI) a kompatibilních cloudů."
---

# Poskytovatelé AI — katalog nastavení

Vrstva AI cMind je nezávislá na poskytovateli (viz [Funkce AI](../features/ai.md)). Nakonfigurujte poskytovatele dvěma způsoby:

1. **UI (vlastník):** Nastavení → AI → **Přidat poskytovatele** → vyberte druh, základní adresu URL, model, klíč (volitelné pro lokální) → přepínače schopností, **Nastavit jako aktivní** → **Testovat připojení**.
2. **Konfig/env (ops):** Inicializujte `App:Ai:Providers[]` a `App:Ai:ActiveProvider` — importovány do úložiště při prvním spuštění, když nejsou dostupné žádné přihlašovací údaje. Příklad (env, index poskytovatele `0`):

   ```
   App__Ai__ActiveProvider=OpenAiCompatible
   App__Ai__Providers__0__Kind=OpenAiCompatible
   App__Ai__Providers__0__BaseUrl=http://localhost:11434/v1/
   App__Ai__Providers__0__Model=llama3.1:8b
   # App__Ai__Providers__0__ApiKey=...   (vynechejte pro klíčové lokální koncové body)
   ```

V jednom okamžiku je aktivní přesně jeden poskytovatel. Klíče jsou uloženy šifrované; lokální koncový bod nepotřebuje žádný.

## Bezpečnost: http vs https

Prostý text `http://` je přijat **pouze** pro loopback / privátní (intranet) hostitele — případ lokálního LLM (Ollama, LM Studio, vLLM, on-prem box). Jakýkoli hostitel směrovatelný na veřejném internetu **musí** být `https://`, aby klíč API nebyl nikdy odeslán v jasné podobě. Air-gapped/on-prem: nasměrujte základní adresu URL na váš interní koncový bod (loopback nebo privátní IP) a pokud je běhové prostředí neautentizované, ponechte klíč prázdný.

## Vestavěná lokální AI (ONNX, dodáno)

cMind dodává **skutečné lokální LLM v procesu** (Microsoft.ML.OnnxRuntimeGenAI), které je **povoleno ve výchozím nastavení** — žádný klíč, žádná externí služba. Při prvním spuštění, když není nakonfigurován žádný poskytovatel a `App:Branding:AllowBuiltInAi` je `true`, je automaticky inicializován a aktivován.

- **Konfigurace:** `App:Ai:BuiltIn:Enabled` (výchozí `true`), `App:Ai:BuiltIn:ModelPath` (výchozí `models/onnx`, relativní k základnímu adresáři aplikace), `App:Ai:BuiltIn:MaxTokens` (výchozí `1024`).
- **Soubory modelu:** nasměrujte `ModelPath` na adresář obsahující model ONNX GenAI — `genai_config.json`, tokenizer a `.onnx` váhy. Build CPU **Phi-3-mini** funguje dobře, například:

  ```bash
  pip install huggingface_hub
  huggingface-cli download microsoft/Phi-3-mini-4k-instruct-onnx \
    --include cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/* \
    --local-dir ./models
  # poté nastavte App:Ai:BuiltIn:ModelPath na tuto složku (obsahuje genai_config.json)
  ```

  Zabalte složku s vaší image nasazení / Helm svazkem nebo ji připojte za běhu. Když soubory chybí, vestavěný program zhoršuje na zprávu "model není nainstalován" — aplikace stále běží; nakonfigurujte jiného poskytovatele nebo nainstalujte model.
- **GPU:** zaměňte balíček CPU/model za CUDA/DirectML ONNX GenAI build; cesta kódu se nemění.

## White-label: omezení AI

Nastavte pod `App:Branding` (vynuceno na straně serveru — zakázané upsert vrátí `400`):

- `AllowBuiltInAi: false` — odstraňte zcela dodaný vestavěný model.
- `AllowLocalProviders: false` — zakažte lokální/samoobslužné koncové body (Ollama/LM Studio/vLLM a jakákoli loopback/soukromá OpenAI kompatibilní adresa URL).
- `AllowedAiProviderKinds: ["Anthropic","OpenAiCompatible"]` — povolte pouze tyto druhy (prázdné = vše).

## Rozšíření budoucími vestavěnými modely

Vrstva poskytovatele je založena na adaptérech (`IAiProvider` klíčovaných podle `AiProviderKind`), takže budoucí vestavěný běhový modul modelu se přidá bez dotýkání se jakékoli funkce AI: přidejte druh, implementujte jeden adaptér, zaregistrujte ho. Vestavěný ONNX je referenční implementace. Viz [Funkce AI → Rozšíření](../features/ai.md#extending-future-built-in-models).

## Cloudoví poskytovatelé

### Anthropic (Claude)

- Klíč: <https://console.anthropic.com/> → API klíče.
- Základní adresa URL: `https://api.anthropic.com/` · Model: např. `claude-opus-4-8`.
- Schopnosti: webové vyhledávání + vize ve výchozím nastavení povoleny.

### OpenAI

- Klíč: <https://platform.openai.com/api-keys>.
- Základní adresa URL: `https://api.openai.com/v1/` · Model: např. `gpt-4o`.
- Druh: **OpenAiCompatible**. Povolte vizi v dialogu, pokud používáte model s viděním.

### Azure OpenAI

- Klíč + koncový bod: Portál Azure → váš prostředek Azure OpenAI.
- Základní adresa URL: `https://<resource>.openai.azure.com/` · Model: vaše **jméno nasazení**.
- Druh: **AzureOpenAi** (používá záhlaví `api-key` + dotaz `api-version` a cestu nasazení).

### Google Gemini

- Klíč: <https://aistudio.google.com/app/apikey>.
- Základní adresa URL: `https://generativelanguage.googleapis.com/` · Model: např. `gemini-2.0-flash`.
- Druh: **Gemini**. Ukotvení webového vyhledávání + vize ve výchozím nastavení povoleny.

### Ostatní kompatibilní cloudů OpenAI (OpenRouter, Groq, Together, Mistral, DeepSeek)

- Druh: **OpenAiCompatible**. Základní adresa URL = kompatibilní koncový bod OpenAI poskytovatele, Model = jeho ID modelu, ApiKey = klíč poskytovatele. Není potřeba žádná změna cMind — jeden adaptér slouží všem.

## Lokální modely (bez klíče)

Všechny lokální běhové prostředí zveřejňují drát OpenAI Chat Completions, takže používejte **Kind: OpenAiCompatible** se základní adresou URL běhového prostředí a podávaným jménem modelu; ponechte klíč prázdný.

### Ollama

```
# instalujte z https://ollama.com, poté:
ollama pull llama3.1:8b
```

- Základní adresa URL: `http://localhost:11434/v1/` · Model: vytažené jméno (např. `llama3.1:8b`, `qwen2.5-coder`).
- Žádný klíč API. Schopnosti se defaultují na pouze text; povolte vizi pouze pro model s viděním.

### LM Studio

- Spusťte místní server (Vývojář → Spustit server).
- Základní adresa URL: `http://localhost:1234/v1/` · Model: ID načteného modelu. Žádný klíč API.

### vLLM / llama.cpp `server` / LocalAI

- Poskytujte kompatibilní koncový bod OpenAI (každý dodává jeden).
- Základní adresa URL: vaše podávaná adresa URL (např. `http://localhost:8000/v1/`) · Model: podávané jméno modelu. Žádný klíč, pokud před ním neumístíte ověřování.

## Ověřování

- **Testovat připojení** v dialogu spouští malou ping kompletaci a hlásí úspěch + latenci — ideální pro potvrzení lokálního koncového bodu.
- Automatizované: sada E2E aplikace řídí každou funkci AI proti vestavěnému podvádějícímu kompatibilnímu serveru OpenAI ve výchozím nastavení, nebo vašemu reálnému poskytovateli, když je nastaveno `AI_E2E_BASEURL` (+ volitelné `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`). Viz [Funkce AI → Testování](../features/ai.md#testing-with-the-fake-local-llm).

## Přepínání / rotace

- **Přepnout aktivního poskytovatele:** Nastavení → AI → **Nastavit jako aktivní** na jiné kartě (aktivace jednoho deaktivuje ostatní).
- **Otočit klíč:** upravte poskytovatele a zadejte nový klíč (ponechte prázdný, aby se zachoval uložený).
- **Odebrat:** odstraňte kartu. Bez aktivního poskytovatele se funkce AI deaktivují a zbytek aplikace běží nezměněně.
