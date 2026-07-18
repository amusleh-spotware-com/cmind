---
description: "cMind AI adalah provider-agnostic — Anthropic, OpenAI, Azure OpenAI, Google Gemini, dan endpoint compatible OpenAI apa pun termasuk model lokal (Ollama, LM Studio, vLLM). Pilih provider, model, dan endpoint; setiap fitur AI bekerja tanpa berubah."
---

# Fitur AI

Lapisan AI cMind bersifat **provider-agnostic**. Setiap fitur berbicara kepada sebuah seam netral provider tunggal
(`IAiClient.CompleteAsync`); sebuah **klien routing** menyelesaikan kredensial provider aktif dan mendistribusikan
ke adapter kawat yang sesuai. Anda memilih provider + model + endpoint (dan, jika provider membutuhkannya,
sebuah kunci); setiap fitur yang ada bekerja tanpa perubahan dengan gating, enkripsi, resiliensi, dan
degradasi yang sama.

**Baterai termasuk:** sebuah **LLM lokal bawaan dikirim dengan aplikasi dan diaktifkan secara default**
(Microsoft.ML.OnnxRuntimeGenAI, misalnya Phi-3-mini) — jadi setiap deployment memiliki AI yang bekerja **tanpa API key
dan tanpa layanan eksternal**. Deployment white-label dapat menghapusnya dan membatasi provider mana yang dapat
ditambahkan pengguna. Selain yang bawaan, hubungkan provider eksternal apa pun.

Provider yang didukung:

- **AI lokal bawaan** (`BuiltInOnnx`) — model GenAI ONNX dalam proses, tanpa kunci, dikirim + default-on.
- **Anthropic** (Claude — Messages API)
- **OpenAI** dan **Azure OpenAI** (Chat Completions)
- **Google Gemini** (`generateContent`)
- **Endpoint compatible OpenAI apa pun**, termasuk **model lokal** (Ollama, LM Studio, vLLM,
  llama.cpp `server`, LocalAI) dan cloud compatible OpenAI (OpenRouter, Groq, Together, Mistral,
  DeepSeek) — semua melalui satu adapter compatible OpenAI, berbeda hanya berdasarkan base URL + model + kunci.

Tepat **satu** provider aktif pada satu waktu. Kredensial disimpan **terenkripsi**
(`AiProviderCredential` aggregate + `IAiProviderStore` + `ISecretProtector`, `EncryptionPurposes.AiApiKey`);
endpoint lokal membutuhkan **tidak ada kunci**. Dengan **tidak ada** provider aktif, setiap fitur mengembalikan hasil yang dinonaktifkan dan sisa aplikasi berjalan tanpa perubahan (tidak ada kunci yang dibutuhkan untuk membangun, menguji, atau menjalankan platform).

**Back-compat:** deployment yang ada dengan legacy `App:Ai:ApiKey` (atau pengaturan `ai.api_key` terenkripsi lama) dihormati secara otomatis sebagai default active **Anthropic** provider — tidak ada tindakan yang diperlukan.

AI tidak dikonfigurasi → halaman AI menonaktifkan tindakan dan menampilkan banner ditambah prompt sekali saja untuk menambahkan provider di
**Settings → AI** (`AiFeatureNotice`). Status di `GET /api/ai/status` (`{ enabled, kind, model }`);
provider dikelola (hanya pemilik) melalui `GET/PUT /api/ai/providers`, `POST /api/ai/providers/{id}/activate`,
`DELETE /api/ai/providers/{id}`, dan ping konektivitas `POST /api/ai/providers/test`.

## Default deployment vs provider pengguna sendiri

Kredensial AI memiliki dua cakupan:

- **Default deployment (dikelola pemilik).** Pemilik mengonfigurasi provider (atau mengirim satu melalui
  `App:Ai:Providers[]` / legacy `App:Ai:ApiKey`). Ini menjadi **default bersama untuk setiap pengguna** —
  jadi broker atau penyedia hosting dapat mendanai AI untuk semua pengguna mereka dengan **tidak ada pengaturan per pengguna dan tidak ada
  batas per pengguna**. Dikelola melalui rute `/api/ai/providers` khusus pemilik di atas.
- **Provider pengguna sendiri (self-service).** Setiap pengguna yang masuk dapat menambahkan provider mereka sendiri di bawah
  `GET/PUT /api/ai/my-providers`, `POST /api/ai/my-providers/{id}/activate`,
  `DELETE /api/ai/my-providers/{id}`. Ketika ada, **provider aktif mereka sendiri mengganti default deployment
  untuk fitur AI mereka sendiri**; menghapusnya kembali ke default.

