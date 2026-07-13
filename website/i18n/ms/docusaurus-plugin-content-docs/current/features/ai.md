---
description: "AI cMind bersifat agnostik pembekal — Anthropic, OpenAI, Azure OpenAI, Google Gemini, dan mana-mana titik akhir OpenAI-serasakan termasuk model tempatan (Ollama, LM Studio, vLLM). Pilih pembekal, model, dan titik akhir; setiap ciri AI berfungsi tidak berubah."
---

# Ciri AI

Lapisan AI cMind ialah **agnostik pembekal**. Setiap ciri bercakap dengan satu camar seams provider-neutral
(`IAiClient.CompleteAsync`); klien **routing** menyelesaikan bukti pembekal aktif dan menghantar ke adaptor wayar yang sepadan. Anda memilih pembekal + model + titik akhir (dan, jika pembekal memerlukan nó,
kunci); setiap ciri sedia ada berfungsi tidak berubah dengan gerbang, penyulitan, keteguhan, dan kemerosotan yang sama.

**Bateri incluida:** **LLM tempatan terbina dalam dihantar dengan apl dan dilaktifkan secara lalai**
(Microsoft.ML.OnnxRuntimeGenAI, cth Phi-3-mini) — jadi setiap penempatan mempunyai AI yang berfungsi **tanpa kunci API dan tanpa perkhidmatan luaran**. Penempatan white-label boleh mengalihkannya dan mengekang pembekal yang boleh ditambah oleh pengguna. Melampaui terbina dalam, sambungkan mana-mana pembekal luaran.

Pembekal yang disokong:

- **AI tempatan terbina dalam** (`BuiltInOnnx`) — model GenAI ONNX dalam-proses, tiada kunci, dihantar + lalai aktif.
- **Anthropic** (Claude — Messages API)
- **OpenAI** dan **Azure OpenAI** (Chat Completions)
- **Google Gemini** (`generateContent`)
- **Mana-mana titik akhir OpenAI-serasakan**, termasuk **model tempatan** (Ollama, LM Studio, vLLM,
  `server` llama.cpp, LocalAI) dan awan OpenAI-serasakan (OpenRouter, Groq, Together, Mistral,
  DeepSeek) — kesemuanya melalui satu adaptor OpenAI-serasakan, berbeza hanya dengan URL asas + model + kunci.

Tepat **satu** pembekal aktif pada satu masa. Kredensi disimpan **disulit**
(`AiProviderCredential` agregat + `IAiProviderStore` + `ISecretProtector`, `EncryptionPurposes.AiApiKey`);
titik akhir tempatan memerlukan **tiada kunci**. Dengan **tiada** pembekal aktif, setiap ciri mengembalikan keputusan
dilumpuhkan dan baki apl berjalan tidak berubah (tiada kunci diperlukan untuk bina, uji, atau jalankan platform).

**Keserasian laluan:** `App:Ai:ApiKey` legacy penempatan sedia ada (atau tetapan `ai.api_key` tersulit lama) dihormati secara automatik sebagai pembekal aktif **Anthropic** lalai — tindakan sifar diperlukan.

AI tidak dikonfigur → halaman AI meredupkan tindakan dan menunjukkan sepanduk serta segera gesaan untuk menambah pembekal dalam
**Tetapan → AI** (`AiFeatureNotice`). Status di `GET /api/ai/status` (`{ enabled, kind, model }`);
pembekal diurus (pemilik sahaja) melalui `GET/PUT /api/ai/providers`, `POST /api/ai/providers/{id}/activate`,
`DELETE /api/ai/providers/{id}`, dan `POST /api/ai/providers/{id}/test` conectividad ping.

## Lalai penempatan vs pembekal sendiri pengguna

Kredensi AI mempunyai dua skop:

- **Lalai penempatan (diurus pemilik).** Pemilik mengkonfigur pembekal (atau menghantar satu melalui
  `App:Ai:Providers[]` / `App:Ai:ApiKey` legacy). Ia menjadi **lalai kongsi untuk setiap pengguna** —
  jadi broker atau pembekal hosted boleh membiayai AI untuk semua pengguna mereka dengan **tiada persediaan setiap pengguna dan tanpa had setiap pengguna**. Diurus melalui laluan `/api/ai/providers`所有者 sahaja di atas.
