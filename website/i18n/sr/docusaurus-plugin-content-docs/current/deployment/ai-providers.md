---
description: "Каталаног подешавања за сваког добављача вештачке интелигенције који подржава cMind — Anthropic, OpenAI, Azure OpenAI, Google Gemini, и сваку OpenAI-компатибилну крајњу тачку укључујући локалне моделе (Ollama, LM Studio, vLLM, llama.cpp, LocalAI) и OpenAI-компатибилне облаке."
---

# Добављачи AI — каталог подешавања

AI слој cMind је независан од добављача (вид [AI карактеристике](../features/ai.md)). Конфигурирајте добављача на два начина:

1. **UI (власник):** Settings → AI → **Add provider** → изаберите врсту, базну URL, модел, кључ (опционално за локално), функционалности за укључивање, **Set active** → **Test connection**.
2. **Config/env (ops):** сеедујте `App:Ai:Providers[]` и `App:Ai:ActiveProvider` — увезено у складишту при првом стартовању када не постоје акредитиви. Пример (env, индекс добављача `0`):

   ```
   App__Ai__ActiveProvider=OpenAiCompatible
   App__Ai__Providers__0__Kind=OpenAiCompatible
   App__Ai__Providers__0__BaseUrl=http://localhost:11434/v1/
   App__Ai__Providers__0__Model=llama3.1:8b
   # App__Ai__Providers__0__ApiKey=...   (изоставите за безопасне локалне крајње тачке)
   ```

Тачно један добављач је активан у исто време. Кључи се чувају шифровано; локална крајња тачка не треба ниједна.

## Безбедност: http у односу на https

Обичан `http://` се прихвата **само** за loopback / приватне (intranet) домаћине — локални LLM случај (Ollama, LM Studio, vLLM, он-прем кутија). Било која домаћин рутабилна на јавном интернету **мора** бити `https://`, тако да API кључ никад није послан у јавности. Air-gapped/on-prem: упутите базну URL на вашу интерну крајњу тачку (loopback или приватну IP) и оставите кључ празан ако време извршавања није аутентификовано.

## Уграђена локална AI (ONNX, достављена)

cMind доставља **прави LLM у процесу** (Microsoft.ML.OnnxRuntimeGenAI) који је **подразумевано омогућен** — без кључа, без спољне услуге. При првом стартовању, када није конфигуриран ниједан добављач и `App:Branding:AllowBuiltInAi` је `true`, аутоматски се сеедује и активира.

- **Config:** `App:Ai:BuiltIn:Enabled` (подразумевано `true`), `App:Ai:BuiltIn:ModelPath` (подразумевано `models/onnx`, релативно према основном директоријуму апликације), `App:Ai:BuiltIn:MaxTokens` (подразумевано `1024`).
- **Датотеке модела:** упутите `ModelPath` на директоријум који садржи ONNX GenAI модел — `genai_config.json`, токенизер и тежине `.onnx`. CPU **Phi-3-mini** изградња добро функционише, на пример:

  ```bash
  pip install huggingface_hub
  huggingface-cli download microsoft/Phi-3-mini-4k-instruct-onnx \
    --include cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/* \
    --local-dir ./models
  # затим поставите App:Ai:BuiltIn:ModelPath нату фасциклу (садржи genai_config.json)
  ```

  Спакујте фасциклу са вашом сликом развоја / Helm волуменом, или га монтирајте у време извршавања. Када су датотеке одсутне, уграђена деградира на јасну поруку "модел није инсталиран" — апликација јако ради; конфигурирајте другог добављача или инсталирајте модел.
- **GPU:** замените CPU пакет/модел са CUDA/DirectML ONNX GenAI изградњом; путања кода је непромењена.

## White-label: ограничавање AI

Поставите под `App:Branding` (намештено на серверској страни — забрањена уметања враћа `400`):

- `AllowBuiltInAi: false` — уклоните целокупан уграђени модел.
- `AllowLocalProviders: false` — забраните локалне/сопствене хост крајње тачке (Ollama/LM Studio/vLLM и било коју loopback/приватну OpenAI-компатибилну URL).
- `AllowedAiProviderKinds: ["Anthropic","OpenAiCompatible"]` — дозволите само ове врсте (празно = све).

