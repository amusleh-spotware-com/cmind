# Kekuatan mata wang AI makro & outlook forward

cMind ships enjin kekuatan mata wang makro **AI-assisted, matematik-deterministik**. Ia memeringkat
semesta mata wang yang boleh dikonfigurasi — 8 utama plus mata wang emerging-market dan eksotik — oleh
**kekuatan asas semasa**, dan memprojeksikan **outlook directional forward** untuk setiap pasangan dalam ufuk pilihan (1M / 3M / 6M / 12M). Setiap pangkat, setiap pincang pasangan dan setiap nombor dikira oleh matematik deterministik murni dalam domain teras; LLM hanya *mengumpul* input pandangan-masa-depan yang data
tidak boleh terbitkan dan *menerangkan* keputusan dalam bahasa Inggeris biasa. nó tidak pernah mereka cipta pangkat, arah atau nombor.

> **Had jujur.** Asas meramalkan nilai jangka sederhana hingga panjang dengan baik dan nilai jangka pendek dengan lemah. Rawat ini sebagai penapis penempatan / confluence, **bukan** isyarat masa pendek. Bacaan berhampiran pelepasan impak tinggi (NFP/CPI/bank pusat) tidak bermakna. Bukan nasihat kewangan.

## Cara ia berfungsi

1. **Asas semasa daripada Kalendar Ekonomi, bukan LLM.** Nombor keras — kadar dasar, CPI vs sasaran, GDP, guna tenaga, imbangan perdagangan — dan **skor z keputusan** mereka bersumber **titik-dalam-masa** dari modul [kalendar ekonomi](./economic-calendar.md) (FRED/BLS/BEA/ECB dan jadual bank pusat). Gambaran sejarah tidak pernah bocor lihat-masa-depan.
2. **LLM mengumpul hanya apa yang kalendar tidak boleh terbitkan** — setiap mata wang: trajektori **forward** (laluan kadar dasar dalam bp, arah aliran inflation-vs-sasaran, momentum pertumbuhan) dan outlook **geopolitik** (risiko-on/off, tarif, fiskal/utang, pilihan raya), tambah sebarang angka EM/eksotik semasa yang kalendar tidak ada. JSON ketat, pengesahan arided, carian web aktif.
3. **Domain mengira pangkatan dan matriks forward secara deterministik.** Setiap pemacu discorkan sebagai **skor-z dalam-tier** (jadi inflasi 50% eksotik tidak pernah memesongkan utama), winsorized,
   hasil berbobot dijumlahkan ke komposit, dan disenaraikan terkuat→terlemah dengan pemutus pertindihan ISO yang stabil. Lapisan forward membawa setiap komposit sepanjang trajektorinya —
   `projected = current + horizonScale · Σ trajectoryDriver·weight` — dan memetakan pembezaan projeksi setiap pasangan kepada **pincang directional** (▲ appreciate / ▬ neutral / ▼ depreciate) dengan keyakinan.
4. **LLM menerangkan** pangkatan dan panggilan pasangan teratas dalam bahasa biasa.

## Pemacu

| Pemacu | Kesan pada kekuatan | Nota |
|---|---|---|
| Kadar dasar & trajektori | Lebih tinggi / hawkish ⇒ lebih kuat | Pemberat tertinggi; perbezaan bank pusat memacu jurang terbesar. |
| Inflasi (CPI vs sasaran) | Di atas sasaran ⇒ lebih lemah | Discored secara songsang (seretan beli kuasa). |
| Pertumbuhan GDP | Pertumbuhan relatif lebih tinggi ⇒ lebih kuat | Pembezaan vs panel. |
| Guna tenaga | Tenaga kerja lebih kukuh ⇒ lebih kuat | Memberi makan laluan dasar. |
| Imbangan perdagangan / akaun semasa | Lebihan ⇒ lebih kuat | Permintaan struktur. |
| Stesen dasar | Hawkish ⇒ lebih kuat | Pemacu utama jangka panjang. |
| Momentum keputusan | Beat terkini ⇒ lebih kuat | Daripada skor z keputusan kalendar. |
| Geopolitik / risiko | Risk-off ⇒ selamat haven (USD/JPY/CHF) lebih kuat | Delta risiko forward terikat. |
| Hasil sebenar / bawa *(EM/eksotik)* | Kadar sebenar positif ⇒ lebih kuat | Pemacu dominan EM dalam rejim tenang. |
| Ketahanan luaran *(EM/eksotik)* | Defisit / rizab rendah / USD hutang ⇒ lebih lemah | TekananDepresiasi struktur. |
| Syarat perdagangan *(pengeksport komditi)* | Harga eksport naik ⇒ lebih kuat | BRL, ZAR, CLP, NOK, AUD, CAD. |
| Risiko politik / institusi *(EM/eksotik)* | Ketidakstabilan ⇒ lebih lemah | Jalur mati lebih lebar, keyakinan terhad. |

