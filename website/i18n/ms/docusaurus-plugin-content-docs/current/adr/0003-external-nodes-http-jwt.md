---
title: 0003 — Nod cTrader CLI ialah HTTP + JWT, tiada SSH/shell
description: Mengapa ejen nod jauh mendedahkan hanya API HTTP dengan JWT berumur pendek dan tidak pernah shell.
---

# 0003 — Nod cTrader CLI ialah HTTP + JWT, tiada SSH/shell

## Konteks

Bekas backtest/larian melaksanakan pada hos jauh. Pendekatan jelas — SSH dalam dan jalankan docker — memberikan apl utama pelaksanaan kod jauh sewenang-wenang dan kredensial berumur panjang pada setiap nod. Itu adalah jejari letupan besar untuk sistem yang menjalankan cBot pengguna yang tidak dipercayai.

## Keputusan

Setiap hos jauh menjalankan **ejen HTTP** `CtraderCliNode` kendiri **tanpa SSH dan tanpa shell**. Apl utama memanggil ejen melalui HTTP; setiap permintaan membawa **JWT HS256** berumur pendek (5 minit, `iss=app-main` / `aud=app-node`) ditandatangani dengan rahsia nod itu. Ejen:

- hanya menjalankan imej yang sepadan `AllowedImagePrefix` (dengan sempadan laluan jadi `ghcr.io/spotware` tidak boleh padanan `ghcr.io/spotware-evil/...`);
- exec docker melalui `ArgumentList` — tidak pernah rentetan shell;
- adalah **stateless**, mencari bekas mengikut label `app.instance`;
- daftar diri dan jantung ke `POST /api/nodes/register`; apl utama upsert `CtraderCliNode` **mengikut nama**, jadi nod kekal perubahan IP.

## Akibat

- Token permintaan yang bocor tamat dalam beberapa minit; tiada kredensial shell yang berdiri untuk dicuri.
- Keupayaan ejen terikat kepada "menjalankan imej yang dibenarkan" — ia tidak boleh ditukar menjadi shell jauh umum.
- Identiti nod berasaskan nama, jadi menyediakan semula nod dengan IP baru tidak mengungkapkan sejarahnya.
