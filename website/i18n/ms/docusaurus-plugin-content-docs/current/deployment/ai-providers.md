---
description: "Katalog persediaan untuk setiap penyedia AI yang disokong cMind — Anthropic, OpenAI, Azure OpenAI, Google Gemini, dan setiap titik akhir serasi OpenAI termasuk model lokal (Ollama, LM Studio, vLLM, llama.cpp, LocalAI) dan awan serasi OpenAI."
---

# Penyedia AI — katalog persediaan

Lapisan AI cMind adalah tidak bergantung pada penyedia (lihat [Ciri-ciri AI](../features/ai.md)). Konfigurasi penyedia dua cara:

1. **UI (pemilik):** Tetapan → AI → **Tambah penyedia** → pilih jenis, URL asas, model, kunci (pilihan untuk lokal), togol keupayaan, **Tetapkan aktif** → **Sambungan ujian**.
2. **Konfigurasi/env (operasi):** benih `App:Ai:Providers[]` dan `App:Ai:ActiveProvider` — diimport ke dalam kedai pada permulaan pertama apabila tiada kredensial wujud. Contoh (env, indeks penyedia `0`):

   ```
   App__Ai__ActiveProvider=OpenAiCompatible
   App__Ai__Providers__0__Kind=OpenAiCompatible
   App__Ai__Providers__0__BaseUrl=http://localhost:11434/v1/
   App__Ai__Providers__0__Model=llama3.1:8b
   # App__Ai__Providers__0__ApiKey=...   (abaikan untuk titik akhir lokal tanpa kunci)
   ```

Tepat satu penyedia aktif pada masa yang sama. Kunci disimpan terenkripsi; titik akhir lokal tidak perlukan apa-apa.

## Keselamatan: http vs https

Plaintext `http://` diterima **hanya** untuk hos loopback / pribadi (intranet) — kes LLM lokal (Ollama, LM Studio, vLLM, kotak pada premis). Mana-mana hos yang boleh diarah pada internet awam **mesti** `https://`, jadi kunci API tidak pernah dihantar dengan jelas. Udara-berkepung/pada premis: arahkan URL asas pada titik akhir dalaman anda (loopback atau IP peribadi) dan tinggalkan kunci kosong jika runtime tidak disahkan.

## AI lokal terbina dalam (ONNX, dikirim)

cMind menghantar **LLM lokal dalam proses sebenar** (Microsoft.ML.OnnxRuntimeGenAI) yang **didayakan secara lalai** — tiada kunci, tiada perkhidmatan luaran. Pada permulaan pertama, apabila tiada penyedia dikonfigurasi dan `App:Branding:AllowBuiltInAi` ialah `true`, ia ditanam dan diaktifkan secara automatik.

- **Konfigurasi:** `App:Ai:BuiltIn:Enabled` (lalai `true`), `App:Ai:BuiltIn:ModelPath` (lalai `models/onnx`, relatif kepada direktori asas apl), `App:Ai:BuiltIn:MaxTokens` (lalai `1024`).
- **Fail model:** arahkan `ModelPath` pada direktori yang mengandungi model GenAI ONNX — `genai_config.json`, tokenizer, dan berat `.onnx`. Binaan CPU **Phi-3-mini** berfungsi dengan baik, cth.:

  ```bash
  pip install huggingface_hub
  huggingface-cli download microsoft/Phi-3-mini-4k-instruct-onnx \
    --include cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/* \
    --local-dir ./models
  # kemudian tetapkan App:Ai:BuiltIn:ModelPath ke folder itu (mengandungi genai_config.json)
  ```

  Bundle folder dengan imej penempatan / volum Helm anda, atau pasangnya pada masa larian. Apabila fail tiada terbina dalam merosot kepada mesej jelas "model tidak dipasang" — apl masih berjalan; konfigurasi penyedia lain atau pasang model.
- **GPU:** tukar pakej/model CPU dengan binaan ONNX GenAI CUDA/DirectML; laluan kod tidak berubah.

## Label putih: mengehadkan AI

Tetapkan di bawah `App:Branding` (dikuatkuasakan sebelah pelayan — upsert yang dilarang mengembalikan `400`):

- `AllowBuiltInAi: false` — keluarkan model terbina dalam yang dikirim sepenuhnya.
- `AllowLocalProviders: false` — larang titik akhir lokal/hos diri (Ollama/LM Studio/vLLM dan mana-mana URL serasi OpenAI loopback/pribadi).
- `AllowedAiProviderKinds: ["Anthropic","OpenAiCompatible"]` — benarkan hanya jenis ini (kosong = semua).

