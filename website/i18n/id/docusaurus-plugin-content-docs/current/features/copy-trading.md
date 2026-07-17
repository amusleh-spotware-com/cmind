---
description: "Cerminkan akun master cTrader ke satu atau lebih akun slave — lintas broker, lintas cID — dengan kontrol per-tujuan + rekonsiliasi tingkat uang."
---

# Copy trading

Cerminkan akun **master** cTrader ke satu atau lebih akun **slave** — lintas broker, lintas cID — dengan kontrol per-tujuan + rekonsiliasi tingkat uang.

## Konsep

- **Profil copy** — satu master (`SourceAccountId`) + satu atau lebih **tujuan**. Siklus hidup: `Draft → Running → Paused → Stopped` (`Error` saat kegagalan). Akar agregat: `CopyProfile` (memiliki `CopyDestination`).
- **Tujuan** — satu akun slave + set aturan lengkap tentang cara master disalin ke dalamnya. Semua konfigurasi per-tujuan, jadi satu master dapat memberi makan slave konservatif dan agresif secara bersamaan.
- **Host mesin copy** — pekerja yang berjalan untuk profil (`CopyEngineHost`). Berlangganan aliran eksekusi master, menerapkan setiap acara ke setiap tujuan.
- **Supervisor** — `CopyEngineSupervisor`, layanan latar belakang di setiap node. Menampung profil yang ditugaskan, penyembuhan diri di seluruh cluster (lihat [scaling](../deployment/scaling.md)).

## Apa yang dicerminkan

| Acara Master | Aksi Slave |
|--------------|-----------|
| Posisi pasar / pasar-jangkauan terbuka | Buka salinan berukuran (dengan label id posisi sumber) |
| Perintah pending limit / stop / stop-limit | Tempatkan perintah pending yang sesuai |
| Amend perintah pending | Amend perintah pending yang dicerminkan di tempat |
| Batal perintah pending / kedaluwarsa | Batalkan perintah pending yang dicerminkan |
| Penutupan parsial | Tutup proporsi yang sama dari posisi slave |
| Scale-in (peningkatan volume) | Buka volume yang ditambahkan (opt-in) |
| Perubahan stop-loss / trailing-stop | Amend perlindungan posisi slave |
| Penutupan penuh | Tutup salinan slave |

Setiap salinan **diberi label dengan id posisi/perintah sumber**. Setelah sambungan ulang, host merekonstruksi state dari rekonsiliasi: membuka salinan yang dipegang master tetapi slave hilang, menutup "yatim piatu" slave yang master tidak lagi pegang — **tanpa menduplikasi perdagangan**.

## Membuat profil

**Profil Baru** membuka formulir **halaman penuh** yang didedikasikan (`/copy-trading/new`), bukan dialog — set opsi cukup besar sehingga halaman lebih baik dibaca di ponsel dan desktop. Ia mengumpulkan semuanya di muka: nama profil, sumber (master) akun, tujuan (slave) akun (multi-pilih dengan tombol **Pilih semua**; master yang dipilih dikecualikan dari daftar slave), + set opsi per-tujuan lengkap. **Setiap kontrol membawa tooltip bantuan** yang menjelaskan apa fungsinya dan cara menggunakannya. Input terstruktur menggunakan **kontrol tervalidasi yang tepat** — angka/persen melalui bidang numerik, mode/arah/filter melalui pilihan, filter simbol melalui daftar tambah/hapus chip simbol, dan peta simbol melalui tabel tambah/hapus `Sumber → Tujuan (× pengganda)` — tidak pernah blob teks yang dipisahkan koma. Semua input **divalidasi sebelum menyimpan** — nama/sumber/tujuan hilang, parameter sizing non-positif, batas banyak negatif/tidak konsisten, persen drawdown di luar jangkauan, tidak ada tipe pesanan yang diaktifkan, atau filter simbol kosong muncul sebagai daftar kesalahan + blok simpan. Saat membuat, profil dibuat + setiap slave yang dipilih ditambahkan dengan pengaturan yang dipilih, kemudian halaman kembali ke daftar Copy Trading.

