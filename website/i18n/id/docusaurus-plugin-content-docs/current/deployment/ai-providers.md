---
description: "Setup catalog untuk setiap penyedia AI yang didukung cMind — Anthropic, OpenAI, Azure OpenAI, Google Gemini, dan setiap endpoint kompatibel OpenAI termasuk model lokal (Ollama, LM Studio, vLLM, llama.cpp, LocalAI) dan cloud kompatibel OpenAI."
---

# Penyedia AI — katalog setup

Lapisan AI cMind adalah provider-agnostic (lihat [Fitur AI](../features/ai.md)). Konfigurasi penyedia dua cara:

1. **UI (owner):** Settings → AI → **Tambahkan penyedia** → pilih kind, base URL, model, key (opsional untuk lokal), capability toggle, **Tetapkan aktif** → **Test koneksi**.
2. **Config/env (ops):** seed `App:Ai:Providers[]` dan `App:Ai:ActiveProvider` — diimport ke store pada startup pertama ketika tidak ada credential. Contoh (env, indeks penyedia `0`):

   ```
   App__Ai__ActiveProvider=OpenAiCompatible
   App__Ai__Providers__0__Kind=OpenAiCompatible
   App__Ai__Providers__0__BaseUrl=http://localhost:11434/v1/
   App__Ai__Providers__0__Model=llama3.1:8b
   # App__Ai__Providers__0__ApiKey=...   (omit untuk keyless local endpoint)
   ```

Tepat satu penyedia aktif pada saat waktu. Key disimpan terenkripsi; endpoint lokal tidak memerlukan apa pun.

## Keamanan: http vs https

Plaintext `http://` diterima **hanya** untuk loopback / private (intranet) host — kasus local-LLM
(Ollama, LM Studio, vLLM, box on-prem). Host apa pun yang routable di internet publik **harus**
`https://`, jadi API key tidak pernah dikirim dalam plaintext. Air-gapped/on-prem: arahkan base URL ke
endpoint internal Anda (loopback atau private IP) dan biarkan key kosong jika runtime tidak terautentikasi.

## Built-in local AI (ONNX, shipped)

cMind mengiri **real in-process local LLM** (Microsoft.ML.OnnxRuntimeGenAI) yang **enabled by
default** — tidak ada key, tidak ada layanan eksternal. Pada startup pertama, ketika tidak ada penyedia yang dikonfigurasi dan
`App:Branding:AllowBuiltInAi` adalah `true`, itu seeded dan activated secara otomatis.

- **Config:** `App:Ai:BuiltIn:Enabled` (default `true`), `App:Ai:BuiltIn:ModelPath` (default
  `models/onnx`, relative ke app base directory), `App:Ai:BuiltIn:MaxTokens` (default `1024`).
- **File model:** arahkan `ModelPath` ke direktori yang berisi model ONNX GenAI — `genai_config.json`,
  tokenizer, dan weight `.onnx`. Build CPU **Phi-3-mini** bekerja dengan baik, misalnya:

  ```bash
  pip install huggingface_hub
  huggingface-cli download microsoft/Phi-3-mini-4k-instruct-onnx \
    --include cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/* \
    --local-dir ./models
  # lalu atur App:Ai:BuiltIn:ModelPath ke folder itu (berisi genai_config.json)
  ```

  Bundle folder dengan image deployment Anda / Helm volume, atau mount pada runtime. Ketika file tidak ada, built-in merendah ke pesan yang jelas "model tidak diinstal" — aplikasi tetap berjalan; konfigurasi
  penyedia lain atau instal model.
- **GPU:** swap package CPU/model untuk build CUDA/DirectML ONNX GenAI; code path tidak berubah.

## White-label: membatasi AI

Tetapkan di bawah `App:Branding` (ditegakkan server-side — upsert terlarang mengembalikan `400`):

- `AllowBuiltInAi: false` — hapus model built-in yang dikirim sepenuhnya.
- `AllowLocalProviders: false` — larang endpoint lokal/self-hosted (Ollama/LM Studio/vLLM dan loopback/private URL kompatibel OpenAI apa pun).
- `AllowedAiProviderKinds: ["Anthropic","OpenAiCompatible"]` — izinkan hanya jenis ini (kosong = semua).

