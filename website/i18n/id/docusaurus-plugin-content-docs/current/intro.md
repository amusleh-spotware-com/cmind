---
slug: /intro
title: Selamat datang di cMind
description: Pengantar ramah untuk cMind — platform operasi trading untuk cTrader yang open-source dan dapat dihosting sendiri.
sidebar_position: 1
---

# Selamat datang di cMind 👋

Jadi Anda ingin membangun bot trading, membacktest-nya tanpa melelehkan laptop, menjalankannya di
beberapa mesin, mencerminkan transaksi ke selusin akun, dan membiarkan AI mengawasi risiko saat Anda
tidur. **Anda berada di tempat yang tepat.**

cMind adalah **platform operasi trading untuk cTrader yang open-source dan dapat dihosting sendiri**.
Anggap saja sebagai seluruh meja trading Anda — penulisan, eksekusi, armada komputasi, copy trading, dan
inti AI — dikemas dalam satu aplikasi yang tenang, gelap, dan ramah seluler yang sepenuhnya Anda miliki.

:::tip Dalam satu kalimat
Bangun → backtest → jalankan → salin strategi cTrader Anda dalam skala besar, dengan AI bawaan, di
server Anda sendiri, di bawah merek Anda sendiri.
:::

## Apa yang sebenarnya bisa dilakukannya?

| Anda ingin… | cMind melakukannya | Baca selengkapnya |
|---|---|---|
| Menulis cBot di browser | Monaco IDE + templat C#/Python, build tersandbox | [Bangun & backtest](./features/build-and-backtest.md) |
| Membacktest lintas mesin | Armada node yang memulihkan diri memilih mesin yang paling senggang | [Penskalaan](./deployment/scaling.md) |
| Menyalin satu akun ke banyak | Pencerminan tangguh dengan resinkronisasi, tanpa transaksi ganda | [Copy trading](./features/copy-trading.md) |
| Membiarkan AI mengerjakan pekerjaan berat | Pembuatan strategi, perbaikan diri, penjaga risiko, post-mortem | [Inti AI](./features/ai.md) |
| Tetap dalam aturan prop firm | Pelacakan ekuitas langsung + simulasi aturan tantangan | [Prop firm](./features/prop-firm.md) |
| Merilisnya sebagai produk *Anda* | White-label penuh: nama, warna, logo, favicon | [White-label](./features/white-label.md) |
| Menjalankannya di ponsel Anda | PWA yang dapat dipasang dan mengutamakan seluler | [PWA](./features/pwa.md) |
| Mengendalikannya dari klien AI | Server MCP bawaan (HTTP + SSE) | [MCP](./features/mcp.md) |

## Jalur 5 menit ⏱️

Jika Anda punya Docker dan lima menit, Anda bisa mulai mengutak-atik instance cMind sungguhan sekarang juga:

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
cp .env.example .env        # set OWNER_EMAIL + OWNER_PASSWORD
docker compose up --build
```

Lalu buka **<http://localhost:8080>**, masuk, dan Anda siap. Panduan lengkap (dengan pemecahan masalah
untuk saat Docker pasti punya pendapat sendiri) ada di **[Menjalankan secara lokal](./deployment/local.md)**.

## Baru di sini? Ikuti jalan bata kuning 🟡

1. **[Untuk siapa ini?](./audience.md)** — pastikan Anda adalah jenis masalah kami.
2. **[Menjalankan secara lokal](./deployment/local.md)** — jalankan instance sungguhan.
3. **[Fitur](./features/README.md)** — tur lengkap tentang apa yang ada di dalamnya.
4. **[Deploy sungguhan](./deployment/cloud.md)** — Docker, Kubernetes, Azure, AWS.
5. **[Jadikan milik Anda](./white-label-for-business.md)** — terapkan white-label untuk bisnis Anda.
6. **[Berkontribusi](./contributing.md)** — PR (manusia *dan* dibantu AI) sangat diterima.

## Sepatah kata tentang uang 💸

cMind menggerakkan **modal sungguhan**. Kami menanggapinya dengan serius — setiap perubahan dikirim
dengan uji unit, integrasi, dan ujung-ke-ujung, termasuk jalur kegagalan (koneksi putus, order ditolak,
node mati). Anda pun harus menanggapinya dengan serius: **uji dulu di akun demo**, dan baca
[catatan kepatuhan](./features/compliance.md) sebelum mengarahkannya ke sesuatu yang nyata. Trading itu
berisiko; perangkat lunak ini adalah alat, bukan nasihat keuangan.

Baiklah — cukup basa-basinya. Ayo bangun sesuatu. →
