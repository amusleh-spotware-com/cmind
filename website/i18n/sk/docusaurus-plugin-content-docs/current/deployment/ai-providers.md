---
description: "Katalóg nastavení pre každého poskytovania AI, ktorého cMind podporuje — Anthropic, OpenAI, Azure OpenAI, Google Gemini a každý kompatibilný koncový bod OpenAI vrátane lokálnych modelov (Ollama, LM Studio, vLLM, llama.cpp, LocalAI) a OpenAI-kompatibilných oblakov."
---

# Poskytovatelia AI — katalóg nastavení

Vrstva AI cMind je nezávislá na poskytovateľovi (pozrite si [funkcie AI](../features/ai.md)). Nakonfigurujte poskytovania dvoma spôsobmi:

1. **UI (vlastník):** Nastavenia → AI → **Pridať poskytovania** → vyberte typ, základnú URL, model, kľúč (voliteľne pre
   lokálne), prepínače schopností, **Nastaviť ako aktívne** → **Testovať pripojenie**.
2. **Config/env (ops):** semeno `App:Ai:Providers[]` a `App:Ai:ActiveProvider` — importované do úložiska
   pri prvom spustení, keď neexistujú žiadne poverenia. Príklad (env, index poskytovania `0`):

   ```
   App__Ai__ActiveProvider=OpenAiCompatible
   App__Ai__Providers__0__Kind=OpenAiCompatible
   App__Ai__Providers__0__BaseUrl=http://localhost:11434/v1/
   App__Ai__Providers__0__Model=llama3.1:8b
   # App__Ai__Providers__0__ApiKey=...   (vynechajte pre lokálne koncové body bez kľúča)
   ```

Presne jeden poskytovať je aktívny naraz. Kľúče sú uložené zašifrované; lokálny koncový bod nepotrebuje žiadny.

## Bezpečnosť: http vs https

Plaintext `http://` je akceptovaný **iba** pre loopback / privátne (intranet) hostiteľov — prípad lokálneho LLM
(Ollama, LM Studio, vLLM, on-prem krabica). Akýkoľvek hostiteľ dosiahniteľný na verejnom internete **musí** byť
`https://`, takže kľúč API sa nikdy nešíri v textovej forme. Air-gapped/on-prem: nasmerujte základnú URL na váš
interný koncový bod (loopback alebo privátna IP) a ponechajte kľúč prázdny, ak je runtime neautentifikovaný.

## Vstavaný lokálny AI (ONNX, dodaný)

cMind dodáva **reálny lokálny LLM v procese** (Microsoft.ML.OnnxRuntimeGenAI), ktorý je **povolený štandardne** — bez kľúča, bez externej služby. Pri prvom spustení, keď nie je nakonfigurovaný žiadny poskytovať a
`App:Branding:AllowBuiltInAi` je `true`, je automaticky osemený a aktivovaný.

- **Config:** `App:Ai:BuiltIn:Enabled` (štandardne `true`), `App:Ai:BuiltIn:ModelPath` (štandardne
  `models/onnx`, relatívne k základnému adresáru aplikácie), `App:Ai:BuiltIn:MaxTokens` (štandardne `1024`).
- **Súbory modelov:** nasmerujte `ModelPath` na adresár obsahujúci model ONNX GenAI — `genai_config.json`,
  tokenizer a váhy `.onnx`. Zostavo CPU **Phi-3-mini** funguje dobre, napr.:

  ```bash
  pip install huggingface_hub
  huggingface-cli download microsoft/Phi-3-mini-128k-instruct-onnx \
    --include cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/* \
    --local-dir ./models
  # potom nastavte App:Ai:BuiltIn:ModelPath na tento priečinok (obsahuje genai_config.json)
  ```

  Zabaľte priečinok s obrázkom nasadenia / Helm zväzkom, alebo ho namontujte za chodu. Keď sú súbory
  absentné, vstavaný degraduje na jasné "model nie je nainštalovaný" správy — aplikácia stále beží; nakonfigurujte
  iného poskytovania alebo nainštalujte model.
- **GPU:** vymeňte balík CPU/model za CUDA/DirectML ONNX GenAI zostavo; cesta kódu sa nemení.

## White-label: limitovanie AI

Nastavte pod `App:Branding` (vynútené na strane servera — zakázané upserty vrátia `400`):

- `AllowBuiltInAi: false` — odstránite dodaný vstavaný model úplne.
- `AllowLocalProviders: false` — zakážete lokálne/vlastné koncové body (Ollama/LM Studio/vLLM a akýkoľvek
  loopback/privátny OpenAI-kompatibilný URL).
