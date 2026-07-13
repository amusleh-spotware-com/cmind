---
description: "Analisis Kos Urusniaga — mengukur kualiti pelaksanaan (gelinciran dalam mata asas dan implementation shortfall) pesanan berbanding harga ketibaannya, kelebihan pelaksanaan termampat yang bank-bank hidup darinya. Deterministik."
---

# Analisis Kos Urusniaga (TCA)

Alpha pelaksanaan adalah kecil setiap dagangan dan sangat besar melalui ribuan mereka — ia adalah sebahagian besar daripada bagaimana bank
dan meja prop mengekalkan kelebihannya. TCA mengukur betapa jauh harga yang anda sebenarnya capai menyimpang daripada harga
apabila anda *memutuskan* untuk berdagang.

Buka **cBots → Execution Cost** (`/quant/tca`).

## Apa yang diukur

Diberikan **harga ketibaan (keputusan)**, **sisi**, dan **isi** anda (harga × kuantiti), ia melaporkan:

- **Purata harga isi (VWAP)** — harga wajaran volum yang anda sebenarnya dapat.
- **Gelinciran (bps)** — penghayatan dari ketibaan ke VWAP dalam mata asas, **ditandatangani supaya nombor positif ialah kos**
  (beli di atas ketibaan atau jual di bawahnya) dan nombor negatif ialah pembaikan harga.
- **Implementation shortfall** — kos itu dinyatakan dalam istilah harga × kuantiti: wang yang hayunan kos anda pada pesanan ini.

```http
POST /api/quant/tca
{ "arrivalPrice": 1.1000, "side": "Buy",
  "fills": [ { "price": 1.1010, "quantity": 100 }, { "price": 1.1020, "quantity": 100 } ] }
```

## Pemotongan bijak (Almgren-Chriss)

Melangkaui mengukur kos, cMind boleh merancang pesanan besar untuk *meminimumkan* ia. **cBots → Execution Schedule**
(`/quant/execution`) membina **jadual pelaksanaan optimum Almgren-Chriss**: diberikan jumlah kuantiti,
bilangan hirisan, keengganan risiko, turun naik dan kesan pasaran sementara, ia mengembalikan saiz untuk
berdagang dalam setiap hirisan. Keengganan risiko yang lebih tinggi **memuatkan depan** jadual (memotong risiko masa); keengganan risiko sifar meratakan kepada TWAP yang sekata. Hirisan sentiasa menjumlahkan jumlah.

```http
POST /api/quant/execution-schedule
{ "totalQuantity": 100, "slices": 5, "riskAversion": 2, "volatility": 0.02, "temporaryImpact": 0.1 }
```

## Mengapa ia boleh dipercayai

Kod domain deterministik murni (`Core.Execution`) dengan tiada kebergantungan infrastruktur dan tiada panggilan luaran
— diuji unit untuk tanda kos beli/jual, pembaikan harga, gelinciran sifar, pengagregatan VWAP, dan penjaga input. Ini adalah separuh pengukuran kualiti pelaksanaan; ia adalah metrik shortfall yang sama yang digunakan oleh enjin salinan untuk menilai (dan, dengan pemotongan bijak, mengurangkan) kos pesanan yang dicerminkan.