## Memperpanjang dengan model built-in masa depan

Lapisan penyedia adalah adapter-based (`IAiProvider` keyed oleh `AiProviderKind`), jadi runtime model built-in masa depan ditambahkan tanpa menyentuh fitur AI apa pun: tambahkan kind, implementasi satu adapter, register. Built-in ONNX adalah implementasi referensi. Lihat [Fitur AI → Memperpanjang](../features/ai.md#extending-future-built-in-models).

## Penyedia cloud

### Anthropic (Claude)

- Key: <https://console.anthropic.com/> → API key.
- Base URL: `https://api.anthropic.com/` · Model: misalnya `claude-opus-4-8`.
- Capability: web search + vision on by default.

### OpenAI

- Key: <https://platform.openai.com/api-keys>.
- Base URL: `https://api.openai.com/v1/` · Model: misalnya `gpt-4o`.
- Kind: **OpenAiCompatible**. Aktifkan vision dalam dialog jika menggunakan model vision.

### Azure OpenAI

- Key + endpoint: Azure portal → sumber daya Azure OpenAI Anda.
- Base URL: `https://<resource>.openai.azure.com/` · Model: **nama deployment** Anda.
- Kind: **AzureOpenAi** (menggunakan header `api-key` + query `api-version` dan path deployment).

### Google Gemini

- Key: <https://aistudio.google.com/app/apikey>.
- Base URL: `https://generativelanguage.googleapis.com/` · Model: misalnya `gemini-2.0-flash`.
- Kind: **Gemini**. Web-search grounding + vision on by default.

### Cloud lain yang kompatibel OpenAI (OpenRouter, Groq, Together, Mistral, DeepSeek)

- Kind: **OpenAiCompatible**. Base URL = endpoint kompatibel OpenAI penyedia, Model = id model-nya,
  ApiKey = key penyedia. Tidak ada perubahan cMind yang diperlukan — satu adapter melayani semua.

## Model lokal (tidak ada key)

Semua runtime lokal mengekspos OpenAI Chat Completions wire, jadi gunakan **Kind: OpenAiCompatible** dengan
base URL runtime dan nama model yang dilayani; biarkan key kosong.

### Ollama

```
# instal dari https://ollama.com, lalu:
ollama pull llama3.1:8b
```

- Base URL: `http://localhost:11434/v1/` · Model: nama yang ditarik (misalnya `llama3.1:8b`, `qwen2.5-coder`).
- Tidak ada API key. Capability default ke text-only; aktifkan vision hanya untuk model vision.

### LM Studio

- Mulai server lokal (Developer → Start server).
- Base URL: `http://localhost:1234/v1/` · Model: id model yang dimuat. Tidak ada API key.

### vLLM / llama.cpp `server` / LocalAI

- Layani endpoint kompatibel OpenAI (masing-masing mengirinya).
- Base URL: URL yang Anda layani (misalnya `http://localhost:8000/v1/`) · Model: nama model yang dilayani. Tidak ada key
  kecuali Anda menempatkan auth di depan.

## Memverifikasi

- **Test connection** dalam dialog menjalankan ping completion kecil dan reports success + latency — ideal
  untuk mengkonfirmasi endpoint lokal.
- Otomatis: suite E2E aplikasi mendorong setiap fitur AI terhadap server OpenAI-compatible fake in-process by default, atau penyedia real Anda ketika `AI_E2E_BASEURL` (+ opsional `AI_E2E_API_KEY` /
  `AI_E2E_KIND` / `AI_E2E_MODEL`) diatur. Lihat [Fitur AI → Testing](../features/ai.md#testing-with-the-fake-local-llm).

## Switching / rotating

- **Switch penyedia aktif:** Settings → AI → **Set active** pada kartu lain (mengaktifkan yang satu menonaktifkan
  sisanya).
- **Rotate key:** edit penyedia dan supply key baru (biarkan kosong untuk menjaga yang disimpan).
- **Hapus:** hapus kartu. Tanpa penyedia aktif, fitur AI disable dan sisa aplikasi berjalan
  tidak berubah.
