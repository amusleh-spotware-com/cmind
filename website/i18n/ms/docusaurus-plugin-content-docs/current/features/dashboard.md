---
title: Dashboard
description: Papan pemuka cMind — pusat arahan mudah alih-pertama untuk lari cBot, backtest, sumber, dan kluster nod anda.
---

# Dashboard

Perkara pertama yang anda lihat apabila anda daftar masuk, dan secara jujur halaman yang akan anda biarkan terbuka sepanjang hari. Halaman pendaratan (`/`, `Components/Pages/Index.razor`) ialah **pusat perintah mudah alih-pertama** untuk
aktiviti pengguna yang daftar masuk merentasi lari cBot, backtest, sumber dan (untuk admin) kluster nod. Ia menyegarkan dirinya sendiri, kelihatan bagus pada telefon, dan tidak pernah membuat anda tekan F5.

## Apa yang ditunjukkannya

Atas ke bawah, diperintahkan mengikut keutamaan untuk telefon (setiap blok ialah item stack lebar penuh pada mudah alih, grid responsif pada tablet/desktop):

1. **Pengepala** — tajuk, penunjuk langsung (titik berdenyum sebenar; statik di bawah `prefers-reduced-motion`), masa kemas kini terakhir, dan **tukeran tempoh** (`1H · 24H · 7D · 30D`) yang memacu KPI dan carta.
2. **KPI hero** — empat kad pandangan, setiap nombor besar + sparkline SVG sebaris, dan (di mana bermakna) **delta berbanding tempoh sebelumnya**:
   - **Aktif sekarang** — lari + backtest yang sedang bermula/berlari.
   - **Kadar kejayaan** — lengkap ÷ (lengkap + gagal) dalam tempoh; delta dalam mata peratus.
   - **Dilengkapkan** — lari/backtest selesai tempoh ini; delta berbanding tempoh sebelumnya.
   - **Gagal** — kegagalan tempoh ini; delta (lebih sedikit lebih baik, jadi penurunan menunjukkan hijau).
3. **Carta aktiviti** — garis masa kawasan ApexCharts bermula / dilengkapkan / gagal setiap tempoh.
4. **Cincin status contoh** — donat lari / backtest / tunggu / dilengkapkan / gagal, jumlah di tengah.
5. **Backtest** — tiga jubin snapshot (berlari / dilengkapkan / gagal), klik-melalui ke `/backtest`.
6. **Salinan perdagangan** — profil salinan perdagangan anda dengan titik status langsung, bilangan destinasi, dan les **Live**
   pada profil yang berjalan; klik-melalui ke `/copy-trading`.
7. **Ejen AI** — ejen perdagangan bermotivasi persona anda dengan status lari (archetype · status) dan masa tindakan terakhir; klik-melalui ke `/agent-studio`.
8. **Suapan aktiviti langsung** — 20 acara terkini paling baharu (terbaharu dulu) dengan titik berwarna status dan cap masa relatif.
9. **Kesihatan kluster** (admin sahaja) — nod aktif-v-total dan gauge kapasiti-digunakan.
10. **Jubin sumber** — cBot, akaun perdagangan, ID cTrader, kunci MCP (klik melalui ke halaman masing-masing).

## Suaikan papan pemuka anda

Setiap blok di atas ialah **widget yang anda kawal**. Tekan **Suaikan** (atas kanan pengepala) untuk membuka
dialog di mana anda **paparkan/sembunyikan** mana-mana widget dan **susunkannya** dengan anak panah atas/bawah. **Tetapkan semula ke lalai**
memulihkan urutan katalog. Pilihan anda **dikekalkan pelayan-side setiap pengguna**, jadi nó mengikut anda
merentasi pelayar dan peranti — bukan hanya tab ini.

- Widget yang digerbang ciri dan admin-sahaja (Salinan perdagangan, Ejen AI, Kesihatan kluster) hanya muncul dalam
  dialog apabila penempatan/peranan anda boleh menggunakan nó.
- Katalog widget ialah satu sumber kebenaran dalam `Core/Dashboard/DashboardWidgets.cs`; pembentangan
  (label + ikon + kesediaan) tinggal dalam `Components/Dashboard/DashboardWidgetMeta.cs`.

## Cara ia kekal langsung

Halaman mengundi `GET /api/dashboard/overview?period=<1h|24h|7d|30d>` setiap 10 saat dan memproses semula
widget di tempat — tiada muat semula manual. Kegagalan pengambilan sementara ditelan dan dicuba semula pada tik seterusnya;
gelung berhenti dengan kemas pada buang. Muat pertama menunjukkan skeleton; kegagalan berpanjangan menunjukkan kad ralat dengan **Cuba lagi**; pengguna dengan tiada data melihat KPI sifar dan salin keadaan kosong.

## Backend

