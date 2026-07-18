---
description: "cMindin destekledigi her AI saglayicisi icin kurulum katalogu - Anthropic, OpenAI, Azure OpenAI, Google Gemini ve OpenAI-uyumlu her uc nokta dahil yerel modeller (Ollama, LM Studio, vLLM, llama.cpp, LocalAI) ve OpenAI-uyumlu bulutlar."
---

# AI Saglayicilari - Kurulum Katalogu

cMindin AI katmani saglayici-agnostiktir (bkz. [AI ozellikleri](../features/ai.md)). Bir saglayiciyi iki sekilde yapilandirin:

1. **UI (sahip):** Ayarlar - AI - **Saglayici ekle** - tur sec, base URL, model, anahtar (yerel icin istege bagli), yetenek anahtarlari, **Etkin ayarla** - **Baglantiiyi test et**.
2. **Yapilandirma/env (ops):** `App:Ai:Providers[]` ve `App:Ai:ActiveProvider` ile tohum ekle - kimlik bilgisi yokken ilk baslatmada storea aktarilir. Ornek (env, saglayici indeksi `0`):

   ```
   App__Ai__ActiveProvider=OpenAiCompatible
   App__Ai__Providers__0__Kind=OpenAiCompatible
   App__Ai__Providers__0__BaseUrl=http://localhost:11434/v1/
   App__Ai__Providers__0__Model=llama3.1:8b
   # App__Ai__Providers__0__ApiKey=...   (anahtarsiz yerel uc noktalar icin atlayin)
   ```

Ayni anda yalnizca bir saglayici aktiftir. Anahtarlar sifreli saklanir; yerel bir uc nokta hicbirine ihtiyac duymaz.

## Guvenlik: http vs https

Duz `http://` yalnizca geri dongu / ozel (intranet) ana bilgisayarlar icin kabul edilir - yerel-LLM durumu (Ollama, LM Studio, vLLM, sirket ici kutu). Genel internette yonlendirilebilir her ana bilgisayar **`https://`** olmalidir, boylece bir API anahtari acikta gonderilmez. Hava deligi/sirket ici: base URLyi dahili uc noktaniza (geri dongu veya ozel IP) yonlendirin ve calisma zamani kimlik dogrulamasi yoksa anahtari bos birakin.

## Yerlesik Yerel AI (ONNX, gonderilen)

cMind, **varsayilan olarak etkin** olan **gercek bir islem ici yerel LLM** (Microsoft.ML.OnnxRuntimeGenAI) gonderir - anahtar yok, harici hizmet yok. Ilk baslatmada, hicbir saglayici yapilandirilmamisken ve `App:Branding:AllowBuiltInAi` `true` iken, otomatik olarak tohum eklenir ve etkinlestirilir.

- **Yapilandirma:** `App:Ai:BuiltIn:Enabled` (varsayilan `true`), `App:Ai:BuiltIn:ModelPath` (varsayilan `models/onnx`, uygulama base dizinine gore), `App:Ai:BuiltIn:MaxTokens` (varsayilan `1024`).
- **Model dosyalari:** `ModelPath`i bir ONNX GenAI modeli iceren bir dizine isaret edin - `genai_config.json`, tokenizer ve `.onnx` agirliklari. Bir CPU **Phi-3.5-mini-instruct** derlemesi iyi calisir, or.:

  ```bash
  pip install huggingface_hub
  huggingface-cli download microsoft/Phi-3.5-mini-instruct-onnx \
    --include cpu_and_mobile/cpu-int4-awq-block-128-acc-level-4/* \
    --local-dir ./models
  # ardindan App:Ai:BuiltIn:ModelPathi o klasore ayarlayin (genai_config.json icerir)
  ```

  Klasoru dagitim goruntunuzle paketleyin / Helm volume olarak baglayin veya calisma zamaninda baglayin. Dosyalar yoksa yerlesik, net bir "model yuklenmemis" mesajina duser - uygulama yine calisir; baska bir saglayici yapilandirin veya modeli yukleyin.
- **GPU:** CPU paketi/modelini bir CUDA/DirectML ONNX GenAI derlemesiyle degistirin; kod yolu degismez.

## Beyaz Etiket: AIyi Sinirlama

`App:Branding` altinda ayarlayin (sunucu tarafinda zorlanir - yasak bir upsert `400` dondurur):

- `AllowBuiltInAi: false` - gonderilen yerlesik modeli tamamen kaldirin.
- `AllowLocalProviders: false` - yerel/sirket ici uc noktalari (Ollama/LM Studio/vLLM ve herhangi bir geri dongu/ozel OpenAI-uyumlu URL) yasaklayin.
- `AllowedAiProviderKinds: ["Anthropic","OpenAiCompatible"]` - yalnizca bu turlere izin verin (bos = hepsi).