- **Pembekal sendiri pengguna (layanan diri).** Mana-mana pengguna yang daftar masuk boleh menambah pembekal mereka sendiri di bawah
  `GET/PUT /api/ai/my-providers`, `POST /api/ai/my-providers/{id}/activate`,
  `DELETE /api/ai/my-providers/{id}`. Apabila hadir, **pembekal aktif mereka sendiri mengatasi lalai penempatan
  untuk ciri AI mereka sendiri**; mengalihkannya kembali ke lalai.

**Kekeruhan pesanan** (dalam `AiProviderStore`, setiap permintaan pengguna): bukti aktif pengguna sendiri → lalai
penempatan → kunci konfigurasi legacy → none (AI dilumpuhkan). Tepat satu bukti aktif **setiap skop**
(indeks unik separa setiap `OwnerUserId`), dan setiap skop diselesaikan secara bebas, jadi pengguna yang mengaktifkan
kunci mereka sendiri tidak pernah mengganggu lalai kongsi. Konteks latarbelakang/non-Web (tiada pengguna permintaan) sentiasa menyelesaikan lalai penempatan.

## Matriks keupayaan pembekal

Keupayaan lalai setiap pembekal dan boleh ditindih oleh pemilik. Apabila keupayaan dimatikan ciri
**merosot, tidak pernah membaling**: carian web senyap dibuang; visinya mengembalikan kegagalan taip
tidak disokong keupayaan.

| Pembekal | Jenis | URL Asas Lalai | Kunci diperlukan | Carian web | Visi | Nota |
|---|---|---|---|---|---|---|
| AI tempatan terbina dalam | `BuiltInOnnx` | t/a (dalam-proses) | tidak | ✖ | ✖ | model GenAI ONNX dihantar, lalai aktif |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | ya | ✅ | ✅ | Messages API, alat `web_search` |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | ya | pilihan | pilihan | Chat Completions |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | ya | ✅ | ✅ | laluan deployment + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | ya | ✅ | ✅ | `generateContent`, pembingkaian `google_search` |
| Ollama (tempatan) | `OpenAiCompatible` | `http://localhost:11434/v1/` | tidak | ✖ | bergantung model | melalui adaptor OpenAI-serasakan |
| LM Studio (tempatan) | `OpenAiCompatible` | `http://localhost:1234/v1/` | tidak | bergantung model | bergantung model | melalui adaptor OpenAI-serasakan |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | URL yang served | tidak | ✖ | bergantung model | melalui adaptor OpenAI-serasakan |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | URL pembekal | ya | ✖ | bergantung model | melalui adaptor OpenAI-serasakan |

Panduan persediaan setiap pembekal (kunci, URL, ID model, langkah UI): lihat
[Panduan persediaan pembekal AI — katalog](../deployment/ai-providers.md).

## AI tempatan terbina dalam (dihantar, lalai aktif)

cMind menghantar **LLM tempatan sebenar yang berjalan dalam-proses** melalui
[Microsoft.ML.OnnxRuntimeGenAI](https://onnxruntime.ai/docs/genai/) (model instruct padat seperti
Phi-3-mini). Ia memerlukan **tiada kunci API dan tiada perkhidmatan luaran**, dan pada permulaan pertama — apabila tiada pembekal dikonfigur dan gerbang white-label membenarkannya — nó **dibibit dan diaktifkan secara automatik**, jadi setiap penempatan mempunyai AI yang berfungsi di luar kotak.

- Direktori model (`genai_config.json` + tokenizer + pemberat) dikonfigur oleh
  `App:Ai:BuiltIn:ModelPath` (lalai `models/onnx`, relatif kepada direktori asas apl). Apabila fail model
  tidak hadir pembekal **merosot kepada kegagalan taip dengan hint pemasangan** — nó tidak pernah membaling,
  dan baki apl tidak terjejas.
- Ia menggerakkan setiap ciri AI teks. Menjadi model padat, nó teks sahaja (tiada carian web pelayan atau
  visi) dan penjanaan disiri (satu contoh model, digunakan semula selepas muat malas).
