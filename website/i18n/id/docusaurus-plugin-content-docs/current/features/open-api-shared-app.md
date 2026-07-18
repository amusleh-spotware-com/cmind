---
description: "Ship one cTrader Open API application for every user (white-label shared mode), the single redirect URL to register, and per-message-type client rate limits."
---

# Shared Open API application & rate limits

Secara default setiap user mendaftarkan **aplikasi Open API mereka sendiri** di
**Settings → Open API**. Operator white-label (biasanya broker cTrader atau reseller) dapat sebaliknya
mengirim **satu aplikasi Open API bersama untuk semua user** — tidak ada yang mendaftarkan aplikasi mereka sendiri; semua orang mengotorisasi akun mereka melalui aplikasi tunggal operator.

## Two ways to provide the shared application

Aplikasi bersama disediakan baik dari konfigurasi deployment **atau** dari UI pengaturan pemilik
(nilai yang ditetapkan pemilik menang). Sediakan sekali dan mode bersama aktif untuk semua orang.

### 1. Deployment config (seeded on startup)

```jsonc
"App": {
  "OpenApi": {
    "PublicBaseUrl": "https://cmind.yourbroker.com",   // canonical public URL of THIS deployment
    "SharedApp": {
      "Enabled": true,
      "Name": "YourBroker Open API",
      "ClientId": "1234_abcd...",
      "ClientSecret": "…"                                // encrypted at rest; never logged
    }
  }
}
```

Saat startup aplikasi seed satu aplikasi bersama yang dimiliki oleh akun pemilik (idempotent — tidak pernah
menimpa nilai yang diedit pemilik saat runtime, dan re-seeding adalah no-op).

### 2. Owner settings (runtime, no redeploy)

**Settings → Open API** (owner only) menampilkan dua hal: bagian **Your Open API application** — pemilik mendaftarkan, mengedit, dan mengotorisasi aplikasi per-user mereka **sendiri** persis seperti user lain (tersedia saat tidak ada aplikasi bersama yang dikonfigurasi) — dan kartu **Deployment shared application** untuk menambah / mengedit / menghapus aplikasi bersama, dengan URL redirect ditampilkan untuk copy-paste. Perubahan berlaku untuk otorisasi baru segera. Setelah aplikasi bersama dikonfigurasi, aplikasi bersama menggantikan aplikasi pemilik mereka, dan bagian **Your Open API application** beralih ke pemberitahuan bahwa akun sekarang mengotorisasi melalui aplikasi bersama.

## The redirect URL (register this in cTrader)

Setiap aplikasi Open API cTrader mendaftarkan **satu** URL redirect — nilai **yang sama** untuk
aplikasi bersama dan untuk aplikasi per-user:

```
{your deployment URL}/openapi/callback
```

misalnya `https://cmind.yourbroker.com/openapi/callback`.

- Aplikasi **menampilkan nilai yang tepat** di halaman pengaturan Open API (dengan tombol copy) — tempel ke
  portal mitra cTrader saat Anda membuat aplikasi Open API.
- Ini disusun dari `App:OpenApi:PublicBaseUrl` sehingga tetap stabil di belakang reverse proxy / CDN;
  saat tidak disetel, kembali ke host permintaan inbound.
- Pengalaman undangan vs normal-user berbeda hanya di mana user mendarat **setelah** callback
  (daftar akun mereka vs konfirmasi "akun ditambahkan") — URL redirect yang terdaftar tidak berubah.

## What users see under shared mode

Ketika aplikasi bersama ada:

- User **tidak mendapat opsi** untuk mendaftarkan aplikasi Open API mereka sendiri — halaman pengaturan menampilkan
  **"Open API dikelola oleh penyedia Anda"** dan tombol **Authorize accounts** yang menggunakan aplikasi bersama.
- Aplikasi pribadi yang sudah ada **dihapus**; akun otorisasi mereka dialihkan ke
  aplikasi bersama dan harus **diotorisasi ulang** (token lama mereka dikeluarkan di bawah
  id klien yang berbeda). Mencoba membuat aplikasi pribadi mengembalikan error "dikelola oleh penyedia Anda".

## Client rate limits (per message type)

Klien melaju pesan Open API cTrader keluar sehingga ledakan tidak pernah memicu pemblokan batas laju sisi server. Batas adalah **per tipe pesan**, cocok dengan dokumen Open API cTrader:

| Category | What it covers | Default |
|---|---|---|
| `General` | trading + read messages (orders, symbols, account queries) | 45 msg/s |
| `HistoricalData` | trendbar / tick-data requests (throttled harder by cTrader) | 5 msg/s |

Permintaan data historis dihitung terhadap **kedua** bucket-nya sendiri dan bucket umum. Pesan detak jantung dan
autentikasi tidak pernah dipacing. Pesan antri dan mengalir pada tingkat yang tersedia — tidak ada yang
dijatuhkan dan urutan dipertahankan.

Sesuaikan jika broker Anda bernegosiasi **lebih tinggi** batas cTrader, atau tetapkan kategori ke **`0`** untuk menonaktifkan
pacing sepenuhnya (unlimited):

- **Config:** `App:OpenApi:RateLimits:General` / `App:OpenApi:RateLimits:HistoricalData` (msgs/sec).
- **Owner settings:** kartu **Client rate limits** di **Settings → Open API** (override pemilik menang,
  berlaku untuk koneksi baru / saat reconnect).
