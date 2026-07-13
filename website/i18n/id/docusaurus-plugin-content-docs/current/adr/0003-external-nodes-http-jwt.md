---
title: 0003 — Node CLI cTrader adalah HTTP + JWT, tanpa SSH/shell
description: Mengapa node agent jarak jauh hanya mengekspos API HTTP dengan JWT berumur pendek dan tidak pernah shell.
---

# 0003 — Node CLI cTrader adalah HTTP + JWT, tanpa SSH/shell

## Konteks

Container backtest/run berjalan di host jarak jauh. Pendekatan yang jelas — SSH dan jalankan docker — memberikan aplikasi utama eksekusi kode jarak jauh arbitrer dan kredensial berumur panjang di setiap node. Itu adalah radius ledakan besar untuk sistem yang menjalankan cBot pengguna yang tidak dipercaya.

## Keputusan

Setiap host jarak jauh menjalankan agent **HTTP** `CtraderCliNode` standalone dengan **tanpa SSH dan tanpa shell**. Aplikasi utama memanggil agent melalui HTTP; setiap permintaan membawa JWT **HS256** berumur pendek (5-menit, `iss=app-main` / `aud=app-node`) yang ditandatangani dengan rahasia node tersebut. Agent:

- hanya menjalankan image yang cocok dengan `AllowedImagePrefix` (dengan batas path sehingga `ghcr.io/spotware` tidak dapat cocok dengan `ghcr.io/spotware-evil/...`);
- exec docker melalui `ArgumentList` — tidak pernah string shell;
- adalah **stateless**, menemukan container menurut label `app.instance`;
- self-registers dan heartbeat ke `POST /api/nodes/register`; aplikasi utama upsert `CtraderCliNode` **berdasarkan nama**, sehingga node bertahan perubahan IP.

## Konsekuensi

- Token permintaan yang bocor kadaluarsa dalam hitungan menit; tidak ada kredensial shell berdiri untuk dicuri.
- Kemampuan agent dibatasi untuk "menjalankan image yang diizinkan" — itu tidak dapat diubah menjadi shell jarak jauh umum.
- Identitas node berbasis nama, jadi re-provisioning node dengan IP baru tidak mengorbankan historinya.
