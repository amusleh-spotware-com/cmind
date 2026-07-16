---
description: "Strategy Health & Alpha Decay — pengesanan pereputan yang deterministik yang membandingkan Sharpe terbaru strategi dengan rekod awalnya dan mencari peralihan min terbesar (titik perubahan CUSUM), mengembalikan vonis Healthy / Degrading / Decayed."
---

# Strategy Health & Alpha Decay

Setiap kelebihan merepot — penyelidikan adalah terang bahawa separuh hayat strategi kuant telah runtuh dari tahun
ke bulan, jadi *adaptasi mengalahkan penemuan*. Pemantau Strategy Health memberitahu anda, daripada sejarah pulangan strategi sendiri, sama ada kelebihan masih ada.

Buka **cBots → Strategy Health** (`/quant/health`).

## Apa yang dilakukannya

Diberikan siri pulangan (atau kurva ekuiti, tertua dahulu), ia:

- membahagi sejarah kepada separuh **awal** dan **terbaru** dan membandingkan nisbah Sharpe mereka;
- menjalankan imbasan **titik perubahan CUSUM** untuk mencari pemerhatian di mana min paling jelas berubah (pemecahan rejim), dilaporkan hanya apabila sisihan ketara secara statistik;
- mengembalikan vonis:

| Vonis | Maksud |
|---|---|
| **Healthy** | Prestasi terbaru selaras dengan (atau lebih baik daripada) rekod awal. |
| **Degrading** | Sharpe terbaru jauh lebih lemah daripada rekod awal — pantau dengan rapi. |
| **Decayed** | Kelebihan telah hilang secara berkesan dalam tetingkap terbaru — pertimbangkan untuk berhenti. |
| **Unknown** | Tidak cukup sejarah untuk menilai. |

- **Terus daripada larian ujian — tanpa salin-tampal.** Setiap ujian yang lengkap mendedahkan jantung ikon **Check strategy health** pada baris senarai **Backtest** dan pada paparan perincian sampelnya; satu klik menjalankan pemantau pada kurva ekuiti tersimpan larian itu dan menunjukkan vonis dalam dialog. Ikon dilumpuhkan sehingga ujian selesai dan menghasilkan laporan, jadi ia tidak pernah menjadi kawalan mati. Di sebalik tabir ini adalah `POST /api/quant/health/backtest/{instanceId}`, yang membaca kurva ekuiti laporan tersimpan.

```http
POST /api/quant/health
{ "returns": [...] }   // or { "equity": [...] }
```

## Mengapa ia boleh dipercayai

Ia adalah kod domain tulen, deterministik (`Core.Health`) tanpa pergantungan infrastruktur dan tiada panggilan luaran — diuji unit untuk kes yang merepot, merosot, sihat dan terlalu singkat serta untuk lokalisasi titik perubahan. Ia adalah rakan manual kepada pemeriksaan kesihatan yang sentiasa hidup yang menyokong ejen otonomi:
statistik yang sama mendorong pemutus litar yang mengurangkan risiko strategi langsung yang kelebihannya semakin pudar.