**Impor / ekspor.** Seluruh blok pengaturan dapat **dieksport ke file JSON** dan di-**impor kembali** untuk mengisi formulir sebelumnya, jadi penyetelan dapat digunakan kembali di seluruh profil tanpa pengetikan ulang. Peta simbol dapat demikian pula **dieksport / diimpor sebagai file CSV** (`Source,Destination,VolumeMultiplier`) — siapkan peta simbol broker besar di spreadsheet dan muat dalam satu langkah. Kontrol simbol dan impor/ekspor CSV yang sama juga tersedia dalam dialog tujuan di halaman Copy Trading.

Tindakan baris menghormati siklus hidup: **Start** diaktifkan hanya jika tidak berjalan, **Stop** + **Pause** hanya saat berjalan, **Delete** dinonaktifkan saat berjalan + minta konfirmasi sebelum menghapus profil + tujuan.

## Opsi per-tujuan

Diatur pada halaman Profil Baru, dalam dialog tujuan di halaman Copy Trading, atau melalui `POST /api/copy/profiles/{id}/destinations`:

- **Sizing** (`MoneyManagementMode` + parameter): banyak tetap, pengganda banyak/notional, keseimbangan/ekuitas/margin bebas proporsional, risiko tetap %, leverage tetap, proporsi otomatis, **risiko-%-dari-stop** (M7). Ditambah batas banyak min/maks + force-min-lot. **Risk-from-stop** mengubah ukuran tujuan sehingga risiko persen yang dikonfigurasi dari **jarak stop-loss master** (`master risiko 2% → slave auto-risiko 2%`): `lots = balance×% ÷ (stopDistance × contractSize)`. Master terbuka **tanpa** stop-loss tidak memiliki jarak untuk mengubah ukuran terhadapnya → menggunakan **max-risk fallback lot** yang dikonfigurasi (M7) jika diatur, else dilewati (`no_stop_loss`) tidak ditebak. Ukuran proporsional-**ekuitas**/**margin bebas** dari **ekuitas** akun nyata (`balance + Σ floating P&L`, berasal per cTrader Open API yang tidak memberikan ekuitas), bukan keseimbangan polos — jadi master duduk di keuntungan/kerugian terbuka mengubah ukuran salinan dengan benar. Margin yang digunakan tidak diekspos oleh reconcile API, jadi margin bebas diperlakukan sebagai ekuitas (proksi dana yang tersedia jujur); mode lain membaca keseimbangan + lewati putaran revaluasi ekstra.
- **Filter arah**: kedua / panjang saja / pendek saja. **Balik**: balik sisi (+ tukar SL↔TP) untuk salinan yang bertentangan.
- **Manage-only** (Ignore-New-Trades / Close-Only): cerminkan penutupan, penutupan parsial + perubahan perlindungan pada posisi yang sudah disalin, tetapi buka **tidak ada** posisi/perintah pending baru (dilewati `manage_only`). Gunakan untuk menutup tujuan tanpa memotong salinan yang ada.
- **Sync-Open-on-start** / **Sync-Closed-on-start** (default aktif): pada **pertama kali** resync profil, apakah membuka salinan untuk posisi pra-ada master, + apakah menutup salinan master ditutup saat profil berhenti. Keduanya hanya berlaku saat mulai — sambungan ulang mid-run selalu merekonsiliasi penuh sehingga desync pulih terlepas dari apakah keduanya diatur.
- **Peta simbol** + **filter simbol** (whitelist / blacklist). Setiap entri peta-simbol membawa pengganda volume **per-simbol** opsional (penggantian per-simbol cMAM) mengubah ukuran salinan untuk simbol itu di atas sizing tujuan (1 = tidak ada perubahan). Peta keseluruhan mengimpor/mengekspor sebagai **CSV** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; kolom `Source,Destination,VolumeMultiplier`) — setiap baris divalidasi melalui objek nilai domain, jadi file yang tidak terbentuk tidak dapat menghasilkan peta yang tidak valid.
- **Jendela jam perdagangan** (C18) — jendela UTC harian per-tujuan (`start`/`end` menit-hari, akhir eksklusif; `start == end` = sepanjang hari). Pembukaan baru di luar jendela dilewati (`trading_hours`); jendela dengan `start > end` membungkus melewati tengah malam (mis. 22:00–06:00). Posisi yang ada tetap dikelola.
- **Filter label sumber** (C18, setara cTrader dari filter magic-number MT) — saat diatur, salin hanya perdagangan master yang labelnya cocok **dengan tepat** (mis. perdagangan satu bot, atau label manual saja); else dilewati (`source_label`). Kosong = salin semua. Dibawa pada `ExecutionEvent.SourceLabel` dari label posisi/perintah master `TradeData.Label`, dihormati saat resync juga.
- **Perlindungan akun** (ZuluGuard / Perlindungan Akun Global) — tonton **ekuitas langsung** tujuan (`balance + Σ floating P&L`, diundur setiap `CopyDefaults.EquityGuardInterval`) melawan lantai `StopEquity` dan/atau batas opsional `TakeEquity`. Pada pelanggaran, terapkan mode: **CloseOnly** (hentikan salinan baru, terus mengelola yang ada), **Frozen** (hentikan pembukaan), **SellOut** (tutup **setiap** salinan pada tujuan segera). Setelah diaktifkan, tujuan tergantung — tidak ada pembukaan baru sampai host dimulai ulang — + alert `CopyAccountProtectionTriggered` dimunculkan. `SellOut` memerlukan `StopEquity`; `TakeEquity` harus duduk di atas `StopEquity`. **Kaveat tanpa jaminan:** sell-out menggunakan eksekusi pasar — seperti setara kompetitor, tidak dapat menjamin harga pengisian di pasar cepat/gapped.
- **Tombol panik Flatten-All** (C8) — `POST /api/copy/profiles/{id}/flatten` segera menutup **setiap** posisi yang disalin pada setiap tujuan + mengunci terhadap pembukaan baru. Dirutekan lintas-proses: API menetapkan flag, supervisor mengirimkan ke host yang sedang berjalan (menggunakan kembali saluran rotasi token), yang meratakan di tempat; flag dihapus jadi api tepat sekali (`CopyFlattenAll` alert). Pengguna kemudian menjeda/menghentikan profil.
- **Penjaga aturan prop-firm** (C7) — penjaga penegakan pengguna copier prop-firm minta. Per tujuan, **batas kerugian harian** (kerugian dari ekuitas pembukaan hari) dan/atau batas **trailing-drawdown** (kerugian dari puncak ekuitas berjalan), keduanya dalam mata uang simpanan. Pada tujuan pelanggaran **auto-flattened** (setiap salinan ditutup) + **dikunci keluar** sisa hari UTC (pembukaan baru dilewati `prop_lockout`); alert `CopyPropRuleBreached` api. Lockout membersihkan saat hari UTC bergulir (baseline/peak fresh diambil). Berbagi jajak ekuitas langsung yang sama dengan perlindungan akun.
- **Jitter eksekusi** (C11, off secara default) — penundaan acak `0..N` ms sebelum menempatkan setiap salinan, untuk de-korelasi timestamp pesanan yang hampir identik di seluruh akun **sendiri** pengguna. **Kaveat kepatuhan:** bantuan untuk prop firm yang *mengizinkan* penyalinan — **tidak** alat untuk menghindari firma yang melarangnya; tetap dalam aturan firma Anda adalah tanggung jawab Anda.
- **Kunci konfigurasi** (C9) — bekukan pengaturan tujuan untuk periode (`POST …/destinations/{id}/lock` dengan menit). Saat terkunci, tujuan tidak dapat dihapus (agregat menolak dengan `CopyDestinationConfigLocked`) — penjaga yang disengaja terhadap perubahan impulsif selama drawdown. Kunci kedaluwarsa secara otomatis pada timestamp-nya.
- **Pra-alert konsistensi** (C10) — peringatan (sekali per hari UTC) saat **keuntungan harian** tujuan mencapai persen yang dikonfigurasi dari ekuitas pembukaan hari (`CopyConsistencyThresholdApproaching`), jadi aturan konsistensi prop-firm dihormati *sebelum* api. Sisi keuntungan, independen dari lockout sisi kerugian; berjalan dari baseline hari yang sama dengan penjaga aturan prop.
- **Filter tipe pesanan** — pilih tepat jenis pesanan master mana yang akan disalin: pasar, jangkauan pasar, batas, berhenti, stop-limit (`CopyOrderTypes` flag; default semua). Selektivitas gaya cMAM.
- **Salin SL / Salin TP** — cerminkan stop-loss / take-profit master, atau kelola perlindungan secara independen.
- **Salin trailing stop**, **cerminkan penutupan parsial**, **cerminkan scale-in** — masing-masing dapat dialihkan secara independen.
- **Salin kedaluwarsa pending** (default aktif) — cerminkan timestamp kedaluwarsa Good-Till-Date perintah pending master.
- **Salin slippage master** (default aktif) — untuk perintah pasar-jangkauan + stop-limit, tempatkan pesanan slave dengan slippage-in-points master yang tepat (harga dasar diambil dari spot langsung slave).
- **Penjaga**: drawdown maks %, batas kerugian harian, keterlambatan salinan maks, filter slippage (lewati salinan jika harga slave bergerak melampaui N pip dari entri master). **Max copy delay** diukur terhadap timestamp server nyata acara master (`ExecutionEvent.ServerTimestamp`) melalui `TimeProvider` yang disuntikkan: sinyal lebih tua dari max-lag yang dikonfigurasi dilewati, jadi stale copy tidak pernah ditempatkan terlambat (sebelumnya delay selalu nol + penjaga mati).
- **Normalisasi presisi SL/TP** (M6) — stop-loss/take-profit yang disalin dibulatkan ke presisi digit simbol **tujuan** sebelum amend, jadi harga master pada presisi lebih halus (atau ketidakcocokan digit lintas-broker) tidak pernah api server `INVALID_STOPLOSS_TAKEPROFIT`.
- **Pemutus sirkuit penolakan / Penjaga Pengikut** (G8) — tujuan menolak pembukaan `CopyDefaults.RejectionBudget` berturut-turut **dipicu**: tidak ada pembukaan baru untuk jendela cooldown (`CopyDestinationTripped` alert api), menghentikan badai penolakan dari menghantam (prop-firm) akun. Posisi yang ada masih dikelola + ditutup saat dipicu; pemutus auto-reset setelah cooldown + salinan yang berhasil menghapus counter.
- **Batas akal banyak** (C14) — ukuran salinan maks absolut dan/atau batas kelipatan-master. Salinan yang dihitung melampaui batas absolut, atau melampaui `N×` ukuran banyak master sendiri, **hard-blocked** (ditampilkan sebagai skip `lot_sanity`, dihitung pada `cmind.copy.skipped`) tidak ditempatkan — mempertahankan terhadap kelas oversize bencana (master 0.23-lot berubah menjadi 3 banyak pada setiap penerima melalui pengganda pelarian atau bug pembulatan). Kedua dimensi default `0` (off).

## Keandalan & kasus tepi

Mesin dibangun untuk kenyataan bahwa apa pun dapat gagal kapan saja:

- **Timeout korelasi pengisian pending slave** (C13) — pending slave yang dicerminkan yang pending master menghilang (baik tidak diam atau baru saja diisi) dibatalkan setelah timeout korelasi, jadi salinan slave tidak dapat diisi tidak berkorelasi ke posisi yang tidak dikelola (`CopyPendingTimedOut`). Resync juga membersihkan yatim piatu pending-id-labelled yang diisi.
- **Penutupan/flatten yang kuat** (M8) — menutup yatim piatu pada resync, atau meratakan pada pelanggaran penjaga, mentoleransi posisi broker sudah ditutup (`POSITION_NOT_FOUND`): setiap penutupan berjalan secara independen, jadi satu id stale tidak pernah menghentikan resync atau meninggalkan sisa akun yang tidak diratakan.

- **Mulai dengan master sudah dalam perdagangan** — saat mulai host merekonsiliasi + membuka salinan untuk posisi yang ada dari master.
- **Koneksi putus / desync** — pada sambungan ulang host merekonsiliasi: membuka salinan yang hilang, menutup yatim piatu, re-label pendings. Tidak ada pesanan duplikat.
- **Kegagalan penempatan pesanan** — kegagalan pada satu tujuan dicatat, tidak pernah memblokir tujuan lain.
- **Token valid tunggal per cID** — cTrader menginvalidasi token akses lama cID segera token baru diterbitkan. cMind menukar token host yang sedang berjalan **di tempat** (re-auth pada soket langsung) sehingga penyalinan berlanjut tanpa menjatuhkan aliran. Lihat [token lifecycle](token-lifecycle.md).

## Audit

Setiap tindakan memancarkan acara log terstruktur yang dihasilkan sumber (`LogMessages`) dengan id profil, cID tujuan, id pesanan/posisi, + nilai — pesanan ditempatkan/dilewati (dengan alasan), penutupan parsial, perlindungan diterapkan, trailing diterapkan, pending ditempatkan/diamend/dibatalkan, kedaluwarsa dicerminkan, slippage pasar-jangkauan dicerminkan, token ditukar, ringkasan resync. Ini adalah jejak audit untuk kepatuhan + resolusi sengketa.

Bersama log, mesin memancarkan **metrik OpenTelemetry** pada meter `cMind.Copy` (terdaftar dalam pipeline OTel bersama, diekspor lebih OTLP / ke Azure Monitor seperti sisanya): `cmind.copy.latency` (master-event → dispatch, ms), `cmind.copy.dispatch.duration` (fan-out ke semua tujuan, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (ditandai oleh tujuan), `cmind.copy.skipped` (ditandai oleh alasan), + `cmind.copy.failed`. Ini membuat regresi latensi/slippage dapat diukur, bukan hanya terlihat dalam baris log — suite langsung menegaskan terhadap anggaran.

## API

- `GET /api/copy/profiles` — daftar.
- `POST /api/copy/profiles` — buat (dengan id akun tujuan opsional).
- `GET /api/copy/profiles/{id}` — detail lengkap termasuk setiap opsi tujuan.
- `POST /api/copy/profiles/{id}/destinations` — tambahkan tujuan dengan set opsi lengkap.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — hapus.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — siklus hidup.

## Tes

- **Unit** (`tests/UnitTests/CopyTrading`) — mode sizing, filter keputusan, filter tipe pesanan, salinan kedaluwarsa, slippage pasar-jangkauan/stop-limit, toggle SL/TP, penutupan parsial, amend/batal pending, mulai-dengan-terbuka, putus→desync→resync, tukar token di tempat, penolakan cross-cID. Berjalan terhadap `FakeTradingSession`, simulator in-memory yang setia cTrader.
- **Integrasi** (`tests/IntegrationTests/CopyLive`) — klaim afinitas-node/lease, propagasi token-version pada Postgres asli.
- **E2E** (`tests/E2ETests`) — putaran pilihan tujuan melalui API + UI, siklus hidup penuh.
- **Stres / DST** (`tests/StressTests`) — deterministic-simulation testing: beban kerja acak yang disemai + injeksi kesalahan (soket flap, penolakan pesanan, penolakan pasar-jangkauan, rotasi token, kematian node) mendorong `CopyEngineHost` ke quiescence + menegaskan invarian konvergensi. Lihat [testing/stress-testing.md](../testing/stress-testing.md). Suite ini ditampilkan + diperbaiki balapan startup asli: `OnReconnected` terikat sebelum ref-load awal + resync, jadi flap soket selama startup bisa menjalankan resync kedua secara bersamaan + merusak kamus state non-concurrent host — startup load + resync pertama sekarang berjalan di bawah `_stateGate`.
- **Langsung** — akun demo cTrader asli; lihat [testing/live-copy-trading.md](../testing/live-copy-trading.md).

Lihat [dev-credentials.md](../testing/dev-credentials.md) untuk file kredensial tunggal tier langsung + E2E baca.
## Kontrol profil dan manajemen tujuan

Start/stop adalah tombol ikon pada setiap baris profil (dinonaktifkan saat tindakan tidak berlaku). Akun sumber dan tujuan ditunjukkan oleh **nomor akun** mereka, tidak pernah id internal. Mengklik profil membuka **dialog** untuk mengelola akun tujuannya (tambah/hapus dengan pengaturan per-tujuan lengkap).
