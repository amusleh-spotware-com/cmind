---
description: "Backtest Integrity Lab — bir ham backtest'i, kaç konfigürasyon denediğinizi düzelterek Sağlam / Kırılgan / Aşırı Uyarlanmış bir hükme dönüştüren deterministik, fon düzeyinde aşırı uyarlama istatistikleri (Olasılıksal & Deflate Edilmiş Sharpe, t-istatistiği)."
---

# Backtest Integrity Lab

Perakende platformlar size bir backtest'in Sharpe'ını veya net kârını gösterir ve orada durur.
Kurumlar bir ham backtest'e asla güvenmez — sonucun **seçim yanlılığı ve denenen konfigürasyon sayısı
için düzeltmeden sağ çıkıp çıkmadığını** sorarlar. Backtest Integrity Lab bu kontrolü cMind'e getirir.
Bu **deterministik matematiktir** (yapay zeka yok, harici çağrı yok), bu yüzden hüküm yeniden
üretilebilir ve her sayı açıklanabilir.

Onu **cBots → Integrity** (`/quant/integrity`) altında açın.

## Ne hesaplar

Bir getiri serisi (veya bir öz sermaye/bakiye eğrisi) ve ona ulaşmak için denediğiniz parametre
kümesi sayısı verildiğinde, çözümleyici şunları raporlar:

- **Sharpe oranı** — dönem başına ve yıllıklandırılmış (zamanın karekökü).
- **Olasılıksal Sharpe Oranı (PSR)** — *gerçek* Sharpe'ın kıyaslamayı geçtiğine dair güven; kayıt
  uzunluğunu, çarpıklığı ve basıklığı hesaba katar (Bailey & López de Prado, 2012). Kısa veya kalın
  kuyruklu bir kayıt onu düşürür.
- **Deflate Edilmiş Sharpe Oranı (DSR)** — bir **deflate edilmiş kıyaslamaya** karşı ölçülen PSR:
  boş hipotez altında *N rastgele denemenin en iyisinden* beklediğiniz Sharpe (Yanlış Strateji
  Teoremi). Ne kadar çok konfigürasyon denediyseniz, çıta o kadar yükselir — aşırı uyarlamayı yakalayan
  budur.
- Ortalama getirinin **t-istatistiği**. Harvey, Liu & Zhu'yu takiben, gerçek bir üstünlük ders
  kitabındaki 2.0'ı değil **t ≥ 3.0**'ı geçmelidir.
- Getirilerin **çarpıklığı / basıklığı**; bunlar PSR/DSR düzeltmelerini besler.

## Hüküm

| Hüküm | Anlamı | Kural |
|---|---|---|
| **Sağlam** | Üstünlük çalıştırdığınız denemelerden sağ çıkar. | DSR ≥ %95 **ve** PSR ≥ %95 **ve** \|t\| ≥ 3.0 |
| **Kırılgan** | İstatistiksel olarak canlı ama ikna edici biçimde değil — yalnızca buna dayanarak pozisyon büyütmeyin. | ikisinin arasında |
| **Aşırı Uyarlanmış** | Büyük olasılıkla gerçek bir üstünlük değil, seçim yanlılığının bir yapaylığı. | DSR < %90 |

Her sonuç sade İngilizce bir gerekçe taşır, böylece "neden" asla gizli kalmaz.

## Backtest Aşırı Uyarlama Olasılığı (denemeler boyunca)

Bir deneme *sayısı* beslemek iyidir; **denediğiniz her konfigürasyonun gerçek örneklem-dışı serisini**
beslemek daha iyidir. Bunları isteğe bağlı **deneme ızgarasına** yapıştırın (satır başına bir seri) ve
cMind **Kombinatoryal-Simetrik Çapraz Doğrulama** (Bailey, Borwein, López de Prado & Zhu, 2015)
çalıştırır: gözlemleri gruplara böler ve yarısını örneklem-içi seçmenin her yolu için örneklem-içi en
iyi konfigürasyonu seçer ve o kazananın **örneklem-dışı** alt yarıya düşüp düşmediğini kontrol eder.
**Backtest Aşırı Uyarlama Olasılığı (PBO)**, kazananın genelleyemediği bölmelerin oranıdır. 0'a yakın
bir PBO, en iyi konfigürasyonun gerçekten en iyi olduğu anlamına gelir; 0.5 veya daha yüksek bir PBO,
seçim sürecinizin gürültü seçtiği anlamına gelir — kazanan ne kadar iyi görünürse görünsün hüküm
**Aşırı Uyarlanmış** olur.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Yerel cTrader Console optimize edici geldiğinde, cMind onun tam deneme yüzeyini otomatik olarak buraya
besleyecektir.

## Denemeler — önemli olan sayı

`Trials`, bunu seçmeden önce **kaç parametre kümesini test ettiğinizdir**. Bir stratejiyi test etmek ile
on bin tane test edip en iyisini tutmak son derece farklı şeylerdir: ikincisi, şans eseri yüksek bir
örneklem-içi Sharpe imal eder. Dürüst deneme sayısını beslemek tüm mesele budur — deflasyonu yükseltir
ve bir "harika" backtest'i **Aşırı Uyarlanmış**'a taşıyabilir. Yerel cTrader Console optimize edici
geldiğinde, cMind ona taramanın gerçek ızgara boyutunu otomatik olarak besler.

## Girdiler

- **Dönemsel getiriler** — dönem başına bir sayı (örn. `0.01` = +%1). En az iki.
- **Öz sermaye / bakiye eğrisi** — cMind ardışık basit getirileri sizin için türetir.
- Veya doğrudan tamamlanmış bir backtest üzerinde çalıştırın:
  `POST /api/quant/integrity/backtest/{instanceId}` saklanan raporun öz sermaye eğrisini okur.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Hükmü, tüm metrikleri ve gerekçeyi döndürür. `POST /api/quant/integrity/backtest/{id}` aynı çözümlemeyi
sahip olduğunuz tamamlanmış bir backtest üzerinde çalıştırır.

## Neden güvenilir

İstatistikler, sıfır altyapı bağımlılığı olan alan çekirdeğindeki (`Core.Quant`) saf fonksiyonlardır —
bir ağ kesintisiyle çökertilemezler ve yayımlanmış formüllere karşı altın-vektör birim testleriyle
sabitlenmişlerdir. Normal CDF/ters formu kapalı-form yaklaşımlardır (Abramowitz-Stegun / Acklam), bu
yüzden aynı girdiler her zaman aynı hükmü verir.
