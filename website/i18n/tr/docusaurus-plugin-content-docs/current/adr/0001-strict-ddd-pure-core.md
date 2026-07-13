---
title: "ADR-0001: Katı DDD, saf Core"
---

# ADR-0001: Katı DDD, saf Core

## Bağlam

cMind finans-duyarlı bir platform — bir hata gerçek parayı kaybedebilir. İş mantığı dokunmazlık
gereklidir. Kod tabanı büyür.

## Karar

Tüm iş alanı mantığı **saf `src/Core`** 'da yaşar:

- Varlıklar, toplamalar, değer nesneleri
- Güçlü ID'ler, etki alanı olayları
- Hiçbir HttpClient, hiçbir EF, hiçbir ASP.NET — sadece C# + matematik

Altyapı — EF, Web, DI — **etki alanı yöntemleri** kapsamında `Core` 'u çağırır. Uç noktalar ve
arka plan hizmetleri **hiçbir zaman** iş mantığı karar vertemez.

## Sonuçlar

- İş mantığı, test **birim testleri ile**, harici bağımlılıklar olmadan.
- Değişiklikler Web tarafında veya EF tarafında, Core tarafı tehdit etmez.
- Uyum sağlama testi zorunlu — hiçbir Core bağımlılığı altyapıya
  fırlatılmış.
