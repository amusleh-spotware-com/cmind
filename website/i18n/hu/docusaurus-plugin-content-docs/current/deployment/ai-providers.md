---
title: AI szolgaltatok - beallitasi katalogus
description: "Beallitasi katalogus minden AI szolgaltatohoz, amelyet a cMind tamogat - Anthropic, OpenAI, Azure OpenAI, Google Gemini, es minden OpenAI-kompatibilis vegpont, beleertve a helyi modelleket."
---

# AI szolgaltatok - beallitasi katalogus

A cMind AI retege szolgaltatofuggetlen (laskad [AI funkciok](../features/ai.md)). Konfigurálj egy szolgaltatot ket modon:

1. **UI (tulajdonos):** Beallitasok → AI → **Add provider** → valassz fajat, base URL-t, modellt, kulcsot (opcionalis helyi vagy kulcs nelkuli), kepesseg kapcsolokat, **Beallitas aktívként** → **Teszteljes kapcsolatot**.
2. **Konfig/kornyezet (ops):** seed `App:Ai:Providers[]` es `App:Ai:ActiveProvider` - importalva a store-ba az elso inditaskan, amikor nincs meg hitelesito adat. Pelda (kornyezet, szolgaltato index 0):

   ```
   App__Ai__ActiveProvider=OpenAiCompatible
   App__Ai__Providers__0__Kind=OpenAiCompatible
   App__Ai__Providers__0__BaseUrl=http://localhost:11434/v1/
   App__Ai__Providers__0__Model=llama3.1:8b
   # App__Ai__Providers__0__ApiKey=...   (hagyd ki kulcs nelkuli helyi vegpontoknal)
   ```

Pontosan egy szolgaltato aktív egyszerre. A kulcsok titkositva tarolodnak; egy helyi vegpontnak nem kell kulcs.

## Biztonsag: http vs https

A purlic text `http://` csak loopback / privat (intranet) hostokhoz elfogadott - a helyi-LLM esethez (Ollama, LM Studio, vLLM, egy on-prem gep). Bármely, a public interneten rutalható hostnak `https://`-nek kell lennie, igy egy API kulcs sosem megy titkositatlanul. Air-gapped/on-prem: ird a base URL-t a belso vegpontra (loopback vagy privat IP) es hagyd a kulcsot uresen, ha a runtime nem autentikalt.

## Beepitett helyi AI (ONNX, szallitva)

A cMind egy **valo folyamatban levo helyi LLM-t** szallit (Microsoft.ML.OnnxRuntimeGenAI), amely **alapertelmezes szerint be van kapcsolva** - nincs kulcs, nincs kulso szolgalatas. Az elso inditaskan, amikor nincs szolgaltato konfiguralva es az `App:Branding:AllowBuiltInAi` `true`, automatikusan seedelesre es aktivaclasra kerul.

- **Konfig:** `App:Ai:BuiltIn:Enabled` (alapertelmezett `true`), `App:Ai:BuiltIn:ModelPath` (alapertelmezett `models/onnx`, az alkalmazas alapkonyvtarahoz viszonyitva), `App:Ai:BuiltIn:MaxTokens` (alapertelmezett `1024`).
- **Modell fajlok:** ird a `ModelPath`-ot egy konyvtarra, ami egy ONNX GenAI modellt tartalmaz - `genai_config.json`, a tokenizer es a `.onnx` sulyok. Egy CPU **Phi-3-mini** build jól működik, pl.:

  ```bash
  pip install huggingface_hub
  huggingface-cli download microsoft/Phi-3-mini-4k-instruct-onnx     --include cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/*     --local-dir ./models
  # aztán állítsd az App:Ai:BuiltIn:ModelPath-ot erre a mappára (tartalmazza a genai_config.json-t)
  ```

  Bundolj a mappát a telepitési image-be / Helm volume-ba, vagy mountold futásidoben. Amikor a fajlok hianyoznak, a beepitett egy tiszta "modell nincs telepitve" uzenetre degradalodik - az alkalmazas meg fut; konfigurálj mas szolgaltatot vagy telepitsd a modellt.
- **GPU:** csereld a CPU csomagot/modellt egy CUDA/DirectML ONNX GenAI build-re; a kod utvonlat valtozatlan.

## White-label: AI korlatozasa

Allitsd az `App:Branding` alatt (szerveroldalon kényszeritve - egy tiltott beszuras `400`-et ad):

- `AllowBuiltInAi: false` - teljesen eltavolítja a szallitott beepitett modellt.
- `AllowLocalProviders: false` - tiltja a helyi/self-hosted vegpontokat (Ollama/LM Studio/vLLM es barmely loopback/privat OpenAI-kompatibilis URL).
- `AllowedAiProviderKinds: ["Anthropic","OpenAiCompatible"]` - csak ezeket a fajtakat engedelyezi (ures = mind).