- `Endpoints/DashboardEndpoints.cs` memetakan `/overview` (dan menyimpan skalar lama `/stats`). Ia
  setiap-pengguna dan digerbang admin melalui `ICurrentUser`; jam berasal dari `TimeProvider`. Ia juga memetakan
  `GET/PUT /api/dashboard/layout` — reka letak widget pengguna, dimuat pada permulaan halaman dan disimpan dari dialog Suaikan.
- **Kekekalan reka letak** ialah agregat `UserDashboard` (`Core/Dashboard/UserDashboard.cs`): satu papan
  setiap pengguna (unik pada `UserId`), memiliki senarai teratur tetapan widget (visible + order) disimpan sebagai lajur `jsonb`. Senarai teratur hanya pernah bermutasi melalui `Apply` / `Reset`, yang mengesahkan setiap kunci terhadap
  katalog `DashboardWidgets` dan menyimpan koleksi lengkap dan tanpa pendua. Kunci tidak diketahui ditolak dengan `DomainException` → `400`.
- `Endpoints/DashboardQuery.cs` membina model baca `DashboardOverview` komposit: gambaran status keseluruhan-masa (kiraan berkumpulan), set contoh material jendela, dan kiraan sumber/nod.
  Status contoh dan cap masa terminal tinggal pada subtipe TPH (bukan lajur), jadi baris dibaca dalam memori
  melalui pembantu kongsi `InstanceEndpoints.GetStartedAt/GetStoppedAt`. Masa acara =
  `stopped ?? started ?? created`.
- `Endpoints/DashboardModels.cs` memegang DTO, plan tempoh→(jendela, bilangan baldi), dan
  `DashboardMath` — pemprosesan murni, deterministik bucketing + KPI/delta math (tiada I/O, `now` dimasukkan).

Delta KPI membandingkan jendela semasa terhadap yang sebelumnya secara langsung (pertanyaan mengambil jendela berganda untuk ini). Tiada **suapan P&L akaun langsung** — platform hanya mempunyai ekuiti untuk backtest dan jejak prop-firm — jadi papan pemuka secara sengaja *operasional* (aktiviti, throughout, kadar kejayaan),
bukan ticker baki broker.

## Reka & token

Semua warna berasal dari token reka (`var(--app-success|-warning|-error|-info|-primary|-text*)`), jadi
palet white-label mengalir secara percuma — termasuk carta, yang warna siri nó dibaca dari token yang diselesaikan
 pada masa jalan melalui `window.appReadTokens` (SVG tidak boleh menggunakan pembolehubah CSS secara langsung). Tiada
hex keraskod di mana-mana dalam papan pemuka. Lihat [../ui-guidelines.md](../ui-guidelines.md).

## Pautan "Powered by cMind"

Papan pemuka mempamerkan pautan kecil, elok **"Powered by cMind"** yang menunjuk ke tapak dokumentasi ini. nó **dipapar secara lalai** — kami berbangga dengan projek dan nó membantu pedagang lain mencarinya — tetapi nó sepenuhnya terserah anda. Penjual semula yang mengendalikan instance white-label sepenuhnya flip
`App:Branding:ShowSiteLink` kepada `false` dan nó hilang. Lihat
[White-label branding](./white-label.md#powered-by-link).

## Ujian

- **Unit-style** (`tests/IntegrationTests/DashboardMathTests.cs`) — bucketing, kadar kejayaan,
  delta tempoh sebelumnya, penghuraian tempoh, kosong/sempadan (peristiwa pada `now`, penjaga bahagi-sifar).
- **Unit** (`tests/UnitTests/Dashboard/UserDashboardTests.cs`) — agregat `UserDashboard`: seed lalai, pakai order/visibility, append-diabaikan, collapse-duplicate, penolakan kunci tidak diketahui, set semula.
- **Integrasi** (`tests/IntegrationTests/DashboardQueryTests.cs`, `DashboardLayoutTests.cs`) — model baca terhadap Postgres sebenar (status/KPI/aktiviti/sumber, kesihatan nod admin, lalai-pengguna kosong), bahagian backtest/ profil salinan/ejen baharu, dan satu **pusingan-taip** reka letak (simpan reka letak tersuai → muat semula → order + visibility bertekak).
- **E2E** (`tests/E2ETests/DashboardTests.cs`, `DashboardCustomizeTests.cs`) — desktop + mudah alih: kad KPI, carta, cincin dan suapan dipapar; tukeran tempoh bertukar tempoh aktif dan muat semula; KPIGerudi melalui ke `/run`; **menyembunyikan widget bertekak merentasi muat semula**, **Tetapkan semula** membawanya kembali, dan dialog Suaikan berfungsi pada telefon dengan tiada limpahan mendatar. `/` juga dalam `PageSmokeTests`,
  `MobileLayoutTests` (shell + tiada limpahan) dan `MobileJourneyTests`.
