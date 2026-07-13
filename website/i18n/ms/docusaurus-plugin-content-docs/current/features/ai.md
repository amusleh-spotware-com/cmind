---
description: "AI cMind adalah agnostik penyedia — Anthropic, OpenAI, Azure OpenAI, Google Gemini, dan sebarang titik akhir serasi OpenAI termasuk model lokal (Ollama, LM Studio, vLLM). Pilih penyedia, model, titik akhir; setiap ciri AI berfungsi tanpa perubahan."
---

# Ciri-ciri AI

Lapisan AI cMind adalah **agnostik penyedia**. Setiap ciri bercakap dengan jahitan neutral penyedia tunggal (`IAiClient.CompleteAsync`); **pelanggan penghalaan** menyelesaikan bukti kelayakan penyedia aktif dan menghantarkan ke adaptor wayar yang sepadan. Anda memilih penyedia + model + titik akhir (dan, jika penyedia memerlukannya, kunci); setiap ciri sedia ada berfungsi tanpa perubahan dengan gating, enkripsi, ketahanan, dan degradasi yang sama.

**Bateri termasuk:** **LLM lokal terbina bersama apl dan didayakan secara lalai** (Microsoft.ML.OnnxRuntimeGenAI, contohnya Phi-3-mini) — jadi setiap penempatan mempunyai AI yang berfungsi **tanpa kunci API dan tanpa perkhidmatan luaran**. Penempatan berkaitan putih boleh membuangnya dan menyekat penyedia mana yang pengguna boleh tambah. Selepas terbina, sambungkan mana-mana penyedia luaran.

Penyedia yang disokong:

- **AI lokal terbina-dalam** (`BuiltInOnnx`) — model ONNX GenAI dalam proses, tiada kunci, dihantar + lalai aktif.
- **Anthropic** (Claude — API Mesej)
- **OpenAI** dan **Azure OpenAI** (Penyiapan Sembang)
- **Google Gemini** (`generateContent`)
- **Sebarang titik akhir serasi OpenAI**, termasuk **model lokal** (Ollama, LM Studio, vLLM, llama.cpp `server`, LocalAI) dan awan serasi OpenAI (OpenRouter, Groq, Together, Mistral, DeepSeek) — semua melalui satu adaptor serasi OpenAI, berbeza hanya mengikut URL asas + model + kunci.

Tepat **satu** penyedia aktif pada satu masa. Bukti kelayakan disimpan **dienkripsi** (`AiProviderCredential` mengagregat + `IAiProviderStore` + `ISecretProtector`, `EncryptionPurposes.AiApiKey`); titik akhir lokal memerlukan **tiada kunci**. Dengan **tiada** penyedia aktif, setiap ciri mengembalikan hasil ketidakupayaan dan aplikasi selebihnya berjalan tanpa perubahan (tiada kunci diperlukan untuk membina, menguji, atau menjalankan platform).

**Serasi belakang:** bukti kelayakan penyedia warisan `App:Ai:ApiKey` sesuatu penempatan sedia ada (atau tetapan `ai.api_key` dienkripsi lama) dihormati secara automatik sebagai penyedia **Anthropic** aktif lalai — tindakan sifar diperlukan.

AI tidak dikonfigurasi → halaman AI mengaburi tindakan dan menunjukkan sepanduk tambah satu kali permintaan untuk menambah penyedia dalam **Tetapan → AI** (`AiFeatureNotice`). Status di `GET /api/ai/status` (`{ enabled, kind, model }`); penyedia diurus (milik sahaja) melalui `GET/PUT /api/ai/providers`, `POST /api/ai/providers/{id}/activate`, `DELETE /api/ai/providers/{id}`, dan ping ping sambungan `POST /api/ai/providers/test`.

## Lalai penempatan berbanding penyedia pengguna sendiri

Bukti kelayakan AI mempunyai dua skop:

- **Lalai penempatan (milik-diurus).** Pemilik mengonfigurasi penyedia (atau menghantar satu melalui `App:Ai:Providers[]` / `App:Ai:ApiKey` warisan). Ia menjadi **lalai dikongsi untuk setiap pengguna** — jadi broker atau pembekal pengehosan boleh membiayai AI untuk semua pengguna mereka dengan **tiada persediaan per pengguna dan tiada had per pengguna**. Diurus melalui laluan `/api/ai/providers` hanya milik di atas.
- **Penyedia pengguna sendiri (layanan diri).** Mana-mana pengguna yang masuk mungkin menambah penyedia mereka sendiri di bawah `GET/PUT /api/ai/my-providers`, `POST /api/ai/my-providers/{id}/activate`, `DELETE /api/ai/my-providers/{id}`. Apabila hadir, bukti kelayakan aktif mereka sendiri **mengubah lalai penempatan untuk ciri AI mereka sendiri**; membuangnya kembali ke lalai.

**Perintah resolusi** (dalam `AiProviderStore`, setiap pengguna permintaan): bukti kelayakan aktif pengguna mereka sendiri → lalai penempatan → kunci konfigurasi warisan → tiada (AI hilang). Tepat satu bukti kelayakan adalah aktif **setiap skop** (indeks unik separa setiap `OwnerUserId`), dan setiap skop diselesaikan secara bebas, jadi pengguna mengaktifkan kunci mereka sendiri tidak akan mengganggu lalai dikongsi. Latar belakang/konteks bukan-Web (tiada pengguna permintaan) sentiasa menyelesaikan lalai penempatan.

## Matriks keupayaan penyedia

Keupayaan lalai setiap penyedia dan boleh ditulis semula milik. Apabila keupayaan adalah keluaran ciri **merendah, tidak pernah melempar**: carian web didrop senyap; penglihatan mengembalikan kegagalan ketidakupayaan jenis.

| Penyedia | Jenis | URL asas lalai | Kunci diperlukan | Carian web | Penglihatan | Catatan |
|---|---|---|---|---|---|---|
| AI lokal terbina-dalam | `BuiltInOnnx` | tiada (dalam proses) | tidak | ✖ | ✖ | model ONNX GenAI dihantar, lalai aktif |
| Anthropic | `Anthropic` | `https://api.anthropic.com/` | ya | ✅ | ✅ | API Mesej, alat `web_search` |
| OpenAI | `OpenAiCompatible` | `https://api.openai.com/v1/` | ya | opt-in | opt-in | Penyiapan Sembang |
| Azure OpenAI | `AzureOpenAi` | `https://<resource>.openai.azure.com/` | ya | ✅ | ✅ | laluan penempatan + `api-version` |
| Google Gemini | `Gemini` | `https://generativelanguage.googleapis.com/` | ya | ✅ | ✅ | `generateContent`, `google_search` berlandaskan |
| Ollama (lokal) | `OpenAiCompatible` | `http://localhost:11434/v1/` | tidak | ✖ | bergantung model | melalui adaptor serasi OpenAI |
| LM Studio (lokal) | `OpenAiCompatible` | `http://localhost:1234/v1/` | tidak | bergantung model | bergantung model | melalui adaptor serasi OpenAI |
| vLLM / llama.cpp / LocalAI | `OpenAiCompatible` | URL anda disajikan | tidak | ✖ | bergantung model | melalui adaptor serasi OpenAI |
| OpenRouter / Groq / Together / Mistral / DeepSeek | `OpenAiCompatible` | URL penyedia | ya | ✖ | bergantung model | melalui adaptor serasi OpenAI |

Panduan persediaan penuh setiap penyedia (kunci, URL, id model, langkah UI): lihat [Penyedia AI — katalog persediaan](../deployment/ai-providers.md).

## AI lokal terbina-dalam (dihantar, lalai aktif)