**Urutan resolusi** (di `AiProviderStore`, per pengguna permintaan): kredensial aktif pengguna sendiri → default deployment → kunci config legacy → none (AI dinonaktifkan). Tepat satu kredensial aktif
**per cakupan** (indeks unik parsial per `OwnerUserId`), dan setiap cakupan diselesaikan secara independen, jadi pengguna mengaktifkan kunci mereka sendiri tidak pernah mengganggu default bersama. Konteks latar belakang/non-Web (tidak ada pengguna permintaan) selalu menyelesaikan default deployment.

## Matriks kemampuan provider

Kemampuan default per provider dan dapat ditimpa pemilik. Ketika kemampuan dimatikan fitur
**merosot, tidak pernah melempar**: pencarian web dilepas secara diam-diam; visi mengembalikan typed
kegagalan ketidakdukung kemampuan.

| Provider | Jenis | URL dasar default | Kunci diperlukan | Pencarian web | Visi | Catatan |
|---|---|---|---|---|---|---|
| AI lokal bawaan | `BuiltInOnnx` | n/a (dalam proses) | tidak | ✖ | ✖ | model ONNX GenAI yang dikirim, default-on |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | ya | ✅ | ✅ | Messages API, alat `web_search` |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | ya | opt-in | opt-in | Chat Completions |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | ya | ✅ | ✅ | path deployment + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | ya | ✅ | ✅ | `generateContent`, grounding `google_search` |
| Ollama (lokal) | `OpenAiCompatible` | `http://localhost:11434/v1/` | tidak | ✖ | bergantung model | melalui adapter compatible OpenAI |
| LM Studio (lokal) | `OpenAiCompatible` | `http://localhost:1234/v1/` | tidak | bergantung model | bergantung model | melalui adapter compatible OpenAI |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | URL yang Anda layani | tidak | ✖ | bergantung model | melalui adapter compatible OpenAI |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | URL provider | ya | ✖ | bergantung model | melalui adapter compatible OpenAI |

Panduan setup lengkap per provider (kunci, URL, id model, langkah UI): lihat
[AI providers — katalog setup](../deployment/ai-providers.md).

## AI lokal bawaan (dikirim, default-on)

cMind mengirim **LLM lokal nyata yang berjalan dalam proses** melalui
[Microsoft.ML.OnnxRuntimeGenAI](https://onnxruntime.ai/docs/genai/) (model instruct kompak seperti
Phi-3-mini). Tidak membutuhkan **kunci API dan layanan eksternal**, dan pada startup pertama — ketika tidak ada provider yang dikonfigurasi dan gating white-label memungkinkannya — ia **disemai dan diaktifkan secara otomatis**, jadi setiap deployment memiliki AI yang bekerja di luar kotak.

- Direktori model (`genai_config.json` + tokenizer + bobot) dikonfigurasi oleh
  `App:Ai:BuiltIn:ModelPath` (default `models/onnx`, relatif ke direktori dasar aplikasi). Ketika file model tidak ada provider **merosot menjadi kegagalan typed dengan petunjuk install** — tidak pernah melempar, dan sisa aplikasi tidak terpengaruh.