## Bovites jovo beli beepitett modellekkel

A szolgaltato reteg illesztoalapu (`IAiProvider` kulcsolva `AiProviderKind` altal), igy egy jovo beli beepitett modell futasido hozzaadasa erintés nélkül az AI funkciokat: add egy fajat, implementalj egy adaptert, regisztrald. Az ONNX beepitett a referenciaja ennek a mintának. Laskad [AI funkciok - Bovites](../features/ai.md#extending-future-built-in-models).

## Felhos szolgaltatok

### Anthropic (Claude)

- Kulcs: <https://console.anthropic.com/> → API keys.
- Base URL: `https://api.anthropic.com/` · Modell: pl. `claude-opus-4-8`.
- Kepessegek: webes kereses + latas alapertelmezes szerint be.

### OpenAI

- Kulcs: <https://platform.openai.com/api-keys>.
- Base URL: `https://api.openai.com/v1/` · Modell: pl. `gpt-4o`.
- Fajta: **OpenAiCompatible**. Engedelyezd a latast a dialogusban, ha lat modellt hasznalsz.

### Azure OpenAI

- Kulcs + vegpont: Azure portal → az Azure OpenAI erőforrasod.
- Base URL: `https://<resource>.openai.azure.com/` · Modell: a te **deployment neved**.
- Fajta: **AzureOpenAi** (hasznalja az `api-key` headert + `api-version` query-t es a deployment utvonalat).

### Google Gemini

- Kulcs: <https://aistudio.google.com/app/apikey>.
- Base URL: `https://generativelanguage.googleapis.com/` · Modell: pl. `gemini-2.0-flash`.
- Fajta: **Gemini**. Webes kereses grounding + latas alapertelmezes szerint be.

### Egyeb OpenAI-kompatibilis felhők (OpenRouter, Groq, Together, Mistral, DeepSeek)

- Fajta: **OpenAiCompatible**. Base URL = a szolgaltato OpenAI-kompatibilis vegpontja, Modell = az ennek modell ID-je, ApiKey = a szolgaltato kulcsa. Nem kell cMind valtoztatas - egy adapter szolgal mindet.

## Helyi modellek (nincs kulcs)

Minden helyi futasido kiteszi az OpenAI Chat Completions wire-t, igy használd a **Fajta: OpenAiCompatible** a futasido base URL-jével es a servelt modell nevével; hagyd a kulcsot uresen.

### Ollama

```
# telepites: https://ollama.com, aztán:
ollama pull llama3.1:8b
```

- Base URL: `http://localhost:11434/v1/` · Modell: a pullolt nev (pl. `llama3.1:8b`, `qwen2.5-coder`).
- Nincs API kulcs. A kepessegek alapertelmezes szerint szoveg-only; engedelyezd a latast csak lat modellnel.

### LM Studio

- Inditsd a helyi szervert (Developer → Start server).
- Base URL: `http://localhost:1234/v1/` · Modell: a betoltott modell ID-je. Nincs API kulcs.

### vLLM / llama.cpp `server` / LocalAI

- Szolgaltass egy OpenAI-kompatibilis vegpontot (mindegyik szallit egyet).
- Base URL: a te servelt URL-ed (pl. `http://localhost:8000/v1/`) · Modell: a servelt modell neve. Nincs kulcs, hacsak nem raksz auth-ot ele.

## Ellenorzes

- **Teszteljes kapcsolatot** a dialogusban futtat egy mini ping completion-t es jelenti a sikert + latenciat - idealis a helyi vegpont megerositesere.
- Automatizált: az alkalmazas E2E suite-e minden AI funkciot hajt egy folyamatban levo hamis OpenAI-kompatibilis szerver ellen alapertelmezes szerint, vagy a te valodi szolgaltatodat, amikor az `AI_E2E_BASEURL` (+ opcionalis `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`) be van allitva. Laskad [AI funkciok - Teszteles](../features/ai.md#testing-with-the-fake-local-llm).

## Valtas / rotalas

- **Kapcsold at az aktiv szolgaltatot:** Beallitasok → AI → **Beallitas aktívként** egy masik kartyan (egy aktivalsa deaktiválja a masikat).
- **Rotald a kulcsot:** szerkesztd a szolgaltatot es add meg az uj kulcsot (hagyd üresen a meglevo megtartasahoz).
- **Torles:** torold a kartyat. Aktiv szolgaltato nelkul az AI funkciok letiltodnak es az alkalmazas tobbi reze valtozatlanul fut.