## Memanjangkan dengan model terbina dalam masa depan

Lapisan penyedia adalah berasaskan adaptor (`IAiProvider` dikunci mengikut `AiProviderKind`), jadi runtime model terbina dalam masa depan ditambah tanpa menyentuh mana-mana ciri AI: tambah jenis, laksanakan satu adaptor, daftarkan. Terbina dalam ONNX adalah pelaksanaan rujukan. Lihat [Ciri-ciri AI → Memanjangkan](../features/ai.md#extending-future-built-in-models).

## Penyedia awan

### Anthropic (Claude)

- Kunci: <https://console.anthropic.com/> → kunci API.
- URL asas: `https://api.anthropic.com/` · Model: cth. `claude-opus-4-8`.
- Keupayaan: carian web + penglihatan secara lalai.

### OpenAI

- Kunci: <https://platform.openai.com/api-keys>.
- URL asas: `https://api.openai.com/v1/` · Model: cth. `gpt-4o`.
- Jenis: **OpenAiCompatible**. Dayakan penglihatan dalam dialog jika menggunakan model penglihatan.

### Azure OpenAI

- Kunci + titik akhir: portal Azure → sumber Azure OpenAI anda.
- URL asas: `https://<resource>.openai.azure.com/` · Model: nama **penempatan** anda.
- Jenis: **AzureOpenAi** (menggunakan tajuk `api-key` + pertanyaan `api-version` dan laluan penempatan).

### Google Gemini

- Kunci: <https://aistudio.google.com/app/apikey>.
- URL asas: `https://generativelanguage.googleapis.com/` · Model: cth. `gemini-2.0-flash`.
- Jenis: **Gemini**. Grounding carian web + penglihatan secara lalai.

### Awan serasi OpenAI lain (OpenRouter, Groq, Together, Mistral, DeepSeek)

- Jenis: **OpenAiCompatible**. URL asas = titik akhir serasi OpenAI penyedia, Model = id modelnya, ApiKey = kunci penyedia. Tiada perubahan cMind diperlukan — satu adaptor melayani semuanya.

## Model lokal (tiada kunci)

Semua runtime lokal mendedahkan pemasa Chat Completions OpenAI, jadi gunakan **Jenis: OpenAiCompatible** dengan URL asas runtime dan nama model yang dilayani; tinggalkan kunci kosong.

### Ollama

```
# pasang dari https://ollama.com, kemudian:
ollama pull llama3.1:8b
```

- URL asas: `http://localhost:11434/v1/` · Model: nama yang ditarik (cth. `llama3.1:8b`, `qwen2.5-coder`).
- Tiada kunci API. Keupayaan lalai ke teks sahaja; dayakan penglihatan hanya untuk model penglihatan.

### LM Studio

- Mulakan pelayan lokal (Pembangun → Mulakan pelayan).
- URL asas: `http://localhost:1234/v1/` · Model: id model yang dimuat. Tiada kunci API.

### vLLM / llama.cpp `server` / LocalAI

- Layani titik akhir serasi OpenAI (setiap kapal satu).
- URL asas: URL yang anda layani (cth. `http://localhost:8000/v1/`) · Model: nama model yang dilayani. Tiada kunci melainkan anda meletakkan auth di depan.

## Mengesahkan

- **Sambungan ujian** dalam dialog menjalankan kesempurnaan ping kecil dan melaporkan kejayaan + kependaman — ideal untuk mengesahkan titik akhir lokal.
- Otomatis: rangkaian E2E apl memacu setiap ciri AI terhadap pelayan palsu serasi OpenAI dalam proses secara lalai, atau penyedia nyata anda apabila `AI_E2E_BASEURL` (+ pilihan `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`) ditetapkan. Lihat [Ciri-ciri AI → Ujian](../features/ai.md#testing-with-the-fake-local-llm).

## Menukar / bergilir

- **Tukar penyedia aktif:** Tetapan → AI → **Tetapkan aktif** pada kad lain (mengaktifkan satu melumpuhkan yang lain).
- **Putar kunci:** sunting penyedia dan bekalkan kunci baru (tinggalkan kosong untuk menyimpan yang disimpan).
- **Keluarkan:** padamkan kad. Tanpa penyedia aktif, ciri-ciri AI melumpuh dan baki apl berjalan tanpa perubahan.
