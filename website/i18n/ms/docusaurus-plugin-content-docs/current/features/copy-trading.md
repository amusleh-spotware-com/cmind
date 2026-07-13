---
description: "Cermin akaun master cTrader ke satu + akaun slave — lintas-broker, lintas-cID — dengan kawalan setiap tujuan + penyerasian gred wang."
---

# Salinan perdagangan

Cermin akaun **master** cTrader ke satu + akaun **slave** — lintas-broker, lintas-cID — dengan kawalan setiap tujuan + penyerasian gred wang.

## Konsep-konsep

- **Profil salinan** — satu master (`SourceAccountId`) + satu+ **tujuan**. Kitaran hayat: `Draft → Running → Paused → Stopped` (`Error` pada kegagalan). Akar agregat: `CopyProfile` (memiliki `CopyDestination`).
- **Tujuan** — satu akaun slave + set peraturan penuh untuk bagaimana master disalin ke atasnya. Semua config setiap tujuan, jadi satu master memberi makan slave konservatif + agresif sekali.
- **Hos enjin salinan** — pekerja yang berjalan untuk profil (`CopyEngineHost`). Melanggan aliran pelaksanaan master, menerapkan setiap peristiwa ke setiap tujuan.
- **Penyelia** — `CopyEngineSupervisor`, perkhidmatan latar belakang pada setiap nod. Hos profil yang ditugaskan, penyembuhan diri merentasi kluster (lihat [penskalaan](../deployment/scaling.md)).

## Apa yang mendapat cermin

| Peristiwa Master | Tindakan Slave |
|--------------|--------------|
| Kedudukan pasaran / pasaran-julat terbuka | Buka salinan bersaiz (berlabel dengan id kedudukan sumber) |
| Perintah alam sekitar / henti / henti-had tertunda | Letakkan perintah alam sekitar yang sepadan |
| Amend perintah alam sekitar tertunda | Amend perintah alam sekitar yang tercermin di tempat |
| Perintah alam sekitar batal / tamat tempoh | Batalkan perintah alam sekitar yang tercermin |
| Penutup sebahagian | Tutup perkadaran yang sama daripada kedudukan slave |
| Skala-masuk (peningkatan volum) | Buka volum yang ditambah (pilihan) |
| Perubahan kehilangan-henti / henti-seret | Amend perlindungan kedudukan slave |
| Penutup penuh | Tutup salinan slave |

Setiap salinan **berlabel dengan id kedudukan sumber/pesanan**. Selepas sambung semula hos membina semula keadaan daripada penyerasian: membuka salinan master memegang tetapi slave hilang, menutup "yatim piatu" slave master tidak lagi pegang — **tanpa menduplikasi dagangan**.

## Membuat profil

Dialog **Profil Baharu** di halaman Salinan Perdagangan mengumpul semua didepan: nama profil, sumber (master) akaun, tujuan (slave) akaun (multi-pilih dengan butang **Pilih semua**; master yang dipilih dikecualikan daripada senarai slave), + set pilihan setiap tujuan penuh di bawah. Semua input **disahkan sebelum menyimpan** — nama/sumber/tujuan hilang, parameter saiz tidak positif, batas lot negatif/tidak konsisten, % undur luar julat, tiada jenis pesanan didayakan, penapis simbol kosong, atau pasangan peta simbol yang tidak betul permukaan sebagai senarai ralat + henti penyimpanan. Pada pengesahan, profil dibuat + setiap slave yang dipilih ditambah dengan tetapan yang dipilih.

Tindakan baris menghormati kitaran hayat: **Mula** diaktifkan hanya apabila tidak berjalan, **Henti** + **Jeda** hanya apabila berjalan, **Padam** dilumpuhkan semasa berjalan + tanya pengesahan sebelum membuang profil + tujuan.

## Pilihan setiap tujuan

Ditetapkan dalam dialog Profil Baharu, pada panel setiap tujuan halaman Salinan Perdagangan, atau melalui `POST /api/copy/profiles/{id}/destinations`:

- **Saiz** (`MoneyManagementMode` + parameter): lot tetap, lot/notional multiplier, keseimbangan proporsional/ekuiti/margin-bebas, % risiko tetap, leverage tetap, auto-proporsional, **risiko-%-daripada-henti** (M7). Tambah had lot min/max + paksakan-min-lot. **Risiko-daripada-henti** saiz tujuan jadi ia mengambil risiko % yang dikonfigurasi *ekuitinya sendiri*, yang diperoleh daripada **jarak henti-henti master** (`master risiko 2% → slave auto-risiko 2%`): `lot = keseimbangan×% ÷ (stopDistance × contractSize)`. Master membuka **tanpa** kehilangan-henti tidak mempunyai jarak untuk saiz terhadap → menggunakan **lot fallback risiko-max yang dikonfigurasi** (M7) jika ditetapkan, sebaliknya dilewatkan (`no_stop_loss`) tidak diteka. Saiz proporsional-**ekuiti**/**margin-bebas** daripada **ekuiti** akaun sebenar (`keseimbangan + Σ terapung P&L`, diperoleh setiap cTrader Open API yang tidak memberikan ekuiti), bukan keseimbangan biasa — jadi master duduk di keuntungan/kerugian terbuka saiz salinan dengan betul. Margin yang digunakan tidak disedari oleh API penyerasian, jadi margin-bebas dianggap sebagai ekuiti (proksi dana-tersedia yang jujur); mod lain membaca keseimbangan + lewatkan putar semula penilaian semula.
- **Penapis arah**: kedua-duanya / panjang sahaja / pendek sahaja. **Balikkan**: flip side (+ swap SL↔ TP) untuk salinan kontrarian.
- **Urus sahaja** (Abaikan-Dagangan-Baharu / Tutup-Sahaja): cermin tutup, tutup sebahagian + perubahan perlindungan pada kedudukan yang sudah disalin, tetapi buka **tidak ada** kedudukan/pesanan alam sekitar baru (dilewatkan `manage_only`). Gunakan untuk mengurangi tujuan tanpa memotong salinan sedia ada.
- **Segerakkan-Terbuka-pada-permulaan** / **Segerakkan-Tertutup-pada-permulaan** (lalai hidup): pada **pertama** penyegerakan semula profil, sama ada untuk membuka salinan untuk kedudukan sedia ada master, + sama ada untuk menutup salinan master ditutup semasa profil dihentikan. Keduanya hanya terpakai pada permulaan — jangka tengah sambung semula sentiasa menyerasikan sepenuhnya jadi desync pulih tanpa mengira.
- **Peta simbol** + **penapis simbol** (senarai putih / senarai hitam). Setiap entri peta simbol membawa multiplier volum pilihan **setiap simbol** (cMAM overide setiap simbol) saiz salinan saiz untuk simbol itu di atas saiz tujuan (1 = tiada perubahan). Seluruh peta import/eksport sebagai **CSV** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; lajur `Source,Destination,VolumeMultiplier`) — setiap baris disahkan melalui objek nilai domain, jadi fail yang tidak betul tidak boleh menghasilkan peta tidak sah.
- **Tetingkap jam perdagangan** (C18) — tetingkap UTC harian setiap tujuan (`mula`/`akhir` minit-hari, akhir eksklusif; `mula == akhir` = sepanjang hari). Pembukaan baru di luar tetingkap dilewatkan (`trading_hours`); tetingkap dengan `mula > akhir` membungkus yang lalu tengah malam (cth 22:00–06:00). Kedudukan sedia ada tetap diuruskan.
- **Penapis label sumber** (C18, cTrader setara dengan penapis bilangan ajaib MT) — apabila ditetapkan, salinan hanya dagangan master yang labelnya sepadan **tepat** (cth dagangan bot satu, atau label-manual sahaja); sebaliknya dilewatkan (`source_label`). Kosong = salin semua. Dibawa pada `ExecutionEvent.SourceLabel` daripada kedudukan master/pesanan `TradeData.Label`, dihormati pada penyegerakan semula juga.
- **Perlindungan akaun** (ZuluGuard / Perlindungan Akaun Global) — tonton **ekuiti hidup** tujuan (`keseimbangan + Σ terapung P&L`, diundi setiap `CopyDefaults.EquityGuardInterval`) terhadap lantai `StopEquity` dan/atau siling pilihan `TakeEquity`. Pada pelanggaran, gunakan mod: **TutupSahaja** (berhenti salinan baru, simpan mengurus sedia ada), **Beku** (berhenti membuka), **Jual** (tutup **setiap** salinan di tujuan segera). Setelah terpicu, tujuan terkunci — tiada pembukaan baru sehingga hos mula semula — + amaran `CopyAccountProtectionTriggered` dinaikkan. `SellOut` memerlukan `StopEquity`; `TakeEquity` mesti duduk di atas `StopEquity`. **Kaveat tanpa jaminan:** jual-keluar menggunakan pelaksanaan pasaran — seperti setara pesaing setiap orang, tidak boleh menjamin harga isi dalam pasaran pantas/celah.
- **Butang panik Ratakan-Semua** (C8) — `POST /api/copy/profiles/{id}/flatten` segera menutup **setiap** salinan kedudukan pada setiap tujuan + kunci terhadap pembukaan baru. Diutamakan lintas-proses: API menetapkan bendera, penyelia memberikan kepada hos yang berjalan (menggunakan semula saluran putaran token), yang meratakan di tempat; bendera jelas jadi tembakan tepat sekali (`CopyFlattenAll` amaran). Pengguna kemudian menjeda/menghentikan profil.
- **Pengawal peraturan prop-firm** (C7) — perlaksanaan pengguna salinan prop-firm minta. Setiap tujuan, **had kerugian harian** (kerugian daripada ekuiti pembukaan hari) dan/atau **had undur seret** (kerugian daripada puncak ekuiti yang berjalan), kedua-duanya dalam mata wang deposit. Pada pelanggaran tujuan **auto-rata** (setiap salinan ditutup) + **terkunci keluar** sisa hari UTC (pembukaan baru dilewatkan `prop_lockout`); amaran `CopyPropRuleBreached` tembakan. Penguncian keluar jelas apabila hari UTC berubah (asas segar/puncak diambil). Berkongsi undian ekuiti hidup yang sama sebagai perlindungan akaun.
- **Getaran pelaksanaan** (C11, matikan secara lalai) — kelewatan rawak `0..N` ms sebelum meletakkan setiap salinan, untuk menghubungkan waktu-cap pesanan yang hampir sama di akaun **mereka sendiri**. **Kaveat kepatuhan:** bantuan untuk prop firm yang *membenarkan* penyalinan — **bukan** alat untuk mengelak firm yang melarang; tetap dalam peraturan firm anda adalah tanggungjawab anda.
- **Kunci config** (C9) — bekukan tetapan tujuan untuk tempoh (`POST …/destinations/{id}/lock` dengan minit). Semasa terkunci, tujuan tidak boleh dialih keluar (agregat menolak dengan `CopyDestinationConfigLocked`) — pengawal yang disengajakan terhadap perubahan impulsif semasa undur. Kunci tamat secara automatik pada sampel masanya.
- **Amaran pra-konsistensi** (C10) — amaran (sekali setiap hari UTC) apabila **keuntungan harian** tujuan mencapai % yang dikonfigurasi daripada ekuiti pembukaan hari (`CopyConsistencyThresholdApproaching`), jadi peraturan konsistensi prop-firm dihormati *sebelum* ia menjerat. Keuntungan-sisi, bebas daripada penguncian sisi kerugian; berjalan dari asas hari yang sama sebagai penjaga peraturan prop.
- **Penapis jenis pesanan** — pilih betul jenis pesanan master mana untuk disalin: pasaran, pasaran-julat, had, henti, henti-had (`CopyOrderTypes` bendera; lalai semua). Selektiviti gaya cMAM.
- **Salin SL / Salin TP** — cermin kehilangan-henti / ambil-keuntungan master, atau uruskan perlindungan secara bebas.
- **Salin henti seret**, **cermin tutup sebahagian**, **cermin skala-masuk** — masing-masing dapat dipilih secara bebas.
- **Salin tamat tempoh alam sekitar** (lalai hidup) — cermin cap tanggal baik-sehingga-cap pesanan alam sekitar tertunda master.
- **Salin gelincir master** (lalai hidup) — untuk pesanan pasaran-julat + henti-had, letakkan pesanan slave dengan gelincir-dalam-mata tepat master (harga asas diambil daripada tempat hidup slave).
- **Pengawal**: undur max %, had kerugian harian, kelewatan salinan max, penapis gelincir (lewatkan salinan jika harga slave bergerak melampaui N mata dari entri master). **Kelewatan salinan max** diukur terhadap cap waktu pelayan sebenar peristiwa master (`ExecutionEvent.ServerTimestamp`) melalui `TimeProvider` yang disuntik: isyarat lebih lama daripada ketinggalan-max yang dikonfigurasi dilewatkan, jadi salinan basi tidak pernah diletakkan lewat (sebelumnya kelewatan sentiasa sifar + pengawal mati).
- **Normalisasi ketepatan SL/TP** (M6) — harga kehilangan-henti/ambil-keuntungan yang disalin dibulatkan kepada **tujuan** ketepatan digit simbol sebelum amend, jadi harga master pada ketepatan lebih halus (atau ketidakpadanan digit lintas-broker) tidak pernah memicu pelayan `INVALID_STOPLOSS_TAKEPROFIT`.
- **Pemutus litar penolakan / Pengawal Pengikut** (G8) — tujuan menolak pembukaan `CopyDefaults.RejectionBudget` berturut-turut adalah **terpicu**: tiada pembukaan baru untuk tetingkap penyejukan (`CopyDestinationTripped` amaran tembakan), menghentikan badai penolakan daripada menghentak (prop-firm) akaun. Kedudukan sedia ada masih diuruskan + ditutup semasa terpicu; pemutus auto-set semula selepas penyejukan + salinan berjaya membersihkan kaunter.
- **Siling akal budi lot** (C14) — saiz salinan max mutlak dan/atau had berganda-daripada-master. Lot salinan yang dikira melebihi had mutlak, atau melebihi `N×` saiz lot master sendiri, **keras-diblok** (permukaan sebagai lewatan `lot_sanity`, dikira pada `cmind.copy.skipped`) tidak diletakkan — mempertahankan terhadap kelas saiz terlalu besar yang bencana (master 0.23-lot bertukar menjadi 3 lot pada setiap penerima melalui multiplier atau pepijat pembulatan) yang lari. Kedua-dua dimensi lalai `0` (matikan).

## Kebolehpercayaan & kes tepi

Enjin dibina untuk realiti bahawa apa-apa boleh gagal setiap saat:

- **Tamat masa penyesuaian pengisian tertunda slave** (C13) — slave alam sekitar tertunda yang master alam sekitar hilang (tidak berehat atau baru-baru ini diisi) dibatalkan selepas tamat masa penyesuaian, jadi salinan slave tidak boleh mengisi tidak berkorelasi ke kedudukan tanpa diurus (`CopyPendingTimedOut`). Penyegerakan semula juga membersihkan yatim piatu pesanan-id-berlabel pengisian alam sekitar.
- **Tutup/ratakan yang teguh** (M8) — menutup yatim piatu pada penyegerakan semula, atau meratakan pada pelanggaran penjaga, bertolak ansur dengan kedudukan broker sudah ditutup (`POSITION_NOT_FOUND`): setiap tutup berjalan secara bebas, jadi satu id basi tidak pernah menghentikan penyegerakan semula atau meninggalkan salinan akaun sida tidak-rata.

- **Mula dengan master sudah dalam dagangan** — pada hos permulaan menyerasikan + membuka salinan untuk kedudukan sedia ada master.
- **Sambungan jatuh / desync** — pada hos sambung semula menyerasikan: membuka salinan hilang, menutup yatim piatu, melabel ulang alam sekitar. Tiada pesanan berganda.
- **Kegagalan penempatan pesanan** — kegagalan di satu tujuan dicatat, tidak pernah menyekat tujuan lain.
- **Token sah tunggal setiap cID** — cTrader membatalkan token akses cID lama masa baru dikeluarkan. cMind menukar token hos yang berjalan **di tempat** (auth semula pada soket hidup) jadi penyalinan terus tanpa menjatuhkan aliran. Lihat [kitaran hayat token](token-lifecycle.md).

## Kebolehaudit

Setiap tindakan memancarkan acara log yang berstruktur, yang dihasilkan sumber (`LogMessages`) dengan id profil, tujuan cID, id pesanan/kedudukan, + nilai — pesanan diletakkan/dilewatkan (dengan alasan), tutup sebahagian, perlindungan digunakan, seret digunakan, alam sekitar diletakkan/dipinda/dibatalkan, tamat tempoh tercermin, gelincir pasaran-julat tercermin, token ditukar, ringkasan penyegerakan semula. Ini adalah jejak audit untuk kepatuhan + penyelesaian pertikaian.

Bersama log, enjin memancarkan **metrik OpenTelemetry** pada meter `cMind.Copy` (terdaftar dalam saluran OTel bersama, dieksport melalui OTLP / ke Azure Monitor seperti selebihnya): `cmind.copy.latency` (peristiwa master → hantar, ms), `cmind.copy.dispatch.duration` (kipas-keluar ke semua tujuan, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (ditandai oleh tujuan), `cmind.copy.skipped` (ditandai oleh alasan), + `cmind.copy.failed`. Ini membuat latency/gelincir regresi boleh diukur, bukan hanya terlihat dalam baris log — suite hidup mempertahankan mereka terhadap belanjawan.

## API

- `GET /api/copy/profiles` — senarai.
- `POST /api/copy/profiles` — buat (dengan id akaun tujuan pilihan).
- `GET /api/copy/profiles/{id}` — butiran penuh termasuk setiap pilihan tujuan.
- `POST /api/copy/profiles/{id}/destinations` — tambah tujuan dengan set pilihan penuh.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — alih keluar.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — kitaran hayat.

## Ujian-ujian

- **Unit** (`tests/UnitTests/CopyTrading`) — mod saiz, penapis keputusan, penapis jenis pesanan, salinan tamat tempoh, gelincir pasaran-julat/henti-had, togol SL/TP, tutup sebahagian, amend alam sekitar/batal, mula-dengan-terbuka, putus→desync→penyegerakan semula, tukar token di tempat, penafian lintas-cID. Berjalan terhadap `FakeTradingSession`, simulator dalam-memori yang setia cTrader.
- **Integrasi** (`tests/IntegrationTests/CopyLive`) — klaimaft-nod/klaim pajakan, penyebaran versi token pada Postgres sebenar.
- **E2E** (`tests/E2ETests`) — putaran pilihan tujuan melalui API + UI, kitaran hayat penuh.
- **Tekanan / DST** (`tests/StressTests`) — pengujian simulasi-penentu: beban kerja rawak benih + suntikan kesalahan (soket lepas, penolakan pesanan, penolakan pasaran-julat, putaran token, kematian nod) mendorong `CopyEngineHost` ke ketenangan + mempertahankan invarian penumpuan. Lihat [testing/stress-testing.md](../testing/stress-testing.md). Suite ini dipermukaankan + diperbaiki perlumbaan permulaan sebenar: `OnReconnected` terkabel sebelum beban rujukan awal + penyegerakan semula, jadi soket lepas semasa permulaan boleh menjalankan penyegerakan semula kedua secara serentak + memusnahkan kamus keadaan non-serentak hos — beban permulaan + penyegerakan semula pertama kini berjalan di bawah `_stateGate`.
- **Hidup** — akaun demo cTrader sebenar; lihat [testing/live-copy-trading.md](../testing/live-copy-trading.md).

Lihat [dev-credentials.md](../testing/dev-credentials.md) untuk fail kredensial tunggal live + peringkat E2E baca.
