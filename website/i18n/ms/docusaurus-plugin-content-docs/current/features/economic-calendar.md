---
description: "cMind kapal dengan kalendar ekonomi sendiri — jadual pelepasan, pencapaian sebenar, ramalan, pindaan dan model impak berdasarkan data — diperolehi dari autoriti utama (bank pusat dan agensi statistik nasional), dengan ketergantungan sifar…"
---

# Kalendar ekonomi

cMind kapal dengan **kalendar ekonomi sendiri** — jadual pelepasan, pencapaian sebenar, ramalan, pindaan dan
model impak berdasarkan data — diperolehi dari **autoriti utama** (bank pusat dan agensi statistik nasional),
dengan **ketergantungan sifar** pada ForexFactory, FXStreet, Investing.com atau mana-mana aggregator. Ia tepat
mengikut masa, menyimpan ≥10 tahun sejarah, dan disambungkan ke perdagangan, API awam, MCP, cBots, AI,
makluman dan backtest. Ia ialah modul terpisah: ia boleh dilumpuhkan dengan kesan sifar pada teras perdagangan.

> **Status.** P0–P4 telah dilaksanakan dan dihantar. Teras domain, berterusan (skema EF `calendar`, append-only baca/tulis, sumber FRED + BLS + jadual-bank-pusat, pekerja pengambilan dengan penjejakan kesegaran setiap sumber yang gerbang konfigurasi), API REST JWT berversi, UI `/economic-calendar` mengutamakan mudah alih, alat MCP, API JWT cBot, makluman peristiwa berimpak tinggi, jeda pemadaman berita salin-dagangan, hamparan peristiwa backtest, strim SSE, webhook bertanda HMAC, dan `CmindCalendarClient` yang ditaip semuanya dilaksanakan dan diuji integrasi. P5 tambahan (analitik kejutan, eksport iCal/CSV, carian kata kunci, konsensus pluggable) adalah item yang tinggal — lihat fasa pengeluaran di bawah.

## Apa yang membezakannya

Kekeruhan berulang terhadap kalendar terkemuka menjadi kekangan reka bentuk kami:

- **Tiada perubahan rating impak senyap.** Rating impak kami **deterministik, berjujukan dan
  boleh审计**. Setiap perubahan ialah pindaan berrekod dengan cap masa — bukan tulis semula senyap. Pengguna
  boleh melihat dengan tepat *mengapa* sesuatu peristiwa adalah Tinggi.
- **Satu sauh UTC per peristiwa.** Setiap peristiwa diikat pada satu saat UTC dari jadual rasmi
  sumber utama; zon waktu sumber sendiri disimpan, dan pembuatan setiap pengguna menggunakan zon waktu IANA
  jelas dengan DST dikendalikan oleh pangkalan data zon — bukan suis manual ±1j.
- **Rantai pindaan penuh, di mana-mana.** Nilai asal dan setiap pindaan ialah kelas pertama, didedahkan
  sama rata melalui API, MCP dan permukaan cBot.
- **≥10 tahun sejarah, tiada dinding.** Julat pelayaran tanpa had; tiada topeng 60 hari, tiada gerbang pendaftaran.
- **Tepat-masa oleh konstruksi.** Setiap fakta membawa `KnownAt` (bila *kami* mengetahuinya) dan
  `EffectiveAt` (saat peristiwa). "Seperti kalendar pada masa T" ialah pertanyaan kelas pertama, jadi
  dasar berita yang di-backtest berkelakuan tepat seperti langsung — tiada lihat-depan dari menggunakan nilai
  yang dipinda dalam sejarah.

## Model impak

Skor impak ialah fungsi murni, deterministik dalam `[0, 100]`, dikumpulkan kepada Rendah / Sederhana / Tinggi /
Kritikal. Inputnya hanya data yang diketahui pada masa penskoran (tiada kebocoran masa depan):

- **Prior siri** — pemberat dasar per kelas indikator (keputusan kadar mengatasi CPI, yang
  mengatasi tinjauan minor).
- **Jejak turun naiktorealisasi** — pulangan mutlak median simbol primer yang terjejas dalam
  tingkap selepas pelepasan *lalu* siri ini: "pelepasan ini secara sejarah menggerakkan harga sebanyak ini."
- **Kepekaan kejutan** — betapa kuatnya kejutan mutlak (z-score) secara historis
  berkorelasi dengan pergerakan selepas pelepasan.

Skor mencampurkan ini dengan pemberat tetap dan mencap `ImpactModelVersion`. Pengiraan semula ialah
operasi jelas, logged yang menghasilkan **pindaan baharu** — bukan mutasi — jadi skor selalu
 boleh dihasilkan semula dari inputnya.

