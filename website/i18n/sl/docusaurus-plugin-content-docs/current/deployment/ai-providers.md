---
description: "Katalog nastavitve za vsakega ponudnika AI, ki ga cMind podpira — Anthropic, OpenAI, Azure OpenAI, Google Gemini, in vsak konec OpenAI-kompatibilen, vključno z lokalnimi modeli (Ollama, LM Studio, vLLM, llama.cpp, LocalAI) in oblaka-kompatibilne OpenAI."
---

# Ponudniki AI — katalog nastavitve

Sloj AI cMind je neodvisen od ponudnika (glej [Značilnosti AI](../features/ai.md)). Nastavite ponudnika na dva načina:

1. **UI (lastnik):** Nastavitve → AI → **Dodaj ponudnika** → izberi vrsto, osnovna URL, model, ključ (neobvezno za
   lokalnega), zatikače zmožnosti, **Nastavi aktivnega** → **Testiraj povezavo**.
2. **Konfiguracija/env (ops):** seed `App:Ai:Providers[]` in `App:Ai:ActiveProvider` — uvoženi v trgovino
   pri prvem zagonu, ko ne obstajajo poverilnice. Primer (env, indeks ponudnika `0`):

   ```
   App__Ai__ActiveProvider=OpenAiCompatible
   App__Ai__Providers__0__Kind=OpenAiCompatible
   App__Ai__Providers__0__BaseUrl=http://localhost:11434/v1/
   App__Ai__Providers__0__Model=llama3.1:8b
   # App__Ai__Providers__0__ApiKey=...   (izpustiti za breključni lokalni končni točki)
   ```

Natanko en ponudnik je v nekem trenutku aktiven. Ključi so shranjeni šifrirani; lokalnega končnega točka ne potrebuje nobenega.

## Varnost: http vs https

Golo besedilo `http://` je sprejeto **samo** za zaključne/zasebne (intranetne) gostiteljeve — primer lokalnega-LLM
(Ollama, LM Studio, vLLM, na-premičnega polja). Vsak gostitelj, ki je usmerljiv na javnem internetu **mora biti**
`https://`, zato se ključ API nikoli ne pošlje v jasnem besedilu. Zrak-gapiran/na-premičnem: označite osnovno URL v
interno končno točko (zaključek ali zasebni IP) in pustite ključ prazen, če je čas izvajanja neoverjen.

## Vgrajeni lokalni AI (ONNX, dobavljen)

cMind oddaja **pravi proces-lokalni LLM** (Microsoft.ML.OnnxRuntimeGenAi) ki je **omogočen
privzeto** — nobenega ključa, nobenega zunanjega servisa. Pri prvem zagonu, ko ni nobenega ponudnika
konfiguriranega in je `App:Branding:AllowBuiltInAi` `true`, se seed in aktivira avtomatično.

- **Konfiguracija:** `App:Ai:BuiltIn:Enabled` (privzeto `true`), `App:Ai:BuiltIn:ModelPath` (privzeto
  `models/onnx`, relativno na osnovno direktorije aplikacije), `App:Ai:BuiltIn:MaxTokens` (privzeto `1024`).
- **Datoteke modela:** imenik `ModelPath` na direktoriju, ki vsebuje model ONNX GenAi — `genai_config.json`,
  tokenizer in uteži `.onnx`. Gradnja CPU **Phi-3-mini** dobro deluje, npr.:

  ```bash
  pip install huggingface_hub
  huggingface-cli download microsoft/Phi-3-mini-4k-instruct-onnx \
    --include cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/* \
    --local-dir ./models
  # nato nastavite App:Ai:BuiltIn:ModelPath na ta mapa (vsebuje genai_config.json)
  ```

  Zavit mapi s sliko uvrstitve/Helm glasnostjo ali ga priklopite pri času izvajanja. Ko so datoteke
  odsotne se vgrajeni degradira na jasno sporočilo "model ni nameščen" — aplikacija še vedno teče; nastavite
  drugega ponudnika ali namestite model.
- **GPU:** zamenjajte CPU paket/model z CUDA/DirectML ONNX GenAi gradnjo; pot kode je nespremenjeno.

## Bela etiketa: omejevanje AI

Nastavi pod `App:Branding` (vsiljeno na strani strežnika — prepovedana upserta vrne `400`):

- `AllowBuiltInAi: false` — odstrani odpremljeni vgrajeni model v celoti.
- `AllowLocalProviders: false` — prepovedaj lokalne/samogostovane končne točke (Ollama/LM Studio/vLLM in vsako
  zaključek/zasebno OpenAI-kompatibilen URL).
