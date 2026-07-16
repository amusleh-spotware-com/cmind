---
description: "Bangun, jalankan, backtest cBot cTrader (C# dan Python, keduanya .NET) dari Monaco IDE dalam browser, jalankan pada image ghcr.io/spotware/ctrader-console resmi."
---

# Bangun & backtest cBot

Bangun, jalankan, backtest cBot cTrader (C# **dan** Python, keduanya .NET) dari Monaco IDE dalam browser, jalankan pada image resmi `ghcr.io/spotware/ctrader-console`.

## Bangun

- Halaman **Builder** menghosting editor Monaco; `CBotBuilder` mengompilasi proyek dengan `dotnet build` **dalam container sekali pakai** (`AppOptions.BuildImage`, direktori kerja bind-mount di `/work`), sehingga target MSBuild pengguna tidak terpercaya tidak dapat menjangkau host. Restore NuGet di-cache lintas build melalui volume bersama. Host web memerlukan akses socket Docker.
- Template starter C# + Python berada di `src/Nodes/Builder/Templates/`.

## Jalankan & backtest

- **Instance** = hirarki status TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). Transisi mengganti entity (perubahan id), container id dibawa ke depan.
- `NodeScheduler` memilih node yang paling sedikit beban dan memenuhi syarat; `ContainerDispatcherFactory` merutekan ke agen HTTP node jarak jauh atau dispatcher Docker lokal.
- Poller penyelesaian merekonsiliasi container yang keluar (container backtest keluar sendiri melalui `--exit-on-stop`); laporan hadir → selesai (simpan `ReportJson`), hilang → gagal.
- Log container live mengalir ke browser melalui SignalR; kurva ekuitas backtest diparse dari laporan + dibuat grafik.

## Data pasar backtest di-cache per akun

cTrader Console mengunduh data tick/bar historis ke dalam `--data-dir`. Direktori itu adalah **cache stabil dan persisten yang di-kunci pada akun perdagangan** (nomor akunnya) — bind-mounted dari disk node di jalur containernya sendiri (`/mnt/data`), **mount terpisah, tidak bersarang** dari direktori kerja per-instance. Jadi setiap backtest pada akun yang sama **menggunakan kembali** data yang sudah diunduh alih-alih mengunduhnya lagi setiap kali jalankan. (Sebelumnya direktori data berada di bawah direktori kerja per-instance, yang id-nya berubah setiap kali jalankan, yang memaksa unduhan segar setiap backtest.) Direktori kerja ephemeral per-instance masih menyimpan algo, params, kata sandi dan laporan; cache data bersama dihitung dalam penggunaan data backtest node dan dihapus oleh tindakan pembersihan node.

## Pengaturan backtest

Dialog **Backtest** menampilkan pengaturan backtest cTrader Console yang dapat disesuaikan pengguna, sehingga Anda tidak perlu menyentuh baris perintah:

- **Simbol / Timeframe** — timeframe adalah **dropdown dari setiap period cTrader** (`t1`…`t1000`, `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1`, dan period Renko/Range/Heikin), dalam casing kanonik console, sehingga Anda selalu memilih `--period` yang valid.
- **Dari / Ke** — jendela backtest (`--start` / `--end`).
- **Mode data** — salah satu dari tiga mode cTrader (`--data-mode`): **Data tick** (`tick`, akurat), **bar m1** (`m1`, cepat), atau **Hanya harga buka** (`open`, tercepat).
- **Saldo awal** — default ke `10000` (`--balance`). **Saldo 0 tidak melakukan trade dan membuat cTrader mengeluarkan laporan kosong yang kemudian mengalami crash** ("Pesan diharapkan"), jadi saldo bukan nol selalu dikirim.
- **Komisi** — `--commission`.
- **Spread** — `--spread`, **field numerik dalam pips yang tidak dapat di bawah 0**. Ini **tersembunyi dalam mode data Tick**, di mana cTrader mendapatkan spread dari data tick itu sendiri (tidak ada `--spread` yang dikirim).

Direktori data (`--data-file` / `--data-dir`) dikelola oleh aplikasi itu sendiri (cache per-akun, lihat di atas), tidak ditampilkan dalam dialog.

:::note cTrader mengalami crash pada backtest kosong
Jika backtest menghasilkan **tidak ada hasil** — tidak ada trade, atau tidak ada data pasar untuk tanggal/simbol yang dipilih — penulis laporan cTrader Console mengeluarkan `Message expected` dan keluar tanpa laporan. Aplikasi tidak dapat memperbaiki bug upstream itu, tetapi mendeteksinya dan menandai instance **Gagal** dengan alasan yang dapat ditindaklanjuti ("tidak ada hasil backtest untuk rentang yang dipilih…") alih-alih stack trace mentah. Pilih rentang tanggal yang lebih luas yang memiliki data pasar yang tersedia dan coba lagi.
:::

## Halaman detail instance

Membuka instance (`/instance/{id}`) menunjukkan status live, log dan — untuk backtest — kurva ekuitas. **Judul tab browser** mencerminkan instance tertentu (**nama cBot · jenis · simbol**, mis. `TrendBot · Backtest · EURUSD`) jadi tab live-run dan tab backtest dapat dibedakan sekali pandang. Jalankan dan backtest dari cBot yang sama dilacak sebagai **lineages** yang berbeda (id lineage stabil dibawa lintas transisi state), sehingga halaman mengikuti instance yang tepat dan tidak pernah mencampur data run dengan data backtest.