## Проширење са будућим уграђеним моделима

Слој добављача је заснован на адаптерима (`IAiProvider` кључан по `AiProviderKind`), тако да је будући уграђени модел време извршавања додато без додира било каквог AI карактеристике: додајте врсту, имплементирајте један адаптер, региструјте га. Уграђени ONNX је референтна имплементација. Вид [AI карактеристике → Проширење](../features/ai.md#extending-future-built-in-models).

## Добављачи облака

### Anthropic (Claude)

- Кључ: <https://console.anthropic.com/> → API keys.
- Базна URL: `https://api.anthropic.com/` · Модел: нпр. `claude-opus-4-8`.
- Могућности: web search + vision подразумевано.

### OpenAI

- Кључ: <https://platform.openai.com/api-keys>.
- Базна URL: `https://api.openai.com/v1/` · Модел: нпр. `gpt-4o`.
- Врста: **OpenAiCompatible**. Омогућите vision у дијалогу ако користите vision модел.

### Azure OpenAI

- Кључ + крајња тачка: Azure портал → ваш Azure OpenAI ресурс.
- Базна URL: `https://<resource>.openai.azure.com/` · Модел: ваше **име развоја**.
- Врста: **AzureOpenAi** (користи `api-key` заглавље + `api-version` упит и путања развоја).

### Google Gemini

- Кључ: <https://aistudio.google.com/app/apikey>.
- Базна URL: `https://generativelanguage.googleapis.com/` · Модел: нпр. `gemini-2.0-flash`.
- Врста: **Gemini**. Web-search grounding + vision подразумевано.

### Остали OpenAI-компатибилни облаци (OpenRouter, Groq, Together, Mistral, DeepSeek)

- Врста: **OpenAiCompatible**. Базна URL = OpenAI-компатибилна крајња тачка добављача, Модел = његов id модела, ApiKey = кључ добављача. Нема cMind промене потребне — један адаптер служи њима свима.

## Локални модели (без кључа)

Сво локално време извршавања изложи OpenAI Chat Completions жицу, тако да користи **Kind: OpenAiCompatible** са URL временом извршавања и послужену назив модела; оставите кључ празан.

### Ollama

```
# инсталирајте из https://ollama.com, затим:
ollama pull llama3.1:8b
```

- Базна URL: `http://localhost:11434/v1/` · Модел: извучено име (нпр. `llama3.1:8b`, `qwen2.5-coder`).
- Нема API кључа. Способности подразумевано текст-само; омогућите vision само за vision модел.

### LM Studio

- Почните локални сервер (Developer → Start server).
- Базна URL: `http://localhost:1234/v1/` · Модел: учитано id модела. Нема API кључа.

### vLLM / llama.cpp `server` / LocalAI

- Послужите OpenAI-компатибилну крајњу тачку (свака доставља једну).
- Базна URL: ваша послужена URL (нпр. `http://localhost:8000/v1/`) · Модел: послужено име модела. Нема кључа осим ако не ставите аутентификацију спреди.

## Верификација

- **Test connection** у дијалогу извршава мали ping завршетак и извештава успех + кашњење — идеално за потврду локалне крајње тачке.
- Аутоматизовано: апликација E2E пакета вози сваку AI карактеристику против LLM сервера у процесу подразумевано, или вашег правог добављача када су `AI_E2E_BASEURL` (+ опциона `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`) подешена. Вид [AI карактеристике → Testing](../features/ai.md#testing-with-the-fake-local-llm).

## Замена / ротација

- **Замена активног добављача:** Settings → AI → **Set active** на другој картици (активирање једне деактивира остале).
- **Ротирајте кључ:** уредите добављача и дајте нови кључ (оставите празно да задржите сачувани).
- **Уклоните:** обришите картицу. Без активног добављача, AI карактеристике оневозмогуће и остатак апликације ради без промена.