- Perolehi/bundle model: lihat [Pembekal AI → terbina dalam](../deployment/ai-providers.md#built-in-local-ai-onnx-shipped).

## Kawalan white-label

Penempatan white-label mengekang AI melalui `App:Branding` (dikuatkuasakan pelayan-side pada setiap kemas kini pembekal):

- `AllowBuiltInAi` (lalai `true`) — tetapkan `false` untuk **mengalih model terbina dalam** seluruhnya.
- `AllowLocalProviders` (lalai `true`) — tetapkan `false` untuk haram titik akhir tempatan/di-host sendiri (loopback /
  OpenAI-serasakan peribadi, cth Ollama/LM Studio/vLLM).
- `AllowedAiProviderKinds` (lalai kosong = semua) — senaraikan hanya jenis yang diluluskan penempatan (cth.
  `["Anthropic","OpenAiCompatible"]`) untuk mengunci pembekal yang boleh ditambah pengguna.

## Memperluaskan: model terbina dalam masa depan

Lapisan AI **berasaskan adaptor dan dibina untuk berkembang**. Setiap pembekal ialah `IAiProvider` yang dipilih oleh
`AiProviderKind`; camar ciri (`IAiClient`/`AiFeatureService`) tidak pernah berubah. Menambah masa jalan model terbina dalam baharu kemudian (model ONNX lain, enjin dalam-proses berbeza, GGUF/llama.cpp
in-proc, dll.) ialah perubahan setempat: tambah `AiProviderKind`, pelaksanaan satu adaptor `IAiProvider`,
daftarkannya, dan (pilihan) sambungkan pembibitan lalai + pilihan dialog — tiada perubahan ciri, titik akhir, atau alat MCP.
Pembekal ONNX terbina dalam ialah pelaksanaan rujukan corak ini.

## Keupayaan

- **Bina cBot** — prompt bahasa Inggeris biasa → cBot yang boleh lari melalui **jana → bina → pembaikan AI** gelung pembaikan diri (`build-strategy`), di `/ai/build`.
- **Pengoptimuman parameter** — gelung tertutup: AI cadangkan set parameter, setiap dikekalkan + di-backtest merentasi nod (`optimize-run` / `optimize-params`).
- **Ejen portfolio autonomic** — cadangan bermotivasi mandat dengan jurnal keputusan penuh (`AgentMandate` → `AgentProposal`).
- **Penjaga risiko bertindak** — perkhidmatan latar `AiRiskGuard` menilai bot yang berjalan, boleh **auto-berhenti** pada risiko kritikal (pilihan masuk).
- **Penjaga pendedahan prop-firm** — had undur/pendedahan dengan ratakan automatik.
- **Makluman pasaran** — enjin `AlertRule` dengan sentimen AI (pembingkaian carian web di mana pembekal menyokongnya).
- **Analisis** — ulasan cBot, analesis backtest, post-mortem, sentimen pasaran, reka bentuk carta-penglihatan, kurasi marketplace.

## Permukaan

- Titik akhir Web di bawah `/api/ai/*` (build-strategy, generate-project, review, analyze-backtest, optimize-params, optimize-run, post-mortem, sentiment, vision, curate, …).
- Alat MCP (`AiTools`) untuk klien AI — lihat [mcp.md](mcp.md). Pemilihan pembekal telus kepada klien MCP.
- Kumpulan nav **AI** — satu halaman Blazor **setiap ciri**: Bina cBot (`/ai/build`), Semak (`/ai/review`), Perdebatan (`/ai/debate`), Sentimen Pasaran (`/ai/sentiment`), Semakan Pendedahan (`/ai/exposure`), Ringkasan Portfolio (`/ai/digest`), Penasihat Tune (`/ai/tune`), Optimimum (`/ai/optimize`), tambah Ejen Portfolio, Makluman, Kunci MCP. Halaman kongsi `AiFeaturePageBase` + `AiOutputPanel`; setiap menunjukkan `AiFeatureNotice` apabila tiada pembekal dikonfigur.
- **Tetapan → AI** (`/settings/ai`, pemilik sahaja) — senarai pembekal dengan **dialog tambah/edit pembekal** (jenis, URL asas dengan hint setiap jenis termasuk preset localhost Ollama/LM Studio, model, kunci pilihan, togolan keupayaan, "tetap aktif") dan butang **Uji sambungan**.

## Konfigurasi

`App:Ai` menyokong kedua-dua kunci tunggal legacy dan pembibitan pelbagai pembekal:

- Legacy: `ApiKey`, `Model` (lalai `claude-opus-4-8`), `BaseUrl`, `MaxTokens` — masih dihormati sebagai
  pembekal Anthropic lalai.
- Pelbagai pembekal: `ActiveProvider` (jenis) dan `Providers[]` (`{ Kind, BaseUrl, Model, ApiKey?,
  MaxTokens?, Capabilities? }`) — diimport ke kedai pada permulaan jika tiada kredensi wujud lagi, jadi
  Pasukan ops boleh menghantar penempatan terkonfigur (termasuk LLM tempatan) sepenuhnya melalui appsettings/env.

`RiskGuardEnabled`, `RiskGuardAutoStop`, `RiskGuardInterval` tidak berubah. Untuk ujian/dev, kunci konfigurasi
tinggal dalam [fail kredensi dev](../testing/dev-credentials.md) di bawah `Ai`.

## Kebolehpercayaan

Pembekal dirawat sebagai tidak dipercayai — tidak ada yang nó lakukan boleh merosakkan apl. Ini pegang secara sama
untuk titik akhir awan dan tempatan (Ollama mati mencuba semula kemudian merosot tepat seperti Anthropic yang dikecilkan):

- **Kemerosotan graceful.** Setiap mod kegagalan (tiada pembekal, HTTP 4xx/5xx/429, masa tamat, badan malformed,
  kandungan kosong, keupayaan tidak disokong) mengembalikan `AiResult.Fail(sebab)` taip — klien tidak pernah membaling ke halaman, alat MCP, atau perkhidmatan dihos.
- **Salur keteguhan.** `AddAiHttpClient` memberikan satu `HttpClient` AI kongsi percubaan semula terikat pada
  kegagalan 5xx / rangkaian transient (exponential backoff + jitter) tambah masa tamat yang luas setiap percubaan dan
  jumlah (`AiHttp`), digunakan semula oleh setiap adaptor.

## Menguji dengan LLM tempatan palsu

Lapisan AI dibuktikan end-to-end **tanpa sebarang kebergantungan luaran** oleh `FakeLocalLlmServer` — titik akhir
**OpenAI-serasakan** dalam-proses kecil yang mengembalikan jawapan tiruan deterministik, wayar-separa dengan
Ollama/LM Studio/vLLM. nó menyokong:

- **Unit** — ujian terjemahan-permintaan setiap adaptor + penghuraian-respons, penghalaan/penurunan keupayaan.
- **Integrasi** — adaptor OpenAI-serasakan end-to-end, teori keteguhan berparameter merentasi
  setiap adaptor, dan **alat AI MCP**.
- **E2E** — `AiLocalFixture` boot apl menunjuk ke pelayan palsu (atau pembekal **sebenar** apabila
  pembangun menetapkan `AI_E2E_BASEURL` (+ pilihan `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`) —
  kredensi sebenar menang) dan menggerakkan setiap ciri AI melalui UI sebenar. Menambah atau menukar mana-mana ciri AI
  **memerlukan** ujian E2E melalui fixture ini (lihat mandatori ujian repo). Lorong pilihan masuk
  (`AI_LOCAL_LLM=1`) menjalankan satu penyiapan sebenar melalui **Ollama** Testcontainer.

## AI tempatan terbina dalam — sifar-penyediaan secara lalai

LLM tempatan ONNX terbina dalam berfungsi di luar kotak: apabila direktori model nó tidak hadir dan
`App:Ai:BuiltIn:AutoDownload` ialah `true` (lalai), apl memuat turun model sekali di
latar belakang dari `App:Ai:BuiltIn:DownloadBaseUrl`. Apabila muat turun berjalan, panggilan AI (dan **Uji
sambungan** dalam Tetapan → AI) mengembalikan mesej jelas "model sedang dimuat turun (persediaan kali pertama)" berbanding kegagalan keras. Penempatan air-gapped/metered tetapkan `AutoDownload=false` dan pra-bekalkan direktori model (`App:Ai:BuiltIn:ModelPath`). Gerbang white-label
`App:Branding:AllowBuiltInAi` masih dipakai.