## Semesta berTier (utama + EM + eksotik)

Semesta **boleh dikonfigurasi oleh penempatan** (`App:CurrencyStrength:Universe`) — menambah mata wang ialah konfigurasi, bukan kod. Setiap mata wang membawa **tier** (`Major` / `EmergingMarket` / `Exotic`) yang menala pemberat, lebar jalur mati dan had keyakinan:

- **Utama** — USD, EUR, GBP, JPY, AUD, NZD, CAD, CHF (dipimpin paras kadar).
- **Emerging markets** — CNH, INR, BRL, MXN, ZAR, KRW, SGD, PLN (+ Scandi NOK/SEK); bawa + risiko +
  ketidakpekaan luaran berganda naik, keyakinan sederhana.
- **Eksotik** — TRY, HUF, CZK, tambah USD-pegged HKD/SAR; keyakinan rendah, jalur mati lebih lebar, keyakinan terhad. **Mata wang dipegg/diurus dengan berat** (HKD, SAR, CNH) dilabel, trajektorinya diturunkan wajarnya, dan outlook pasangan nó diremap kepada `Neutral` jadi peg tidak pernah dibaca sebagai isyarat free-floating.

Kerana statistik EM/eksotik rasmi lebih frekuensi rendah, disemak dan kadangkala legap, angka yang dikumpul AI membawa **keyakinan setiap-tier** yang ditunjukkan sebagai lencana kebolehpercayaan.

## Kemerosotan graceful

| Kalendar | AI | Keputusan |
|---|---|---|
| ✅ | ✅ | Pangkat penuh + projeksi forward + naratif (`CalendarAndAi`). |
| ✅ | ❌ | Pangkatan semasa sahaja, tiada projeksi forward (`CalendarOnly`). |
| ❌ | ✅ | Angka semasa + forward yang dikumpul AI, keyakinan lebih rendah (`AiOnly`). |
| ❌ | ❌ | Tiada gambar — widget menyembunyikan dan halaman menunjukkan keadaan kosong. |

Apl berjalan tidak berubah sama ada. AI gerbang pada kunci AI; kaki kalendar menghormati gerbang white-label + togolan masa jalannya sendiri.

## Menggunakannya

- **Dayakan AI** (Tetapan → AI) dan **hidupkan widget** dari dialog **Suaikan** papan pemuka anda sendiri
  ("Kekuatan mata wang" — pilihan masuk, disembunyikan secara lalai). Widget menunjukkan mata wang kuat/lemah teratas dan panggilan pasangan 3M teratas; nó dipaut ke halaman penuh.
- **Halaman penuh** — `/ai/currency-strength`: pilih UFUK (1M/3M/6M/12M), penapis tier (Semua/Utama/EM/Eksotik), pangkatan semasa, ramalan forward, matriks outlook pasangan (pincang +
  keyakinan, dilabel peg/keyakinan rendah), dan naratif AI. Tekan **Segar sekarang** (pemilik) untuk
  menjana semula. Pekerja latar belakang (`App:CurrencyStrength:RefreshEnabled`, **lalai `true`**) menyegarkan pada
  jadual supaya halaman di-populate di luar kotak; penempatan atau pemilik nó matikan nó (atau lumpuhkan
  ciri AI / kalendar ekonomi, yang dihormati oleh penyegaran dengan mundur ke tiada gambar).

## Aksess secara程序化

Satu model baca kongsi (`ICurrencyStrengthQuery`) boleh dicapai tiga cara:

- **AI dalam apl** — disuntik secara langsung (dalam-proses) ke dalam ciri AI.
- **MCP** — alat `currency_strength` (param `horizon`, `tier`) untuk klien/ejen AI.
- **cBot REST** — `GET /api/market/v1/currency-strength/{latest,history,pair/{base}/{quote}}`, dijamin
  oleh mesin JWT `CalendarJwt` yang sama seperti [API cBot kalendar](./calendar-cbot-api.md) dengan tambahan
  skop **`market:read`**. cBot mendaftarkan klien API dengan `market:read`, menukar id + rahsia nó untuk JWT singkat pada `POST /api/calendar/v1/token`, dan memanggil titik akhir dengan
  `Bearer` token. Tiada skema JWT kedua, tiada rahsia kedua — token yang bocor ialah baca sahaja, skop pasar,
  singkat hayat dan boleh batal. Tiada第二种 JWTscheme, tiada第二种 rahsia — token yang bocor ialah baca sahaja, skop pasar,
  singkat hayat dan boleh batal.

Lihat [API cBot kalendar](./calendar-cbot-api.md) untuk aliran token dan contoh salin-lekat.
