---
description: "Hantar satu aplikasi Open API cTrader untuk setiap pengguna (mod Kongsi white-label), URL Pengarah semula tunggal untuk mendaftarkan, dan selimit kadar setiap jenis pesan."
---

# Aplikasi Open API Kongsi & selimit kadar

Secara lalai setiap pengguna mendaftarkan **Aplikasi Open API cTrader mereka sendiri** di bawah
**Tetapan → Open API**. Operator white-label (lazimnya broker atau penjual semula cTrader) boleh sebaliknya
menghantar **satu aplikasi Open API kongsi untuk semua pengguna** — tiada siapa mendaftarkan aplikasi mereka sendiri; semua orang
memberi kuasa akaun mereka melalui aplikasi tunggal operator.

## Dua cara membekalkan aplikasi kongsi

Apl kongsi diperuntukkan sama ada dari konfigurasi penempatan **atau** dari UI tetapan pemilik
(nilai yang ditetapkan pemilik menang). Sediakannya sekali dan mod kongsi diaktifkan untuk semua orang.

### 1. Konfigurasi penempatan (dibibit pada permulaan)

```jsonc
"App": {
  "OpenApi": {
    "PublicBaseUrl": "https://cmind.yourbroker.com",   // URL awam sahaja penempatan ini
    "SharedApp": {
      "Enabled": true,
      "Name": "YourBroker Open API",
      "ClientId": "1234_abcd...",
      "ClientSecret": "…"                                // disulit pada rehat; tidak pernah dicatat
    }
  }
}
```

Pada permulaan apl membibit satu aplikasi kongsi yang dimiliki oleh akaun pemilik (idempoten — nó tidak pernah tulis ganti
nilai masa jalan yang diedit pemilik, dan pembibitan semula ialah no-op).

### 2. Tetapan pemilik (masa jalan, tanpa pengambilan semula)

**Tetapan → Open API** (pemilik sahaja) menunjukkan dua perkara: bahagian **Aplikasi Open API Anda Sendiri** — pemilik mendaftarkan, menyunting, dan membenarkan apl **mereka sendiri** setiap pengguna dengan tepat seperti mana-mana pengguna (tersedia manakala tiada apl kongsi dikonfigurasi) — dan kad **Aplikasi kongsi penempatan** untuk tambah / sunting / padam apl kongsi, dengan URL Pengarah semula dipaparkan untuk salin-lekat. Perubahan berkuatkuasa untuk pengesahan baharu secara serta-merta. Apabila apl kongsi dikonfigurasi ia menggantikan apl pemilik mereka sendiri, dan bahagian **Aplikasi Open API Anda Sendiri** bertukar kepada notis bahawa akaun kini membenarkan melalui apl kongsi.

## URL Pengarah semula (daftarkan ini dalam cTrader)

Setiap aplikasi Open API cTrader mendaftarkan **satu** URL Pengarah semula — **nilai Tunggal yang sama** untuk
apl kongsi dan untuk mana-mana apl setiap pengguna:

```
{your deployment URL}/openapi/callback
```

contohnya `https://cmind.yourbroker.com/openapi/callback`.

- Apl **memperlibat nilai tepat** pada halaman tetapan Open API (dengan butang salin) — tampalnya ke
  portal rakan cTrader apabila anda cipta aplikasi Open API.
- Ia compuesta dari `App:OpenApi:PublicBaseUrl` jadi nó kekal stabil di belakang proksi terbalik / CDN;
  apabila nó tidak ditetapkan nó fallback kepada hos permintaan masuk.
- Pengalaman jemput vs pengguna biasa berbeza hanya di mana pengguna mendarat **selepas** callback
  (senarai akaun mereka vs pengesahan "akaun ditambah") — URL Pengarah semula berdaftar tidak berubah.

## Apa yang dilihat pengguna di bawah mod kongsi

Apabila aplikasi kongsi wujud:

- Pengguna mendapat **tiada pilihan** untuk mendaftarkan aplikasi Open API mereka sendiri — halaman tetapan menunjukkan
  **"Open API diurus oleh pembekal anda"** dan butang **Benarkan akaun** yang menggunakan apl kongsi.
- Sebarang aplikasi peribadi sedia ada dialih keluar; akaun yang dibenarkan mereka dituding semula ke
  apl kongsi dan harus **di kebenaran semula** (token lama mereka dikeluarkan di bawah id klien berbeza). Percubaan
  untuk cipta apl peribadi mengembalikan ralat "diurus oleh pembekal anda".

## Selimit kadar klien (setiap jenis pesan)

Klien memacula pesan API Open API cTrader keluar supaya letupan tidak pernah mencetuskan
sekatan selimit kadar pelayan. Selimit **setiap jenis pesan**, sepadan dengan dokumen Open API cTrader:

| Kategori | Apa yang diliputi | Lalai |
|---|---|---|
| `General` | pesanan perdagangan + baca (pesanan, simbol, pertanyaan akaun) | 45 msg/s |
| `HistoricalData` | permintaan data trendbar / tick (dikecilkan lebih keras oleh cTrader) | 5 msg/s |

Permintaan data sejarah dikira terhadap ** kedua-dua** baldi nó sendiri dan baldi am. Mesej Heartbeat dan
pengesahan tidak pernah dimacula. Mesej gilir dan saliran pada kadar tersedia — tiada yang digugurkan dan pesanan dikekalkan.

Talanya jika broker anda merundingkan selimit cTrader yang **lebih tinggi**, atau tetapkan kategori kepada **`0`** untuk melumpuhkan
paculiran sepenuhnya (tanpa had):

- **Konfigurasi:** `App:OpenApi:RateLimits:General` / `App:OpenApi:RateLimits:HistoricalData` (msg/s).
- **Tetapan pemilik:** kad **Selimit kadar klien** pada **Tetapan → Open API** (tindihan pemilik menang,
  применяется к sambungan baharu / pada sambung semula).