## Pemetaan Negara → mata wang → simbol

kerekatan integrasi algo yang paling sering disebut sekali diselesaikan, sebagai fungsi murni: negara memetakan
kepada mata wangnya (setiap ahli kawasan euro berkembang ke EUR), dan mata wang memetakan kepada simbol watchlist
yang mengutipnya pada mana-mana kaki. Jadi **EURUSD terjejas oleh peristiwa EU dan US kedua-duanya**;
XAUUSD didedahkan USD; US500 memetakan kepada USD. Ini memacu penapis berita, resolusi simbol terjejas
dan matematik blackout.

## Dasar tetingkap berita

`NewsWindowRule` ialah `{ minImpact, beforeMinutes, afterMinutes, currencies?, series? }`. Satu
pelaksanaan murni, dikongsi menjawab "adakah saat T dalam blackout untuk simbol S?" — digunakan oleh
penapis berita cBot, jeda salin-perdagangan dan pengawal risiko AI, jadi nó tidak boleh berselisih.
Pada ketidakpastian jawapan blackout lalai kepada nilai konservatif yang dikonfigurasi (gagalditutup secara lalai)
jadi jurang data tidak pernah secara senyap menghidupkan perdagangan melalui pelepasan berimpak tinggi.

## Tepat-masa & pindaan

Pencapaian sebenar, ramalan dan skor impak **append-only**. Setiap peristiwa memiliki rantai pesanan
pindaan, monotonic dalam `KnownAt`:

- `Scheduled` — peristiwa pertama dijadualkan (impak prior, tiada pencapaian sebenar).
- `Released` — pencapaian sebenar pertama tiba.
- `Revised` — nilai pindaan tiba.
- `Rescheduled` — sumber mengalihkan saat pelepasan (boleh diaudit, boleh dimaklumkan).
- `Rescored` — skor impak dikira semula di bawah versi model baharu.

Menanya `sejak` saat lalu mengembalikan tepat pindaan yang diketahui那时候 — jaminan yang menghapuskan
lihat-depan dalam dasar berita yang di-backtest.

## Ramalan / konsensus

Median tinjauan ekonomi bukan **tidak** diterbitkan secara bebas oleh sumber utama — ia ialah nilai tambah
proprietari aggregator, dan kami tidak memfabricate nó. Skema peristiwa membawa `Forecast` nullable;
penggunaan boleh wayar suapan konsensus berlesen melalui port pilihan `IForecastProvider`
(bawa kunci sendiri, lalai off). Nilai sebelumnya dan pindaan selalu datang dari sumber rasmi.

## Sumber data

Dua lapisan terpisah, semua primer — bukan aggregator:

- **Jadual / masa:** kalendar pelepasan FRED; agensi statistik nasional (BLS, BEA, Census,
  Eurostat, ONS, Destatis, INSEE, e-Stat, ABS, StatCan); kalendar mesyuarat bank pusat (Fed, ECB,
  BoE, BoJ, RBA, BoC, SNB, RBNZ).
- **Nilai sebenar:** FRED (dengan tarikh vintage untuk pindaan dan tepat-masa), tambah BLS, BEA, Census,
  ECB SDW, Eurostat dan API OECD SDMX.

Sumber mati degradasi liputan untuk **sumber itu sahaja**; kalendar terus menghidangkan semua yang lain
dan permukaan jurang sebagai metrik kesegaran.

## Had kadar & pelan sandaran

Pembekal luaran menerbitkan had kadar (FRED membenarkan ~120 permintaan/minit). Kalendar dibina supaya nó
**tidak pernah mencetuskan had pembekal**, dan supaya dilepaskan atau dipotong tidak pernah degradasi bacaan:

- **Throttling proaktif.** Setiap klien HTTP sumber melalui gerbang kadar dikongsi, thread-safe
  yang merapatkan permintaan keluar kepada bajet yang dikonfigurasi (`App:Calendar:FredRequestsPerMinute`, lalai
  100 — sengaja di bawah siling pembekal). Permintaan di排队 dan dipacukan, tidak pernah di-burst.
- **Menghormati `429 Retry-After`.** Jika pembekal mengembalikan `429 Too Many Requests`, gerbang membuat
  seluruh sumber undur bagi cooldown yang diminta pelayan (atau `App:Calendar:RateLimitBackoff`, lalai 60j)
  sebelum panggilan seterusnya — tiada gelung percubaan semula yang ketat.