- Ini menggerakkan setiap fitur AI teks. Menjadi model kompak, ia hanya teks (tidak ada pencarian web sisi server atau visi) dan generasi diserialisasi (satu instance model, digunakan kembali setelah lazy load).
- **Beberapa model bawaan dapat hidup berdampingan.** Setiap model yang diunduh tinggal di bawah `ModelPath/<key>`; katalog yang dikurasi (Phi-3.5-mini default, ditambah Phi-3-mini-128k) dapat diunduh dan dialihkan dari **Settings → AI**. Memilih submodel bawaan memuat dalam proses. Akuisisi/bundel model: lihat [AI providers → built-in](../deployment/ai-providers.md#built-in-local-ai-onnx-shipped).

## Kontrol white-label

Deployment white-label membatasi AI melalui `App:Branding` (diterapkan sisi server pada setiap upsert provider):

- `AllowBuiltInAi` (default `true`) — setel `false` untuk **menghapus model bawaan** sepenuhnya.
- `AllowLocalProviders` (default `true`) — setel `false` untuk melarang endpoint lokal/self-hosted (loopback /
  private OpenAI-compatible, misalnya Ollama/LM Studio/vLLM).
- `AllowedAiProviderKinds` (default kosong = semua) — daftar hanya jenis yang deployment setujui (misalnya
  `["Anthropic","OpenAiCompatible"]`) untuk mengunci provider mana yang dapat ditambahkan pengguna.
- `AllowAiTasks` (default `true`) — setel `false` untuk menghapus fitur **tugas AI latar belakang** (halaman `/ai/tasks` dan API tugas mengembalikan 404; pelari berhenti mengklaim); fitur AI sinkron masih bekerja.
- `AllowAiModelManagement` (default `true`) — setel `false` untuk menyembunyikan **menjelajahi model** dan **pengikatan model per fitur**. Keduanya dapat disesuaikan pemilik pada waktu runtime dari **Settings → Deployment** (overlay live pada `IOptionsMonitor`) dan dikatalog dalam `WhiteLabelCatalog`.

## Memperluas: model bawaan masa depan

Lapisan AI adalah **berbasis adapter dan dibangun untuk berkembang**. Setiap provider adalah `IAiProvider` yang dipilih oleh
`AiProviderKind`; seam yang menghadap fitur (`IAiClient`/`AiFeatureService`) tidak pernah berubah. Menambahkan runtime model bawaan baru kemudian (model ONNX lainnya, engine dalam proses yang berbeda, GGUF/llama.cpp
dalam proc, dll) adalah perubahan terlokalisasi: tambahkan `AiProviderKind`, implementasikan satu adapter `IAiProvider`, daftarkan, dan (opsional) sambungkan seeding default + opsi dialog — tanpa perubahan fitur, endpoint, atau alat MCP. Provider ONNX bawaan adalah implementasi referensi dari pola ini.

## Kemampuan

- **Build cBot** — prompt bahasa Inggris biasa → runnable cBot melalui **generate → build → AI-fix** self-repair loop (`build-strategy`), di `/ai/build`. **Kode sumber yang dihasilkan ditampilkan** ketika build selesai (dengan tombol copy), bersama log build — saat sukses *dan* saat gagal — jadi Anda selalu melihat apa yang ditulis AI, bukan hanya error.
- **Tugas AI latar belakang** — mulai pekerjaan AI yang berjalan lama (misalnya membangun cBot) dengan model pilihan Anda, kemudian tinggalkan halaman dan kembali ke hasilnya. Pilih beberapa model untuk dibandingkan — masing-masing berjalan sebagai tugasnya sendiri (`/ai/tasks`). Pekerja host web mengklaim tugas pada lease penyembuhan diri (didapatkan kembali jika node mati) dan streaming progress ke log aktivitas per tugas.
- **Telusuri & pilih model, per fitur** — telusuri model yang diiklankan endpoint provider (`GET /v1/models` di LM Studio / Ollama / vLLM / llama.cpp, atau katalog bawaan) bukan tangan-ketik id, dan **ikat setiap fitur AI ke model yang berbeda** jadi beberapa model melayani fitur berbeda sekaligus (fitur tidak terikat kembali ke provider aktif scope).
- **Optimasi parameter** — loop tertutup: AI mengusulkan set param, masing-masing persisted + backtested di seluruh node (`optimize-run` / `optimize-params`).
- **Agen portfolio otonom** — proposal berbasis mandat dengan jurnal keputusan lengkap (`AgentMandate` → `AgentProposal`).
- **Penjaga risiko yang bertindak** — `AiRiskGuard` layanan latar belakang menilai bot yang berjalan, dapat **auto-stop** pada risiko kritis (opt-in).
- **Penjaga eksposur prop-firm** — batas drawdown/eksposur dengan auto-flatten.
- **Peringatan pasar** — mesin `AlertRule` dengan sentimen AI (grounded pencarian web di mana provider mendukungnya).
- **Analisis** — tinjauan cBot, analisis backtest, post-mortems, sentimen pasar, desain visi grafik, kurasi marketplace.

## Permukaan

- Endpoint web di bawah `/api/ai/*` (build-strategy, generate-project, review, analyze-backtest, optimize-params, optimize-run, post-mortem, sentiment, vision, curate, …), ditambah **tugas latar belakang** (`/api/ai/tasks` create/list/detail/cancel/delete), **penemuan model** (`/api/ai/models/probe`, `/api/ai/usable-models`) dan **pengikatan per fitur** (`/api/ai/feature-bindings`, `/api/ai/my-feature-bindings`).
- Alat MCP (`AiTools`) untuk klien AI — lihat [mcp.md](mcp.md). Pemilihan provider transparan untuk klien MCP.
- Grup nav **AI** — satu halaman Blazor **per fitur**: Build cBot (`/ai/build`), Review (`/ai/review`), Debate (`/ai/debate`), Market Sentiment (`/ai/sentiment`), Exposure Check (`/ai/exposure`), Portfolio Digest (`/ai/digest`), Tune Advisor (`/ai/tune`), Optimize (`/ai/optimize`), **AI Tasks** (`/ai/tasks`), ditambah Portfolio Agent, Alerts, MCP Keys. Halaman berbagi `AiFeaturePageBase` + `AiOutputPanel`; masing-masing menampilkan `AiFeatureNotice` ketika tidak ada provider yang dikonfigurasi.
- **Settings → AI** (`/settings/ai`, hanya pemilik) — daftar provider dengan dialog **Add / edit provider** (jenis, base URL dengan petunjuk per jenis incl. preset localhost Ollama/LM Studio, model, kunci opsional, toggle kemampuan, "set active") dan tombol **Test connection**.

## Konfigurasi

`App:Ai` mendukung baik kunci tunggal legacy dan seeding multi-provider:

- Legacy: `ApiKey`, `Model` (default `claude-opus-4-8`), `BaseUrl`, `MaxTokens` — masih dihormati sebagai
  default Anthropic provider.
- Multi-provider: `ActiveProvider` (jenis) dan `Providers[]` (`{ Kind, BaseUrl, Model, ApiKey?,
  MaxTokens?, Capabilities? }`) — diimpor ke penyimpanan pada startup jika tidak ada kredensial yang ada, jadi tim ops dapat mengirim deployment yang dikonfigurasi (incl. local-LLM) murni melalui appsettings/env.

`RiskGuardEnabled`, `RiskGuardAutoStop`, `RiskGuardInterval` tidak berubah. Untuk tes/dev, kunci config
tinggal di [file kredensial dev terpadu](../testing/dev-credentials.md) di bawah `Ai`.

## Keandalan

Provider diperlakukan sebagai tidak andal — apa pun yang dilakukannya tidak dapat membawa aplikasi. Ini berlaku identik
untuk endpoint cloud dan lokal (Ollama mati mencoba ulang kemudian merosot persis seperti Anthropic yang throttled):

- **Degradasi anggun.** Setiap mode kegagalan (tidak ada provider, HTTP 4xx/5xx/429, timeout, body malformed,
  konten kosong, kemampuan tidak didukung) mengembalikan `AiResult.Fail(reason)` typed — klien tidak pernah
  melempar ke halaman, alat MCP, atau layanan hosted.
- **Pipa resiliensi.** `AddAiHttpClient` memberikan `HttpClient` AI bersama satu percobaan terbatas pada
  transient 5xx / kegagalan jaringan (exponential backoff + jitter) ditambah generous per-attempt dan total
  timeouts (`AiHttp`), digunakan kembali oleh setiap adapter.

## Pengujian dengan fake lokal LLM

Lapisan AI terbukti end-to-end **tanpa ketergantungan eksternal apa pun** oleh `FakeLocalLlmServer` — sebuah tiny
endpoint **compatible OpenAI** dalam proses mengembalikan balasan canned deterministic, wire-identical ke
Ollama/LM Studio/vLLM. Ini mendukung:

- **Unit** — per-adapter request-translation + response-parse tests, routing/capability degradation.
- **Integration** — adapter compatible OpenAI end-to-end, teori resiliensi yang diparametrikan di seluruh
  setiap adapter, dan **alat MCP AI**.
- **E2E** — `AiLocalFixture` boot aplikasi yang ditunjuk ke server fake (atau provider **real** ketika
  developer mengatur `AI_E2E_BASEURL` (+ opsional `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`) —
  real creds menang) dan mendorong setiap fitur AI melalui UI nyata. Menambahkan atau mengubah fitur AI apa pun
  **memerlukan** tes E2E melalui fixture ini (lihat mandat tes repo). Jalur opt-in
  (`AI_LOCAL_LLM=1`) menjalankan satu penyelesaian nyata melalui **Ollama** Testcontainer.

## AI lokal bawaan — zero-setup secara default

LLM lokal ONNX bawaan bekerja di luar kotak: ketika direktorinya tidak ada dan
`App:Ai:BuiltIn:AutoDownload` adalah `true` (default), aplikasi mengunduh model sekali di
background dari `App:Ai:BuiltIn:DownloadBaseUrl`. Saat download berjalan, panggilan AI (dan **Test
connection** di Settings → AI) mengembalikan pesan jelas "model sedang diunduh (setup pertama kali)" daripada
hard failure. Deployment air-gapped/metered setel `AutoDownload=false` dan
pre-provision direktori model (`App:Ai:BuiltIn:ModelPath`). Gating white-label
`App:Branding:AllowBuiltInAi` masih berlaku.

Download juga **pre-warmed pada startup** ketika model built-in adalah provider aktif, sehingga siap sebelum klik AI pertama daripada gagal dengan "downloading…". **Settings → AI** menampilkan live install state pada kartu built-in provider — *Model ready* / *Downloading model…* / *Model not installed* / *Download failed* — dengan tombol **Download model** (atau **Retry download**) yang memicu one-time background fetch on demand (`GET /api/ai/built-in/status`, `POST /api/ai/built-in/install`). Mengaktifkan built-in provider dari Settings kembali menggunakan baris yang sudah di-seed daripada menambah duplicate, sehingga tidak pernah conflict pada constraint single-active-provider.