- `AllowedAiProviderKinds: ["Anthropic","OpenAiCompatible"]` — dovoli samo te vrste (prazno = vsi).

## Razširitev z bodočimi vgrajenimi modeli

Sloj ponudnika je temelj adaptator (`IAiProvider` ključan po `AiProviderKind`), zato je prihodnji vgrajeni model
čas izvajanja se doda brez dotika funkcije AI: dodajte vrsto, implementirajte en adapter, ga registrirajte. ONNX
vgrajeni je referenčna implementacija. Glej [Značilnosti AI → Razširitev](../features/ai.md#extending-future-built-in-models).

## Ponudniki oblaka

### Anthropic (Claude)

- Ključ: <https://console.anthropic.com/> → Ključi API.
- Osnovna URL: `https://api.anthropic.com/` · Model: npr. `claude-opus-4-8`.
- Zmožnosti: spletna iskanja + vid privzeto vplivna.

### OpenAI

- Ključ: <https://platform.openai.com/api-keys>.
- Osnovna URL: `https://api.openai.com/v1/` · Model: npr. `gpt-4o`.
- Vrsta: **OpenAiCompatible**. Omogočite vid v dialoga, če uporabljate vid model.

### Azure OpenAI

- Ključ + končna točka: Azure portal → vaš Azure OpenAI vir.
- Osnovna URL: `https://<resource>.openai.azure.com/` · Model: vaše **ime uvrstitve**.
- Vrsta: **AzureOpenAi** (uporablja glavo `api-key` + `api-version` poizvedba in pot uvrstitve).

### Google Gemini

- Ključ: <https://aistudio.google.com/app/apikey>.
- Osnovna URL: `https://generativelanguage.googleapis.com/` · Model: npr. `gemini-2.0-flash`.
- Vrsta: **Gemini**. Spletna iskanja uzemljitev + vid privzeto vplivna.

### Drugi OpenAI-kompatibilni oblaki (OpenRouter, Groq, Together, Mistral, DeepSeek)

- Vrsta: **OpenAiCompatible**. Osnovna URL = OpenAI-kompatibilna končna točka ponudnika, Model = njegov ID modela,
  ApiKey = ključ ponudnika. Nobenega cMind sprememba potrebna — en adapter jim služi vsem.

## Lokalni modeli (brez ključa)

Vsi lokalnih časih izvajanja izpostavljajo žice OpenAI Chat Completions, zato uporabite **Vrsta: OpenAiCompatible** z
osnovno URL časa izvajanja in posledičnim modelom; pustite ključ prazen.

### Ollama

```
# namestite iz https://ollama.com, nato:
ollama pull llama3.1:8b
```

- Osnovna URL: `http://localhost:11434/v1/` · Model: potegnjen ime (npr. `llama3.1:8b`, `qwen2.5-coder`).
- Nobenega ključa API. Zmožnosti privzeto samo za besedilo; omogočite vid samo za vid model.

### LM Studio

- Zaženite lokalni strežnik (Razvojnik → Začni strežnik).
- Osnovna URL: `http://localhost:1234/v1/` · Model: naloženi ID modela. Nobenega ključa API.

### vLLM / llama.cpp `server` / LocalAI

- Služi OpenAI-kompatibilna končna točka (vsakega je enega).
- Osnovna URL: vaša služena URL (npr. `http://localhost:8000/v1/`) · Model: служен ime modela. Nobenega ključa
  razen, če postavite prev pred.

## Potrditev

- **Testiraj povezavo** v dialoga teče majhno ping dokončanje in javi uspeh + zakasnitev — idealno
  za potrditev lokalnega končnega točke.
- Avtomatizirani: aplikacijo E2E niz teče vsako značilnost AI proti procesu-lokalnega falš OpenAI-kompatibilna
  strežnik privzeto, ali vaš pravi ponudnik, ko je `AI_E2E_BASEURL` (+ izbirni `AI_E2E_API_KEY` /
  `AI_E2E_KIND` / `AI_E2E_MODEL`) nabor. Glej [Značilnosti AI → Testiranje](../features/ai.md#testing-with-the-fake-local-llm).

## Preklapljanje / vrtenje

- **Preklopite aktivnega ponudnika:** Nastavitve → AI → **Nastavi aktivnega** na drugem kartici (aktiviranje enega
  deaktivira preostanek).
- **Vrtenje ključa:** uredite ponudnika in navedite nov ključ (pustite prazen, da obdržite shranjenega).
- **Odstrani:** izbriši kartico. Brez aktivnega ponudnika so značilnosti AI onemogočene in se preostanek aplikacije teče
  nespremenjeno.