- **Ketahanan standard.** Setiap klien sumber juga mewarisi pengendali ketahanan seluruh apl (percubaan semula
  dengan backoff + jitter, circuit breaker, masa lalu), jadi masalah transient diserap dan sumber yang
  gagal secara berterusan diparkir (liputannya menjadi basi) tanpa menjejaskan yang lain.
- **Pelan sandaran — cache baca-melalui yang tahan lama.** Bacaan **tidak pernah** dihidangkan dengan
  memanggil pembekal. Sebaik sahaja julat dijemput ia dikekalkan append-only kepada Postgres dan dihidangkan
  dari sana selama-lamanya (lihat §"Muatkan atas permintaan"). Jadi walaupun sumber had kadar atau turun,
  kalendar terus menjawab dari data cache, tepat-masa; rentang yang hilang kekal tidak diliputi dan
  dicuba semula pada kitaran pengambilan seterusnya. Jawapan blackout tambahan gagal kepada lalai
  konservatif pada ketidakpastian, jadi jurang data tidak pernah secara senyap menghidupkan perdagangan melalui pelepasan.
- **Polling murah.** Jemput bersyarat (ETag / If-Modified-Since / kursor vintage sumber) dan
  "jemput julat sekali, tidak pernah lagi" cache menjadikan jumlah permintaan sebenar jauh di bawah
  sebarang had dalam operasi biasa — gerbang kadar ialah redes keselamatan, bukan laluan biasa.

## Aktifkan / lumpuhkan

Dua tingkatan bebas, sama seperti ciri cMind lain:

- **Tingkat 1 — togol ciri masa jalan** (`Feature.EconomicCalendar`) dialih dari UI admin Ciri;
  tiada redeploy, berkuat kuasa secara langsung.
- **Tingkat 2 — gerbang keras white-label** (`App:Branding:EnableEconomicCalendar`, lalai `true`).
  Penjual semula menetapkannya `false` untuk menanggalkan ciri sepenuhnya; operator kemudian tidak boleh
  menghidupkannya semula.

Keadaan berkesan ialah `Branding.EnableEconomicCalendar && FeatureToggle.EconomicCalendar`. Apabila dilumpuhkan,
masukan nav disembunyikan dan `/economic-calendar`, `/api/calendar/**` dan alat kalendar MCP memulangkan
`404` ciri dilumpuhkan yang kemas — bukan `500`. Sejarah berterusan dikekalkan pada togol masa jalan
jadi menghidupkan semula adalah serta-merta.

## Fasa pengeluaran

- **P0 — teras domain** *(dilaksanakan)*: agregat, objek nilai, port, model impak,
  pemetaan negara→simbol, dasar tetingkap berita, gerbang dua-tingkat, suite unit penuh.
- **P1 — berterusan + satu sumber** *(dilaksanakan)*: skema EF `calendar` (jadual sendiri, append-only,
  indeks panas), pembaca `IEconomicCalendar` baca-melalui dengan `asOf` tepat-masa, perkhidmatan tulis
  idempoten append-only, penyambung FRED di belakang klien taip berdaya tinci, dan pekerja pengambilan
  yang gerbang konfigurasi; ujian integration Testcontainers (berterusan, PIT, idempoten, blackout).
- **P2 — API REST JWT awam + UI Web** *(dilaksanakan)*: API `/api/calendar/v1` berindeks, dijamin
  JWT — pengeluaran klien, pertukaran token, dan titik akhir baca teras (peristiwa, sejarah, siri,
  kejutan, seterusnya, blackout, simbol terjejas, kesihatan) dengan penguatkuasaan skop dan gerbang
  dua-tingkat, diuji integrasi. Campuran **`halaman /economic-calendar`** mudah alih-pertama —
  agenda pelepasan akan datang sebagai kad mesra telefon dengan cip impak berbanding warna dan
  **dialog penapis** MudBlazor (mata wang + impak minimum + **picker tarikh Dari** untuk melompat ke
  **mana-mana** tarikh lalu merentasi sejarah penuh — tiada topeng 60 hari, tiada dinding); masukan nav,
  diuji smoke/mudah alih/a11y/E2E. **Halaman sejarah siri per-indikator** (`/economic-calendar/series/{code}`, dipautkan
  dari setiap peristiwa) menyenaraikan sejarah cetak penuh siri. Carta kejutan + pelayaran
  skrol tak terhingga następ.