## Kontrol siklus hidup instance

Setiap baris instance (dan halaman detailnya) memiliki kontrol yang benar state. Instance **aktif** menunjukkan **Stop**; instance **terminal** (Stopped / Completed / Failed) menunjukkan **Start (▶)** untuk meluncurkannya kembali dengan cBot, akun, simbol, timeframe, parameter set dan image yang sama (run dimulai ulang sebagai run, backtest sebagai backtest). Mengklik Stop menunjukkan pemberitahuan "Berhenti…" dan menonaktifkan ikon sampai resolusi, dan run yang baru dibuat muncul dalam daftar segera — tanpa reload halaman.

Log console **dipertahankan ketika instance berakhir** — untuk run (saat Stop) dan untuk **backtest** (saat selesai) — sehingga log run terakhir tetap dapat dilihat di halaman detail dan, melalui toolbar log, **disalin ke clipboard** (ikon Salin log) atau **diunduh** (ikon Unduh log) bahkan setelah container hilang. Keduanya bertindak pada log console lengkap instance, bukan hanya tail di layar.

`.algo` **yang diunggah** tidak pernah dibangun di sini, jadi kolom **Last Build**-nya di halaman cBot dibiarkan kosong (hanya menunjukkan waktu build untuk cBot yang Anda bangun di browser).

## Edit & jalankan ulang instance yang terhenti

Instance **terhenti** (run atau backtest) memiliki kontrol **Edit** — ikon di barisnya dalam daftar **dan** di samping Start/Stop di halaman detailnya — yang membuka dialog **diisi sebelumnya** dengan konfigurasinya saat ini. Anda dapat mengubah **akun perdagangan, simbol, timeframe, parameter set dan tag image** (dan, untuk backtest, **jendela dan semua pengaturan backtest** di atas), kemudian **Simpan & mulai** meluncurkannya kembali dengan pengaturan baru (menggantikan instance yang terhenti). Kontrol ini **dinonaktifkan saat instance aktif** — hanya instance yang terhenti yang dapat diedit.

## Jalankan dari editor kode

Mengklik **Run** dalam editor kode membuka dialog alih-alih menjalankan run buta yang dikodekan keras:

- **Akun perdagangan** (diperlukan) — akun cTrader yang terhubung dengan cBot.
- **Parameter set** (opsional) — pilih set yang ada, atau biarkan kosong untuk menjalankan dengan **nilai parameter default** cBot. Tombol **+** di samping selector membuat parameter set baru inline (lihat di bawah) dan memilihnya.
- **Simbol / Timeframe** default ke `EURUSD` / `h1` dan dapat diubah; **Batal** atau **Jalankan**.

Pada **Jalankan** editor menyimpan + membangun sumber saat ini, memulai instance pada akun yang dipilih dengan parameter yang dipilih, kemudian membaca log container live. (Aliran log meneruskan cookie auth pengguna yang masuk ke hub SignalR `/hubs/logs`, sehingga terhubung alih-alih gagal dengan `Invalid negotiation response received`.)

## Parameter set

**Parameter set** adalah set parameter override cBot yang dinamakan, dapat digunakan kembali disimpan sebagai objek JSON flat memetakan setiap nama parameter ke nilai skalar, mis. `{"Period": 14, "Label": "trend"}`. Saat runtime/backtest diubah menjadi file cTrader `params.cbotset` (`{ "Parameters": { … } }`). Anda dapat membuat/mengedit set sebagai JSON mentah dari dialog **Parameter sets** cBot atau inline dari dialog Run.

Setiap parameter set **milik cBot**: dialog Parameter Set Baru mencantumkan semua cBot Anda dan Anda **harus memilih satu** — pembuatan diblokir sampai cBot dipilih. **Nama** set adalah **unik per cBot**: membuat atau mengganti nama set ke nama yang sudah digunakan oleh set lain dari cBot yang sama ditolak (error yang jelas dalam dialog, `409 Conflict` di API). Nama yang sama dapat digunakan kembali pada **cBot yang berbeda**.

JSON **divalidasi** saat simpan: harus berupa objek flat tunggal yang nilainya semuanya skalar (string / number / bool). Root non-objek, array, objek bersarang, nilai `null`, atau JSON yang salah format ditolak (error yang jelas dalam dialog, `400 Bad Request` di API). Objek kosong `{}` diizinkan dan berarti "tanpa override".

## Catatan CLI cTrader Console

Backtest perlu `--data-mode` (default `m1`), tanggal sebagai `dd/MM/yyyy HH:mm`, dan argumen posisional JSON `params.cbotset`; `run` menolak `--data-dir` (backtest-only). Lihat `ContainerCommandHelpers`.

## Node & scale

Kapasitas eksekusi scale dengan menambahkan agen node (self-register + heartbeat). Lihat [node discovery](../operations/node-discovery.md) dan [scaling](../deployment/scaling.md).

## Akun perdagangan diperlukan

Menjalankan atau melakukan backtest cBot memerlukan akun perdagangan cTrader untuk terhubung. Sampai Anda menambahkan satu di bawah **Akun perdagangan**, tombol **Jalankan cBot Baru** / **Backtest cBot Baru** dinonaktifkan (dengan tooltip) dan halaman menunjukkan prompt yang menghubungkan ke penyiapan akun — Anda tidak lagi mengalami error `stream connect failed` dari bot tanpa akun.
