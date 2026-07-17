---
description: "Cerminkan akaun master cTrader ke satu atau lebih akaun slave — lintas-broker, lintas-cID — dengan kawalan per-destinasi + rekonsiliasi gred wang."
---

# Perdagangan salinan

Cerminkan akaun **master** cTrader ke satu atau lebih akaun **slave** — lintas-broker, lintas-cID — dengan kawalan per-destinasi + rekonsiliasi gred wang.

## Konsep

- **Profil salinan** — satu master (`SourceAccountId`) + satu atau lebih **destinasi**. Kitaran hayat: `Draft → Running → Paused → Stopped` (`Error` semasa kegagalan). Akar agregat: `CopyProfile` (memiliki `CopyDestination`).
- **Destinasi** — satu akaun slave + set peraturan lengkap untuk cara master disalin ke atasnya. Semua konfigurasi per-destinasi, jadi satu master boleh memberi makan kepada slave konservatif + agresif pada masa yang sama.
- **Host enjin salinan** — pekerja berjalan untuk profil (`CopyEngineHost`). Melanggan aliran pelaksanaan master, menggunakan setiap peristiwa ke setiap destinasi.
- **Penyelia** — `CopyEngineSupervisor`, perkhidmatan latar belakang pada setiap nod. Profil yang ditugaskan tuan rumah, penyembuhan diri di seluruh kluster (lihat [penskalaan](../deployment/scaling.md)).

## Apa yang dicerminkan

| Peristiwa Master | Tindakan Slave |
|--------------|--------------|
| Buka kedudukan pasaran / jarak pasaran | Buka salinan bersaiz (berlabel dengan id kedudukan sumber) |
| Pesanan pending had / henti / had-henti | Tempat pesanan pending yang sepadan |
| Pinda pesanan pending | Pinda pesanan pending yang dicerminkan di tempat |
| Batalkan pesanan pending / luput | Batalkan pesanan pending yang dicerminkan |
| Tutup separa | Tutup bahagian yang sama dari kedudukan slave |
| Skala-masuk (kenaikan volum) | Buka volum yang ditambah (opsional) |
| Perubahan had-rugi / henti-surut | Pinda perlindungan kedudukan slave |
| Tutup penuh | Tutup salinan slave |

Setiap salinan **berlabel dengan id kedudukan/pesanan sumber**. Selepas sambung semula host membina semula keadaan daripada rekonsiliasi: membuka salinan yang dipegang master tetapi slave hilang, menutup "yatim piatu" slave yang tidak lagi dipegang master — **tanpa menggandakan dagangan**.

## Membuat profil

**Profil Baru** membuka borang **halaman penuh** berdedikasi (`/copy-trading/new`), bukan dialog — set pilihan cukup besar sehingga halaman lebih baik dibaca di telefon dan desktop. Ia mengumpulkan semuanya di awal: nama profil, sumber (master) akaun, destinasi (slave) akaun (multi-pilih dengan butang **Pilih semua**; master yang dipilih dikecualikan daripada senarai slave), + set opsyen per-destinasi penuh. **Hanya akaun yang dipaut melalui API Terbuka cTrader yang boleh dipilih** sebagai master atau destinasi — penyalinan meletakkan pesanan melalui API Terbuka, jadi akaun yang ditambah secara manual (cID sahaja) tidak boleh menyalin dan tidak disenaraikan; apabila tidak ada yang dipaut halaman menunjukkan notis yang menunjuk ke Akaun Perdagangan. Mod pembesaran, arah dan penapis simbol dipaparkan sebagai **label manusia** dengan **penjelasan senarai per-mod** pada petua bantuan pengurusan wang. **Setiap kawalan membawa petua bantuan** yang menjelaskan apa yang dilakukannya dan cara menggunakannya. Input berstruktur menggunakan **kawalan yang disahkan dengan baik** — nombor/peratusan melalui medan berangka, mod/arah/penapis melalui pilihan, penapis simbol melalui senarai cip simbol tambah/buang, dan peta simbol melalui jadual tambah/buang baris `Sumber → Destinasi (× pengganda)` — tidak pernah blob teks berseparasi koma. Semua input **disahkan sebelum menyimpan** — nama/sumber/destinasi hilang, parameter pembesaran bukan positif, had banyak negatif/tidak konsisten, peratusan lintasan keluar-julat, tiada jenis pesanan didayakan, atau penapis simbol kosong muncul sebagai senarai ralat + blok simpan. Pada penciptaan, profil dibuat + setiap slave yang dipilih ditambah dengan tetapan yang dipilih, kemudian halaman kembali ke senarai Perdagangan Salinan.