- **P3 — lebih banyak sumber & pemanasan** *(dimulakan)*: **katalog siri teras** (CPI, CPI Teras, NFP,
  pengangguran, GDP, PCE, Dana Fed, jualan runcit → ID FRED mereka) disemai secara automatik pada permulaan,
  dan **backfill proaktif** sekali gus, idempoten, berbilang tahun menarik ≥10 tahun sejarah mereka jadi
  kes biasa hangat tanpa menunggu pengguna terlepas. **Pengambilan adalah lalai aktif**
  (`App:Calendar:IngestionEnabled`, lalai `true`): **sumber jadual bank pusat** perlukan **tiada kunci API**,
  jadi kalendar keputusan FOMC / ECB / BoE populate keluar kotak — backfill menyemai tarikh mesyuarat itu
  merentasi **sejarah terkini dan ufuk ke hadapan**, jadi pelayaran *bulan lepas* (atau mana-mana
  tingkap lalu) menunjukkan mesyuarat walaupun sebelum sebarang kunci FRED/BLS dikonfigur; siri nilai
  mengisi sebaik kunci mereka ditetapkan. Pekerja menghormati gerbang dua-tingkat kalendar — penggunaan
  white-label atau pemilik melumpuhkan ciri kalendar ekonomi memberhentikan pengambilan, dan
  `App:Calendar:IngestionEnabled=false` matikannya secara eksplisit. **Kesegaran setiap sumber** juga
  nyata sekarang: pekerja merekodkan undur akhir polls terakhir setiap sumber, bilang gagal berturut-turut
  dan bendera litar trip (berterusan dalam tetapan apl, lintas-proses), dan titik akhir `/health` +
  alat MCP `calendar_health` melaporkan verdict `stale` jujur per sumber. **BLS** (sumber nilai ke-2)
  dan **sumber jadual bank pusat** (tarikh keputusan FOMC / ECB / BoE, backfill merentasi sejarah dan
  disinkron ke ufuk oleh pekerja) sudah ada. Masih akan datang: sumber nilai BEA/Census/ECB-SDW/Eurostat/OECD
  dan laluan penyesuaian.
- **P4 — integrasi mendalam**: **alat MCP** *(dilaksanakan — pariti penuh baca-API: `calendar_events`,
  `calendar_event`, `calendar_history`, `calendar_series`, `calendar_surprises`, `calendar_next`,
  `calendar_blackout`, `calendar_affected_symbols`, `calendar_health`, gerbang pada ciri)* dan
  **pemicu makluman `EconomicEvent`** *(dilaksanakan — `AlertRule` yang mencetuskan N minit sebelum
  pelepasan akan datang pada/lebihan impak dipilih, pilihan terbatas kepada mata wang; dinilai oleh
  pekerja makluman sedia ada tanpa AI, dinyahduplikasi per pelepasan; dicipta melalui
  `POST /api/alerts/rules/economic-event`)*. Gerbang blackout berita prop-guard **dan jeda blackout
  salin-perdagangan** ada (§5.1 — `App:Copy:NewsPauseEnabled` pilihan, lalai off: pesanan terbuka yang
  simbolnya duduk dalam blackout impak Kritikal dilangkau, laluan panas byte-identical apabila off). **Hamparan
  peristiwa backtest** ada — `GET /api/calendar/v1/for-symbol` dan
  alat MCP `calendar_events_for_symbol`Pulangan peristiwa tepat-masa yang betul yang menjejaskan simbol dalam
  tingkap, dan **halaman laporan instance/backtest** memesenkan pelepasan berimpak tinggi yang jatuh
  di dalam tingkap backtest di bawah lengkung ekuiti (jadi pengarang melihat dagangan mana yang mendarat
  pada NFP), gerbang dan lokalisasi. Seluruh pelan sekarang dilaksanakan.
- **P5 — tambahan**: analitik kejutan, eksport iCal/CSV, carian kata kunci, konsensus pluggable.

Lihat [rujukan cBot & REST API](calendar-cbot-api.md) untuk permukaan integrasi.

## Sumber data diperlukan (ciri disembunyikan tanpa satu)

Kalendar memperluhkan nilai sebenar/ramalan/sebelum hanya dari sumber nilai yang dikonfigurasi (FRED atau
BLS). Tanpa `App:Calendar:FredApiKey` atau `App:Calendar:BlsApiKey` ciri itu **disembunyikan** dari
navigasi; jika ia dipaksa hidup (white-label/pemilik) tanpa kunci, halaman mempamerkan notis "konfigurasi
sumber data" yang boleh diambil tindakan ganti-buat nilai kosong, dan tindakan penapis kekal tersembunyi
sehingga sumber ditetapkan. Baris peristiwa mempamerkan **nama** siri (dari katalog), bukan kod siri
mentah.