## Gelecekteki Yerlesik Modelleri Genisletme

Saglayici katmani adaptör tabanlidir (`IAiProvider` `AiProviderKind` ile anahtarlanmis), dolayisiyla gelecekteki yerlesik bir model calisma zamani, herhangi bir AI ozelligine dokunmadan eklenir: bir tur ekleyin, bir adaptör uygulayin, kaydedin. ONNX yerlesik, referans uygulamadir. Bkz. [AI ozellikleri - Genisletme](../features/ai.md#extending-future-built-in-models).

## Bulut Saglayicilari

### Anthropic (Claude)

- Anahtar: <https://console.anthropic.com/> - API anahtarlari.
- Base URL: `https://api.anthropic.com/` - Model: or. `claude-opus-4-8`.
- Yetenekler: web aramasi + vizyon varsayilan olarak acik.

### OpenAI

- Anahtar: <https://platform.openai.com/api-keys>.
- Base URL: `https://api.openai.com/v1/` - Model: or. `gpt-4o`.
- Tur: **OpenAiCompatible**. Vizyon modeli kullaniyorsaniz dialogda vizyonu etkinlestirin.

### Azure OpenAI

- Anahtar + uc nokta: Azure portali - Azure OpenAI kaynaginiz.
- Base URL: `https://<resource>.openai.azure.com/` - Model: **deployment name**iniz.
- Tur: **AzureOpenAi** (`api-key` header + `api-version` sorgu ve deployment yolu kullanir).

### Google Gemini

- Anahtar: <https://aistudio.google.com/app/apikey>.
- Base URL: `https://generativelanguage.googleapis.com/` - Model: or. `gemini-2.0-flash`.
- Tur: **Gemini**. Web aramasi yerlestirme + vizyon varsayilan olarak acik.

### Diger OpenAI-Uyumlu Bulutlar (OpenRouter, Groq, Together, Mistral, DeepSeek)

- Tur: **OpenAiCompatible**. Base URL = saglayicinin OpenAI-uyumlu uc noktasi, Model = onun model idsi, ApiKey = saglayici anahtari. cMind degisikligi gerekmez - bir adaptör hepsine hizmet eder.

## Yerel Modeller (anahtarsiz)

Tum yerel calisma zamanlari OpenAI Chat Completions telini gosterir, bu nedenle **Kind: OpenAiCompatible** ve calisma zamaninin base URLsi ve sunulan model adi ile kullanin; anahtari bos birakin.

### Ollama

```
# https://ollama.com adresinden kurun, ardindan:
ollama pull llama3.1:8b
```

- Base URL: `http://localhost:11434/v1/` - Model: cekilen ad (orn. `llama3.1:8b`, `qwen2.5-coder`).
- API anahtari yok. Yetenekler varsayilan olarak yalnizca metin; yalnizca bir vizyon modeli icin vizyonu etkinlestirin.

### LM Studio

- Yerel sunucuyu baslatin (Gelistirici - Sunucu baslat).
- Base URL: `http://localhost:1234/v1/` - Model: yuklenen model id. API anahtari yok.

### vLLM / llama.cpp `server` / LocalAI

- OpenAI-uyumlu bir uc nokta sunun (her biri bir tane gonderir).
- Base URL: sunulan URLniz (orn. `http://localhost:8000/v1/`) - Model: sunulan model adi. Onune auth koymadysaniz anahtar gerekmez.

## Dogrulama

- Dialogdaki **Baglantiiyi test et** kucuk bir ping tamamlama calistirir ve basari + gecikme bildirir - yerel bir uc noktayi onaylamak icin ideal.
- Otomatik: uygulamanin E2E suiti, varsayilan olarak islem ici sahte OpenAI-uyumlu sunucuya karsi her AI ozelligini calistirir veya `AI_E2E_BASEURL` (+ istege bagli `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`) ayarlandiginda gercek saglayiciniza karsi calistirir. Bkz. [AI ozellikleri - Sahte Yerel LLM ile Test](../features/ai.md#testing-with-the-fake-local-llm).

## Gecis / Dondurme

- **Etkin saglayiciyi degistir:** Ayarlar - AI - baska bir kartta **Etkin ayarla** (biri etkinlestirildiginde digerleri deaktive olur).
- **Bir anahtari dondurun:** saglayiciyi duzenleyin ve yeni bir anahtar saglayin (saklanani korumak icin bos birakin).
- **Kaldir:** karti silin. Aktif saglayici yoksa AI ozellikleri devre disi kalir ve uygulamanin geri kalani degismeden calisir.
