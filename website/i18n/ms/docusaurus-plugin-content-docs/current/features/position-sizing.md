---
description: "Saiz kedudukan institusi untuk runcit — sasaran turun naik dan pendedahan Kelly pecahan untuk strategi tunggal, ditambah perkongsian risiko songsang-turun naik dengan matriks korelasi merentasi buku strategi."
---

# Saiz Kedudukan & Portfolio

"Berapakah saiz dagangan ini harus?" ialah soalan yang memutuskan sama ada kelebihan meningkat atau meletup.
Institusi menjawabnya dengan **sasaran turun naik** dan **kriteria Kelly**, dan mereka membina buku dengan
**keseimbangan risiko** berbanding dolar yang sama. cMind membawa kedua-duanya ke runcit — matematik deterministik pada siri pulangan strategi, dengan cadangan teks biasa.

Buka **cBots → Position Sizing** (`/quant/sizing`).

## Saiz strategi tunggal

Diberikan pulangan strategi (atau lengkung ekuiti), turun naik tahunan sasaran, pecahan Kelly dan
had leverage, pengukur saiz melaporkan:

- **Turun naik tahunan direalisasi** — turun naik strategi sendiri, tahunan dengan peraturan punca-masa.
- **Saiz sasaran turun naik** — pendedahan yang menjadikan turun naik direalisasi memenuhi sasaran anda
  (`sasaran ÷ turun naik direalisasi`), dibatasi pada had leverage anda. Strategi turun-naik rendah mendapat saiz lebih besar.
- **Kelly penuh** — pecahan `f* = μ / σ²` (min atas varians pulangan) yang mengoptimumkan pertumbuhan.
- **Kelly pecahan** — `f*` diskala oleh pecahan Kelly anda. Kelly separuh (0.5) adalah pilihan lazim yang selamat;
  Kelly penuh terkenal terlalu agresif untuk kelebihan sebenar yang tidak menentu.
- **Pendedahan disyorkan** — **lebih kecil** (lebih selamat) antara saiz sasaran turun naik dan Kelly pecahan,
  dibatasi. Strategi tanpa kelebihan positif (Kelly penuh ≤ 0) disaiz kepada **sifar**.

```http
POST /api/quant/sizing
{ "returns": [...], "targetVolatility": 0.10, "kellyFraction": 0.5, "leverageCap": 3 }
```

## Peruntukan portfolio

Beri nó dua atau lebih strategi (seri pulangan sebaris) dan nó membina buku dengan **keseimbangan risiko songsang-turun naik**
— setiap strategi berwajaran `1 / turun naik`, dinormalisasi — jadi risiko, bukan dolar, dikongsi
dengan sama rata. Ia juga mengembalikan:

- **Matriks korelasi** merentasi strategi anda (ketahui yang secara senyap sama pertaruhan);
- **Turun naik portfolio yang diunjurkan** pada wajaran tersebut, dari kovarians contoh;
- **Faktor leverage** yang menskalakan seluruh buku ke arah turun naik tahunan sasaran anda (dibatasi).

```http
POST /api/quant/portfolio
{ "strategies": [[...], [...]], "targetVolatility": 0.10, "leverageCap": 3 }
```

## Mengapa ia boleh dipercayai

Kesemuanya ialah kod domain murni, deterministik (`Core.Portfolio`) dengan tiada kebergantungan infrastruktur dan tiada
panggilan luaran — diuji unit untuk penskalaan sasaran turun naik, formula Kelly, harta keseimbangan risiko songsang-volatiliti,
dan matriks korelasi. Secara lalai nasihat: nombor ialah cadangan, tidak pernah pesanan automatik.