- `AllowedAiProviderKinds: ["Anthropic","OpenAiCompatible"]` — povolte iba tieto typy (prázdne = všetky).

## Rozšírenie budúcimi vstavanými modelmi

Vrstva poskytovania je založená na adaptéroch (`IAiProvider` kľúčovaná podľa `AiProviderKind`), takže budúci vstavaný model
runtime sa pridá bez dotyku akejkoľvek funkcie AI: pridajte typ, implementujte jeden adaptér, zaregistrujte ho. 
Vstavaný ONNX je referenčná implementácia. Pozrite si [funkcie AI → Rozšírenie](../features/ai.md#extending-future-built-in-models).

## Cloud poskytovatelia

### Anthropic (Claude)

- Kľúč: <https://console.anthropic.com/> → API keys.
- Základná URL: `https://api.anthropic.com/` · Model: napr. `claude-opus-4-8`.
- Schopnosti: vyhľadávanie na webe + videnie štandardne zapnuté.

### OpenAI

- Kľúč: <https://platform.openai.com/api-keys>.
- Základná URL: `https://api.openai.com/v1/` · Model: napr. `gpt-4o`.
- Typ: **OpenAiCompatible**. Aktivujte videnie v dialógu, ak používate model s vídením.

### Azure OpenAI

- Kľúč + koncový bod: Azure portal → vaša Azure OpenAI zdroj.
- Základná URL: `https://<resource>.openai.azure.com/` · Model: vaše **názov nasadenia**.
- Typ: **AzureOpenAi** (používa hlavičku `api-key` + query `api-version` a cestu nasadenia).

### Google Gemini

- Kľúč: <https://aistudio.google.com/app/apikey>.
- Základná URL: `https://generativelanguage.googleapis.com/` · Model: napr. `gemini-2.0-flash`.
- Typ: **Gemini**. Web-search grounding + videnie štandardne zapnuté.

### Ďalší OpenAI-kompatibilný cloud (OpenRouter, Groq, Together, Mistral, DeepSeek)

- Typ: **OpenAiCompatible**. Základná URL = OpenAI-kompatibilný koncový bod poskytovania, Model = jeho ID modelu,
  ApiKey = kľúč poskytovania. Žiadna zmena cMind — jeden adaptér slúži všetkým.

## Lokálne modely (bez kľúča)

Všetky lokálne prostredia vystavujú drôtový protokol OpenAI Chat Completions, takže použite **Typ: OpenAiCompatible** s
základnou URL prostredia a osluhovým názvom modelu; ponechajte kľúč prázdny.

### Ollama

```
# nainštalujte z https://ollama.com, potom:
ollama pull llama3.1:8b
```

- Základná URL: `http://localhost:11434/v1/` · Model: ťahnutý názov (napr. `llama3.1:8b`, `qwen2.5-coder`).
- Žiadny kľúč API. Schopnosti sú štandardne iba text; povolte videnie iba pre model s vídením.

### LM Studio

- Spustite lokálny server (Developer → Start server).
- Základná URL: `http://localhost:1234/v1/` · Model: ID načítaného modelu. Žiadny kľúč API.

### vLLM / llama.cpp `server` / LocalAI

- Slúžte OpenAI-kompatibilný koncový bod (každý ich dodáva).
- Základná URL: vaša osluhovosť URL (napr. `http://localhost:8000/v1/`) · Model: osluhovný názov modelu. Žiadny kľúč
  pokiaľ vložíte auth dopredu.

## Overovanie

- **Testovať pripojenie** v dialógu spúšťa malé ping completion a oznamuje úspech + latencia — ideálne
  na potvrdenie lokálneho koncového bodu.
- Automatizované: balík E2E aplikácie spúšťa každú funkciu AI voči vstavanému falošnému OpenAI-kompatibilnému
  serveru štandardne, alebo vášmu reálnemu poskytovateľu, keď sú nastavené `AI_E2E_BASEURL` (+ voliteľne `AI_E2E_API_KEY` /
  `AI_E2E_KIND` / `AI_E2E_MODEL`). Pozrite si [funkcie AI → Testovanie](../features/ai.md#testing-with-the-fake-local-llm).

## Prepínanie / rotácia

- **Prepnúť aktívneho poskytovania:** Nastavenia → AI → **Nastaviť ako aktívne** na inej karte (aktivácia jedného deaktivuje
  zvyšok).
- **Otáčajte kľúč:** upravujte poskytovania a zadajte nový kľúč (ponechajte prázdny, aby ste udržali uložený).
- **Odstrániť:** odstránite kartu. Bez aktívneho poskytovania sú funkcie AI zakázané a zvyšok aplikácie beží
  nezmenený.
