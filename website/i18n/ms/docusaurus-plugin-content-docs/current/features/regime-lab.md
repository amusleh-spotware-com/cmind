---
description: "Makmal Rejim — melabelkan siri pulangan kepada rejim turun naik Tenang / Normal / Bergelora dan melaporkan prestasi setiap rejim, ditambah eksponen Hurst (trend-persetaraan vs min-kembalian). Deterministik."
---

# Makmal Rejim

Satu nisbah Sharpe tunggal menyembunyikan kebenaran bahawa kebanyakan kelebihan adalah bersyarat: bagus dalam tenang, pasaran trending
dan mati dalam turbulen (atau sebaliknya). Makmal Rejim memecahkan sejarah strategi kepada turun naik
rejim dan menunjukkan bagaimana prestasinya dalam setiap satu — jadi anda tahu *bila* kelebihan anda benar-benar berfungsi.

Buka **cBots → Regime Lab** (`/quant/regimes`).

## Apa yang dilakukannya

Diberikan siri pulangan (atau lengkung ekuiti, terlama dulu), ia:

- mengira **turun naik-realisasi mengikuti** pada setiap titik dan memecahkan sejarah kepada **Tenang**,
  **Normal** dan **Bergelora** rejim oleh tersil of turun naik itu;
- melaporkan **prestasi setiap rejim** — pemerhatian, pulangan purata, turun naik dan Sharpe — jadi anda boleh melihat
  di mana kelebihan itu hidup;
- menganggar **eksponen Hurst** melalui analisis skala semula (R/S): di atas ~0.55 siri adalah
  **trending / berlarutan**, di bawah ~0.45 ia adalah **min-kembali**, dan sekitar 0.5 ia hampir jalan rawak.

```http
POST /api/quant/regimes
{ "returns": [...], "window": 10 }   // atau { "equity": [...] }
```

## Mengapa ia boleh dipercayai

Kod domain murni, deterministik (`Core.Regimes`) dengan tiada kebergantungan infrastruktur dan tiada panggilan luaran
— diuji unit untuk pengasingan rejim (tenang vs Bergelora turun naik) dan untuk arah Hurst
(siri anti-berterusan mendapat markah di bawah 0.5, trend berterusan mendapat markah di atas). Isyarat rejim yang sama menyuap
gelung pantulan ejen autonomic, jadi ejen boleh bersandar ke rejim di mana kelebihannya nyata.
