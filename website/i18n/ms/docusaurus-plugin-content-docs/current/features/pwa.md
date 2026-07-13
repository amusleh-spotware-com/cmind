---
description: "cMind memasang ke telefon atau desktop seperti apl asli — ikon skrin rumah, tetingkap kendiri, percikan, dan halaman luar sambungan mesra. Ia adalah mudah alih-pertama dan…"
---

# Apl yang boleh dipasang (PWA)

cMind memasang ke telefon atau desktop seperti apl asli — ikon skrin rumah, tetingkap kendiri, percikan,
dan halaman luar sambungan mesra. Ia adalah **mudah alih-pertama** dan responsif sepenuhnya; lihat
[ui-guidelines.md](../ui-guidelines.md).

## Apa yang "boleh dipasang" bermaksud di sini — dan had yang jujur

Blazor **Pelayan** memberikan melalui litar SignalR hidup, jadi apl tidak boleh berjalan luar sambungan sepenuhnya. Apa PWA hantar:

- **Boleh dipasang** — manifesto web sah + ikon, jadi pelayar menawarkan *Pasang* / *Tambah ke Skrin Rumah*.
- **Cangkang apl disimpan** — pekerja perkhidmatan menyimpan aset statik (CSS, ikon, manifesto) dan menunjukkan
  **halaman luar sambungan** apabila rangkaian jatuh, bukannya ralat pelayar.
- **Rasa asli** — paparan kendiri, warna tema berjenama/bar status, ikon apl, ikon skrin rumah iOS.

Ia **bukan** memberikan interaktiviti luar sambungan — itu akan memerlukan Blazor WebAssembly (trek masa depan yang terpisah). Jangan janjikan penggunaan luar sambungan ciri hidup.

## Potongan-potongan

| Potongan | Di mana |
|-------|-------|
| Manifesto (dinamik, berjenama) | `Web/Endpoints/PwaEndpoints.cs` → `GET /manifest.webmanifest` (tanpa nama) |
| Ikon (192, 512, 512-maskable, apple-touch-180) | `Web/wwwroot/icons/` |
| Pekerja perkhidmatan (cangkang apl) | `Web/wwwroot/service-worker.js` |
| Halaman jatuh luar sambungan | `Web/wwwroot/offline.html` |
| Pendaftaran + tag iOS + tangkapan prompt pasang | `Web/Components/App.razor` |
| Pemalar laluan | `Core.Constants.PwaRoutes` |

### Manifesto

Disajikan secara dinamik daripada `BrandingOptions` jadi nama produk, warna dan ikon penjual semula membawa ke dalam apl yang dipasang: `nama`/`nama_pendek` daripada `ProductName`, `perihalan`, `warna_tema` daripada `AppBarColor`, `latar_belakang_warna` daripada `BackgroundColor`, `paparan: kendiri`, dan set ikon (termasuk **maskable** 512 untuk ikon Android yang bersih). Tanpa nama — prompt pasang mestilah berfungsi sebelum menyusun masuk.

### Pekerja perkhidmatan

Cangkang apl sahaja. Ia **tidak pernah** merampas litar Blazor (`/_blazor`), kerangka (`/_framework`), atau hub SignalR (`/hubs`) — mereka sentiasa rangkaian. Navigasi adalah rangkaian-pertama dengan halaman luar sambungan sebagai jatuh balik; aset statik (`/css`, `/ikon`, `/_content`) adalah cache-pertama dengan pengesahan semula latar belakang. Didaftar dengan `updateViaCache: 'none'` jadi kemas kini pekerja digunakan dengan boleh dipercayai. Simpan disenaraikan (`cmind-shell-v<n>`) — bumpmva pada perubahan cangkul.

### iOS

iOS mengabaikan ikon/percikan manifesto, jadi `App.razor` juga memancarkan meta tag `apple-touch-icon` dan `apple-mobile-web-app-*`. iOS tidak ada `beforeinstallprompt`; pengguna memasang melalui *Tambah ke Skrin Rumah* Safari. `beforeinstallprompt` ditangkap ke `window.deferredInstallPrompt` pada Chromium/Android untuk affordansi pasang khusus.

## Ujian-ujian

- **E2E** — `E2ETests/PwaTests.cs`: manifesto disajikan dengan `application/manifest+json`, ikon bukan kosong termasuk satu maskable, `paparan: kendiri`, `apple-touch-icon` terpaut, dan pekerja perkhidmatan mendaftar + mengaktifkan. `MobileLayoutTests` / `MobileDialogTests` menutup cangkul mudah alih PWA memasang.