cMind menghantar **LLM lokal sebenar yang berjalan dalam proses** melalui [Microsoft.ML.OnnxRuntimeGenAI](https://onnxruntime.ai/docs/genai/) (model arahan padat seperti Phi-3-mini). Ia memerlukan **tiada kunci API dan tiada perkhidmatan luaran**, dan pada permulaan pertama — apabila tiada penyedia dikonfigurasi dan pintu penyekat berkaitan putih membenarkannya — ia adalah **dijenis dan diaktifkan secara automatik**, jadi setiap penempatan mempunyai AI yang berfungsi daripada kotak.

- Direktori model (`genai_config.json` + tokenizer + berat) dikonfigurasi oleh `App:Ai:BuiltIn:ModelPath` (lalai `models/onnx`, relatif kepada direktori asas apl). Apabila fail model tiada penyedia **merendah kepada kegagalan jenis dengan petunjuk pemasangan** — ia tidak pernah melempar, dan apl selebihnya tidak terjejas.
- Ia memberi kuasa kepada setiap ciri AI teks. Menjadi model padat, ia adalah teks sahaja (tiada carian web sebelah pelayan atau penglihatan) dan penjanaan adalah bersiri (satu contoh model, digunakan semula selepas beban malas).
- Perolehan/bundle model: lihat [Penyedia AI → terbina-dalam](../deployment/ai-providers.md#built-in-local-ai-onnx-shipped).

## Kawalan berkaitan putih

Penempatan berkaitan putih menyekat AI melalui `App:Branding` (dilaksanakan sebelah pelayan pada setiap upsert penyedia):

- `AllowBuiltInAi` (lalai `true`) — tetapkan `false` untuk **buang model terbina-dalam** sepenuhnya.
- `AllowLocalProviders` (lalai `true`) — tetapkan `false` untuk melarang titik akhir lokal/enjadi sendiri (loopback / OpenAI serasi peribadi, contohnya Ollama/LM Studio/vLLM).
- `AllowedAiProviderKinds` (lalai kosong = semua) — senaraikan hanya jenis penempatan membenarkan (contohnya `["Anthropic","OpenAiCompatible"]`) untuk mengunci penyedia mana pengguna boleh tambah.

## Memanjangkan: model terbina-dalam masa hadapan

Lapisan AI adalah **berasaskan adaptor dan dibina untuk berkembang**. Setiap penyedia adalah `IAiProvider` dipilih oleh `AiProviderKind`; jahitan menghadap ciri (`IAiClient`/`AiFeatureService`) tidak pernah berubah. Menambah masa hadapan model terbina-dalam baru (model ONNX lain, enjin dalam proses berbeza, GGUF/llama.cpp dalam proses, dsb.) adalah perubahan terlokalisasi: tambah `AiProviderKind`, laksanakan satu adaptor `IAiProvider`, daftarkan ia, dan (pilihan) wayar penjemahan lalai + pilihan dialog — tiada ciri, titik akhir, atau perubahan alat MCP. Penyedia ONNX terbina-dalam adalah pelaksanaan rujukan pola ini.

## Keupayaan

- **Bina cBot** — petunjuk teks biasa → cBot boleh lari melalui gelung **penjanaan → bina → pembaikan diri AI** (`build-strategy`), di `/ai/build`.
- **Pengoptimalan parameter** — gelung tertutup: cadangan AI tetapan param, setiap dijemput + diuji balik merentasi nod (`optimize-run` / `optimize-params`).
- **Ejen portfolio autonomi** — cadangan didorong amanat dengan jurnal keputusan penuh (`AgentMandate` → `AgentProposal`).
- **Penjaga risiko yang bertindak** — perkhidmatan latar belakang `AiRiskGuard` menilai bot yang berjalan, boleh **berhenti auto** pada risiko kritikal (opt-in).
- **Penjaga pendedahan prop-firm** — had penarikan/pendedahan dengan auto-ratakan.
- **Amaran pasaran** — enjin `AlertRule` dengan sentimen AI (berdasarkan carian web di mana penyedia menyokongnya).
- **Analisis** — semakan cBot, analisis ujian belakang, autopsi, sentimen pasaran, reka bentuk penglihatan carta, kurasi pasaran tempat.

## Permukaan

- Titik akhir web di bawah `/api/ai/*` (bina-strategi, janakan-projek, semak, analisis-ujian belakang, optimalkan-param, optimalkan-jalankan, autopsi, sentimen, penglihatan, kurasi, ...).
- Alat MCP (`AiTools`) untuk pelanggan AI — lihat [mcp.md](mcp.md). Pemilihan penyedia adalah telus kepada pelanggan MCP.
- Kumpulan nav **AI** — satu halaman Blazor **setiap ciri**: Bina cBot (`/ai/build`), Semak (`/ai/review`), Debat (`/ai/debate`), Sentimen Pasaran (`/ai/sentiment`), Semakan Pendedahan (`/ai/exposure`), Ringkasan Portfolio (`/ai/digest`), Penasihat Tala (`/ai/tune`), Optimalkan (`/ai/optimize`), tambah Ejen Portfolio, Amaran, Kunci MCP. Halaman berkongsi `AiFeaturePageBase` + `AiOutputPanel`; setiap menunjukkan `AiFeatureNotice` apabila tiada penyedia dikonfigurasi.
- **Tetapan → AI** (`/settings/ai`, milik sahaja) — senarai penyedia dengan **Dialog Penyedia Tambah / sunting** (jenis, URL asas dengan petunjuk setiap jenis termasuk Ollama/LM Studio localhost prautama, model, kunci pilihan, togol keupayaan, "tetapkan aktif") dan butang **Sambungan ujian**.

## Konfigurasi

`App:Ai` menyokong kedua-dua kunci tunggal warisan dan penjemahan multi-penyedia:

- Warisan: `ApiKey`, `Model` (lalai `claude-opus-4-8`), `BaseUrl`, `MaxTokens` — masih dihormati sebagai penyedia Anthropic lalai.
- Multi-penyedia: `ActiveProvider` (jenis) dan `Providers[]` (`{ Kind, BaseUrl, Model, ApiKey?, MaxTokens?, Capabilities? }`) — diimport ke dalam stor pada permulaan jika tiada bukti kelayakan wujud lagi, jadi pasukan ops boleh menghantar penempatan dikonfigurasi (termasuk LLM lokal) semata-mata melalui appsettings/env.

`RiskGuardEnabled`, `RiskGuardAutoStop`, `RiskGuardInterval` tidak berubah. Untuk ujian/dev, kunci konfigurasi hidup dalam fail bukti kelayakan dev bersatu [](../testing/dev-credentials.md) di bawah `Ai`.

## Kebolehpercayaan

Penyedia dianggap sebagai tidak boleh dipercayai — tiada apa yang dilakukannya boleh membawa aplikasi ke bawah. Ini memegang sama untuk titik akhir awan dan lokal (Ollama mati mengulang kemudian merendah tepat seperti Anthropic dihalang):

- **Degradasi bermurah hati.** Setiap mod kegagalan (tiada penyedia, HTTP 4xx/5xx/429, tamat masa, badan cacat, kandungan kosong, keupayaan tidak disokong) mengembalikan jenis `AiResult.Fail(reason)` — pelanggan tidak pernah melempar ke halaman, alat MCP, atau perkhidmatan enjin.
- **Saluran ketahanan.** `AddAiHttpClient` memberikan `HttpClient` AI dikongsi satu percubaan terikat pada transien 5xx / kegagalan rangkaian (undur eksponen + gentar) tambah tamat masa berbaik-baik per percubaan dan jumlah (`AiHttp`), digunakan semula setiap adaptor.

## Pengujian dengan LLM lokal palsu

Lapisan AI dibuktikan hujung ke hujung **tanpa sebarang pergantungan luaran** oleh `FakeLocalLlmServer` — titik akhir kecil dalam proses **serasi OpenAI** mengembalikan balasan kalengan deterministik, wayar-sama dengan Ollama/LM Studio/vLLM. Ia mendukung:

- **Unit** — setiap ujian terjemahan permintaan adaptor + analisis balasan, penghalaan/degradasi keupayaan.
- **Integrasi** — adaptor serasi OpenAI hujung ke hujung, teori ketahanan berparameter merentasi setiap adaptor, dan **alat MCP AI**.
- **E2E** — `AiLocalFixture` memulakan apl menunjuk ke pelayan palsu (atau penyedia **sebenar** apabila pembangun menetapkan `AI_E2E_BASEURL` (+ pilihan `AI_E2E_API_KEY` / `AI_E2E_KIND` / `AI_E2E_MODEL`) — bukti kelayakan sebenar menang) dan mendorong setiap ciri AI melalui UI sebenar. Menambah atau mengubah mana-mana ciri AI **memerlukan** ujian E2E melalui perlengkapan ini (lihat mandat ujian repo). Lajur opt-in (`AI_LOCAL_LLM=1`) menjalankan satu penyiapan sebenar melalui bekas ujian **Ollama**.