**Import / eksport.** Blok tetapan keseluruhan boleh **dieksport ke fail JSON** dan **diimport semula** untuk mengisi bentuk terlebih dahulu, jadi penalaan boleh digunakan semula merentasi profil tanpa penaipan semula. Peta simbol juga boleh **dieksport / diimport sebagai fail CSV** (`Source,Destination,VolumeMultiplier`) — sediakan peta simbol broker besar dalam hamparan dan muatkannya dalam satu langkah. Kawalan simbol dan import/eksport CSV yang sama juga tersedia dalam dialog destinasi di halaman Perdagangan Salinan.

Tindakan baris menghormati kitaran hayat: **Mulai** hanya didayakan apabila tidak berjalan, **Henti** + **Jeda** hanya apabila berjalan, **Padam** dilumpuhkan semasa berjalan + meminta pengesahan sebelum membuang profil + destinasi.

## Pilihan per-destinasi

Tetapkan pada halaman Profil Baru, dalam dialog destinasi di halaman Perdagangan Salinan, atau melalui `POST /api/copy/profiles/{id}/destinations`:

- **Pembesaran** (`MoneyManagementMode` + parameter): lot tetap, pengganda lot/notional, baki berkadar/ekuiti/margin bebas, risiko % tetap, leverage tetap, auto-berkadar, **risiko-%-daripada-henti** (M7). Tambah had banyak min/maks + henti-perkosaan banyak min. **Risiko-daripada-henti** mengurangi destinasi supaya ia berisiko peratusan terkonfigurasi daripada *keseimbangannya sendiri*, diperolehi daripada **jarak had-rugi master** (`risiko master 2% → risiko auto-slave 2%`): `lots = balance×% ÷ (stopDistance × contractSize)`. Master terbuka **tanpa** had-rugi tidak mempunyai jarak untuk mengurangi — menggunakan had **lot risiko maksimum** terkonfigurasi (M7) jika ditetapkan, sebaliknya dilangkau (`no_stop_loss`) tidak diteka. Ekuiti **berkadar**/**margin bebas** saiz dari **ekuiti** akaun sebenar (`baki + Σ terapung P&L`, diperolehi per cTrader Open API yang tidak memberikan ekuiti), bukan keseimbangan biasa — jadi master duduk di keuntungan/kerugian terbuka mengurangi salinan dengan betul. Margin yang digunakan tidak didedahkan oleh API rekonsiliasi, jadi margin bebas dianggap sebagai ekuiti (proksi dana tersedia yang jujur); mod lain membaca keseimbangan + melangkau pusingan penilaian semula tambahan.
- **Penapis arah**: kedua-duanya / hanya panjang / hanya pendek. **Terbalik**: sebelah flip (+ tukar SL↔TP) untuk salinan bertentangan.
- **Urus-sahaja** (Abaikan-Dagangan-Baru / Tutup-Sahaja): cerminkan tutupan, tutupan separa + perubahan perlindungan pada kedudukan yang sudah disalin, tetapi buka **tiada** kedudukan/pesanan pending baru (dilangkau `manage_only`). Gunakan untuk menutup destinasi tanpa memotong salinan sedia ada.
- **Sinkronisasi-Terbuka-pada-mulai** / **Sinkronisasi-Tertutup-pada-mulai** (lalai hidup): pada **pertama** resinkronisasi profil, sama ada untuk membuka salinan untuk kedudukan sedia ada master, + sama ada untuk menutup salinan master tutup semasa profil berhenti. Kedua-duanya hanya terpakai pada permulaan — sambung semula pertengahan larian sentiasa rekonsiliasi sepenuhnya supaya desync pulih tanpa mengira.
- **Peta simbol** + **penapis simbol** (senarai putih / senarai hitam). Setiap entri peta-simbol membawa pengganda volum opsional **per-simbol** (ganti per-simbol cMAM) saiz salinan penskalaan untuk simbol itu di atas pembesaran destinasi (1 = tiada perubahan). Peta keseluruhan import/eksport sebagai **CSV** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; lajur `Source,Destination,VolumeMultiplier`) — setiap baris disahkan melalui objek nilai domain, jadi fail yang salah bentuk tidak boleh menghasilkan peta tidak sah.
- **Tingkap waktu perdagangan** (C18) — tingkap UTC harian per-destinasi (`permulaan`/`akhir` minit sehari, akhir eksklusif; `permulaan == akhir` = sepanjang hari). Pembukaan baru di luar tingkap dilangkau (`trading_hours`); tingkap dengan `permulaan > akhir` membungkus lepas tengah malam (cth. 22:00–06:00). Kedudukan yang ada tetap diurus.
- **Penapis label sumber** (C18, bersamaan cTrader filter nombor-ajaib MT) — apabila ditetapkan, salin hanya dagangan master yang labelnya **tepat** (cth. dagangan satu bot, atau label manual sahaja); sebaliknya dilangkau (`source_label`). Kosong = salin semua. Dibawa pada `ExecutionEvent.SourceLabel` daripada label kedudukan/pesanan master `TradeData.Label`, dihormati pada resinkronisasi juga.
- **Perlindungan akaun** (ZuluGuard / Perlindungan Akaun Global) — tonton **ekuiti langsung** destinasi (`baki + Σ terapung P&L`, ditolak setiap `CopyDefaults.EquityGuardInterval`) terhadap lantai `StopEquity` dan/atau siling pilihan `TakeEquity`. Semasa pelanggaran, gunakan mod: **TutupSahaja** (henti salinan baru, terus urus yang ada), **Beku** (henti pembukaan), **JualKeluar** (tutup **setiap** salinan pada destinasi segera). Setelah dipecat, destinasi dikunci — tiada pembukaan baru sehingga hos restart — + amaran `CopyAccountProtectionTriggered` dinaikkan. `JualKeluar` memerlukan `StopEquity`; `TakeEquity` mesti duduk di atas `StopEquity`. **Caveat tiada-jaminan:** jual keluar menggunakan pelaksanaan pasaran — seperti setara setiap pesaing, tidak boleh menjamin harga isi dalam pasaran cepat/terbelah.
- **Butang panik Ratakan-Semua** (C8) — `POST /api/copy/profiles/{id}/flatten` segera menutup **setiap** kedudukan salinan pada setiap destinasi + mengunci terhadap pembukaan baru. Dihalakan lintas-proses: API menetapkan bendera, penyelia menyampaikan ke hos berjalan (menggunakan semula saluran putaran token), yang meratakannya di tempat; bendera dijelas jadi api tepat sekali (`CopyFlattenAll` amaran). Pengguna kemudian menjeda/menghenti profil.
- **Guard peraturan firma-sokongan** (C7) — penguatkuasaan pengguna penyalin firma-sokongan minta. Per destinasi, **had kehilangan harian** (kehilangan daripada ekuiti pembukaan hari) dan/atau **had lintasan-penurunan** (kehilangan daripada ekuiti puncak berjalan), kedua-duanya dalam mata wang deposit. Semasa pelanggaran destinasi **auto-diratakan** (setiap salinan ditutup) + **terkunci keluar** baki hari UTC (pembukaan baru dilangkau `prop_lockout`); amaran `CopyPropRuleBreached` api. Kunci keluar jelas apabila hari UTC berubah (asas segar/puncak diambil). Berkongsi poll ekuiti langsung sama seperti perlindungan akaun.
- **Jitter pelaksanaan** (C11, dilumpuhkan secara lalai) — kelewatan rawak `0..N` ms sebelum meletakkan setiap salinan, untuk mengkorelasikan cap waktu pesanan berhampiran sama di akaun **sendiri** pengguna. **Caveat pematuhan:** bantuan untuk firma-sokongan yang *benarkan* penyalinan — **bukan** alat untuk mengelak firma yang melarangnya; kekal dalam peraturan firma anda adalah tanggungjawab anda.
- **Kunci konfigurasi** (C9) — beku tetapan destinasi untuk tempoh (`POST …/destinations/{id}/lock` dengan minit). Semasa dikunci, destinasi tidak boleh dikeluarkan (agregat menolak dengan `CopyDestinationConfigLocked`) — penjaga yang disengajakan terhadap perubahan impulsif semasa penurunan. Kunci tamat secara automatik pada cap waktunya.
- **Konsistensi pra-amaran** (C10) — amaran (sekali per hari UTC) apabila **keuntungan harian** destinasi mencapai peratusan terkonfigurasi bagi ekuiti pembukaan hari (`CopyConsistencyThresholdApproaching`), jadi peraturan konsistensi firma-sokongan dihormati *sebelum* ia api. Sisi untung, bebas daripada kunci sisi rugi; dijalankan daripada asas hari yang sama seperti penjaga peraturan sokongan.
- **Penapis jenis pesanan** — pilih tepat jenis pesanan master mana untuk disalin: pasaran, jarak pasaran, had, henti, had-henti (`CopyOrderTypes` bendera; lalai semua). Selektiviti gaya cMAM.
- **Salin SL / Salin TP** — cerminkan had-rugi/keuntungan-ambil master, atau urus perlindungan secara bebas.
- **Salin henti surut**, **cerminkan tutupan separa**, **cerminkan skala-masuk** — masing-masing boleh diubah secara bebas.
- **Salin luput pending** (lalai hidup) — cerminkan cap waktu luput pesanan pending master Good-Till-Date.
- **Salin gelincir master** (lalai hidup) — untuk pesanan jarak pasaran + had-henti, letakkan pesanan slave dengan gelincir tepat master-dalam-poin (harga asas diambil daripada tempat langsung slave).
- **Penjaga**: peratusan penurunan maksimum, had kehilangan harian, lengah salinan maksimum, penapis gelincir (lewatkan salinan jika harga slave bergerak melampaui N pip daripada kemasukan master). **Lengah salinan maksimum** diukur terhadap cap waktu pelayan sebenar peristiwa master (`ExecutionEvent.ServerTimestamp`) melalui `TimeProvider` yang disuntik: isyarat lebih lama daripada lengah maksimum terkonfigurasi dilangkau, jadi salinan tua tidak pernah diletakkan lewat (sebelum ini lengah selalu sifar + penjaga mati).
- **Normalisasi ketepatan SL/TP** (M6) — harga had-rugi/keuntungan-ambil salinan dibundarkan ke **ketepatan digit** simbol destinasi sebelum pinda, jadi harga master pada ketepatan lebih halus (atau ketidakpadanan digit lintas-broker) tidak pernah api pelayan `INVALID_STOPLOSS_TAKEPROFIT`.
- **Pemutus litar penolakan / Penjaga Pengikut** (G8) — destinasi menolak pembukaan `CopyDefaults.RejectionBudget` berturut-turut **api**: tiada pembukaan baru untuk tetingkap penyejukan (`CopyDestinationTripped` amaran api), menghenti ribut penolakan daripada menimpa (firma-sokongan) akaun. Kedudukan yang ada masih diurus + ditutup semasa api; pemutus auto-set semula selepas penyejukan + salinan berjaya jelas pembilang.
- **Siling akal lot** (C14) — saiz salinan maksimum mutlak dan/atau cap pengganda-master. Salinan yang dikira melebihi had mutlak, atau melebihi pengganda `N×` lot master sendiri, **keras-diblok** (dipermukakan sebagai `lot_sanity` melangkau, dikira pada `cmind.copy.skipped`) tidak diletakkan — mempertahankan melawan kelas oversize bencana (lot master 0.23 bertukar kepada 3 lot pada setiap penerima melalui pengganda melarikan diri atau pepijat pembundaran). Kedua-dua dimensi lalai `0` (matikan).

## Kebolehpercayaan & kes tepi

Enjin dibina untuk realiti bahawa apa pun boleh gagal pada masa-masa:

- **Timeout korelasi isian pending-slave** (C13) — pending slave yang dicerminkan yang pending master hilang (bukan berehat mahupun baru diisi) dibatalkan selepas timeout korelasi, jadi salinan slave tidak boleh diisi tidak berkorelasi ke kedudukan tidak terurus (`CopyPendingTimedOut`). Resinkronisasi juga membersihkan yatim piatu pending-diisi berlabel id-pesanan.
- **Tutupan/ratakan yang kuat** (M8) — menutup yatim piatu pada resinkronisasi, atau meratakannya semasa pelanggaran penjaga, bertoleransi terhadap kedudukan broker sudah ditutup (`POSITION_NOT_FOUND`): setiap tutupan dijalankan secara bebas, jadi satu id basi tidak pernah menggugurkan resinkronisasi atau meninggalkan akaun un-ratakan yang lain.

- **Mulai dengan master sudah dalam dagangan** — pada mulai hos rekonsiliasi + membuka salinan untuk kedudukan yang ada master.
- **Penggal sambungan / desinkronisasi** — pada penyambung semula hos rekonsiliasi: membuka salinan hilang, menutup yatim piatu, melabel semula pendings. Tiada pesanan gandaan.
- **Kegagalan penempatan pesanan** — kegagalan pada satu destinasi dilogkan, tidak pernah menghalang destinasi lain.
- **Token tunggal sah per cID** — cTrader membatalkan token akses cID lama seketika token baru dikeluarkan. cMind menukar token hos berjalan **di tempat** (re-auth pada soket langsung) supaya penyalinan terus tanpa pemisahan aliran. Lihat [kitaran hayat token](token-lifecycle.md).

## Keauditan

Setiap tindakan memancarkan peristiwa log berstruktur yang dijana sumber (`LogMessages`) dengan id profil, cID destinasi, id pesanan/kedudukan, + nilai — pesanan diletakkan/dilangkau (dengan sebab), tutupan separa, perlindungan digunakan, surutan diterapkan, pending diletakkan/dipinda/dibatalkan, luput dicerminkan, gelincir jarak pasaran dicerminkan, token ditukar, ringkasan resinkronisasi. Ini adalah jejak audit untuk pematuhan + penyelesaian pertikaian.

Bersama-sama log, enjin memancarkan **metrik OpenTelemetry** pada meter `cMind.Copy` (didaftar dalam saluran OTel bersama, dieksport lebih OTLP / ke Azure Monitor seperti baki): `cmind.copy.latency` (peristiwa master → hantar, ms), `cmind.copy.dispatch.duration` (kipas keluar ke semua destinasi, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (ditandai mengikut destinasi), `cmind.copy.skipped` (ditandai mengikut sebab), + `cmind.copy.failed`. Ini menjadikan regresi latensi/gelincir boleh diukur, bukan hanya kelihatan dalam baris log — sut langsung mempertahankan mereka terhadap belanjawan.

## API

- `GET /api/copy/profiles` — senarai.
- `POST /api/copy/profiles` — buat (dengan id akaun destinasi pilihan).
- `GET /api/copy/profiles/{id}` — perincian penuh incl. setiap pilihan destinasi.
- `POST /api/copy/profiles/{id}/destinations` — tambah destinasi dengan set pilihan penuh.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — keluarkan.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — kitaran hayat.

## Ujian

- **Unit** (`tests/UnitTests/CopyTrading`) — mod pembesaran, penapis keputusan, penapis jenis pesanan, salinan luput, gelincir jarak pasaran/had-henti, togol SL/TP, tutupan separa, pinda pending/batal, mulai-dengan-terbuka, putus → desinkronisasi → resinkronisasi, tukar token di tempat, penolakan lintas-cID. Dijalankan terhadap `FakeTradingSession`, simulator dalam-memori setia cTrader.
- **Integrasi** (`tests/IntegrationTests/CopyLive`) — tuntutan keafinan nod/pajakan, penyebaran versi token pada Postgres sebenar.
- **E2E** (`tests/E2ETests`) — perjalanan bulatan pilihan destinasi melalui API + UI, kitaran hayat penuh.
- **Tegasan / DST** (`tests/StressTests`) — ujian simulasi-deterministik: beban kerja rawak berbenih + suntikan kesalahan (kipas soket, penolakan pesanan, penolakan jarak pasaran, putaran token, kematian nod) pandu `CopyEngineHost` ke ketenangan + pertikaian kekonvergenan pertikaian. Lihat [ujian/ujian-tegasan.md](../testing/stress-testing.md). Sut ini menyingkap + memperbaiki perlumbaan permulaan sebenar: `OnReconnected` terwayar sebelum beban rujukan awal + resinkronisasi, jadi kipas soket semasa permulaan boleh menjalankan resinkronisasi kedua serentak + mudaratkan kamus keadaan hos tidak serentak — beban permulaan + resinkronisasi pertama kini dijalankan di bawah `_stateGate`.
- **Hidup** — akaun demo cTrader sebenar; lihat [ujian/ujian-salinan-dagangan-hidup.md](../testing/live-copy-trading.md).

Lihat [dev-credentials.md](../testing/dev-credentials.md) untuk fail kelayakan tunggal tier hidup + E2E baca.
## Kawalan profil dan pengurusan destinasi

Mulai/henti adalah butang ikon pada setiap baris profil (dilumpuhkan apabila tindakan tidak terpakai). Akaun sumber dan destinasi ditunjukkan oleh **nombor akaun** mereka, tidak pernah id dalaman. Mengklik profil membuka **dialog** untuk menguruskan akaun destinasinya (tambah/buang dengan tetapan per-destinasi penuh).
