---
title: Architecture Decision Records
description: Keputusan desain yang tidak jelas di balik cMind — konteks, keputusan, dan konsekuensi — yang tidak dapat Anda baca dari kode.
---

# Architecture Decision Records

Record-record ini mendokumentasikan keputusan desain yang **tidak dapat Anda lihat langsung dari kode** — trade-off, jalur alternatif yang tidak diambil, dan alasannya. Setiap record singkat: *Konteks → Keputusan → Konsekuensi*. Keputusan struktural baru → tambahkan ADR di sini (nomor berikutnya) sehingga insinyur berikutnya (manusia atau AI) mewarisi alasan, bukan hanya hasilnya.

| # | Keputusan |
|---|---|
| [0001](./0001-strict-ddd-pure-core.md) | DDD ketat dengan `Core` murni |
| [0002](./0002-tph-instance-replaces-entity.md) | Status instance adalah TPH; transisi mengganti entity |
| [0003](./0003-external-nodes-http-jwt.md) | Node CLI cTrader adalah HTTP + JWT, tanpa SSH/shell |
| [0004](./0004-cbotbuilder-on-web-host.md) | `CBotBuilder` berjalan di web host dalam container sandbox |
| [0005](./0005-anthropic-raw-http.md) | Klien AI menggunakan HTTP mentah, bukan SDK Anthropic |
| [0006](./0006-copy-profile-db-lease.md) | Hosting copy dikoordinasikan oleh lease DB atomik |
