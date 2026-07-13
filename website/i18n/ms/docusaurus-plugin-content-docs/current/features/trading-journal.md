---
description: "Jurnal Perdagangan & Jurulatih — menganalisis lari dan backtest anda sendiri untuk kebocoran tingkah laku (lebihan tumpuan, kegagalan berulang, pincang rugi) dan menasihati anda pada strategi yang sudah anda ada. Deterministik, dengan naratif AI pilihan."
---

# Jurnal Perdagangan & Jurulatih

Kategori baharu AI-untuk-perdagangan yang benar-benar berguna bukan meramalkan pasaran — ia menganalisis
*opsyen sendiri*. Jurnal Perdagangan mengubah sejarah lari dan backtest anda kepada maklum balas jujur supaya
anda boleh memperbaiki strategi yang sudah anda ada.

Buka **AI → Trading Journal** (`/journal`).

## Apa yang dipaparkan

Dari contoh anda (lari dan backtest) ia mengira, secara deterministik:

- **Bilang menang / rugi / kegagalan dan kadar menang** merentasi backtest anda;
- **Pengetahuan tingkah laku** — kebocoran yang secara senyap membayakan pedagang runcit:
  - **Lebihan penumpuan** — kebanyakan aktiviti anda dalam satu simbol;
  - **Kegagalan berulang** — bahagian tinggi lari gagal bina atau konfigurasi;
  - **Pincang rugi** — lebih banyak backtest rugi daripada menang (dengan desak untuk menjalankan Makmal Integriti dan
    semak kelebihannya nyata);
  - kesihatan bersih apabila tiada di atas terpakai.

```http
GET /api/journal
```

## Mengapa ia boleh dipercayai

Analisis tingkah laku ialah kod domain murni, deterministik (`Core.Journal`) dengan tiada kebergantungan infrastruktur
— diuji unit untuk lebihan penumpuan, kegagalan berulang, pincang rugi, kesimbangan dan akaun kosong. Fakta datang dulu; jurulatih AI (Portfolio Digest) ialah lapisan naratif pilihan di atas,
digerbang pada kunci Anthropic API, jadi jurnal berfungsi sepenuhnya tanpa AI dikonfigurasi.
