---
description: "Kesihatan Strategi & Pereputan Alpha — pengesanan pereputan deterministik yang membandingkan Sharpe baru-baru ini strateginya dengan rekod sebelumnya dan mencari anjakan min terbesar (CUSUM change-point), mengembalikan verdict Sihat / Merosot / Reput."
---

# Kesihatan Strategi & Pereputan Alpha

Setiap kelebihan merosot — penyelidikan nyata bahawa separuh hayat strategi kuant telah merosot dari tahun ke bulan, jadi *penyesuaian mengatasi penemuan*. Monitor Kesihatan Strategi memberitahu anda, dari sejarah pulangan strateginya sendiri, sama ada kelebihannya masih ada.

Buka **cBots → Strategy Health** (`/quant/health`).

## Apa yang dilakukannya

Diberikan siri pulangan (atau lengkung ekuiti, terlama dulu), ia:

- memecahkan sejarah kepada bahagian **lebih awal** dan **terkini** dan membandingkan nisbah Sharpe mereka;
- menjalankan imbasan **CUSUM change-point** untuk mencari pemerhatian di mana min paling jelas beralih (rehat rejim),
  dilaporkan hanya apabila sisihan itu ketara secara statistik;
- mengembalikan verdict:

| Verdict | Makna |
|---|---|
| **Sihat** | Prestasi terkini sepadan dengan (atau lebih baik daripada) rekod sebelumnya. |
| **Merosot** | Sharpe terkini lebih lemah daripada rekod sebelumnya — pantau dengan rapat. |
| **Reput** | Kelebihan itu telah berkesan hilang dalam jendela terkini — pertimbangkan untuk memberhentikan. |
| **Tidak Diketahui** | Tidak cukup sejarah untuk menilai. |

```http
POST /api/quant/health
{ "returns": [...] }   // atau { "equity": [...] }
```

## Mengapa ia boleh dipercayai

Ia ialah kod domain murni, deterministik (`Core.Health`) dengan tiada kebergantungan infrastruktur dan tiada
panggilan luaran — diuji unit untuk kes reput, merosot, sihat dan terlalu-pendek dan untuk lokalisasi change-point.
Ia adalah teman manual kepada semakan kesihatan yang sentiasa hidup yang menyokong ejen autonomic:
statistik yang sama memandu litar pemutus yang mengurangkan risiko strategi langsung yang kelebihannya semakin pudar.
