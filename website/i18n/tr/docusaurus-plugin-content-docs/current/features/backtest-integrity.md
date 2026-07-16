---
description: "Backtest Integrity Lab — belirlenimci, fon-sınıfı aşırı uyumlandırma istatistikleri (Olasılıksal & Deflate Sharpe, t-istatistik) ham bir backtesti Sağlam / Kırılgan / Aşırı Uyumlandırılmış karara dönüştüren, denediğiniz yapılandırma sayısını düzelten."
---

# Backtest Integrity Lab

Perakende platformlar size bir backtest'in Sharpe'sini veya net karını gösterir ve durada kalırlar. Kurumlar asla 
ham bir backtest'e güvenmezler — sonucun **seçim sapması ve denediğiniz yapılandırma sayısı için düzeltme** karşısında 
dayanıp dayanmadığını sorarlar. Backtest Integrity Lab bu kontrolü cMind'e getiriyor. Bu **belirlenimci 
matematik** (yapay zeka yok, dış çağrı yok), bu nedenle sonuç tekrarlanabilir ve her sayı açıklanabilir.

**cBots → Integrity** üzerinde açın (`/quant/integrity`).

## Neyi hesapladığı

Bir getiri serisi (veya bir öz kaynaklar/bakiye eğrisi) ve buna ulaşmak için denediğiniz parametre seti sayısı verildiğinde, 
analizci bildirir:

- **Sharpe oranı** — dönem başına ve yıllıklandırılmış (zamanın karekökü).
- **Olasılıksal Sharpe Oranı (PSR)** — *gerçek* Sharpe'nin benchmarku yenme güvenirliği, 
  rekor uzunluğu, çarpıklık ve basıklığı dikkate alarak (Bailey & López de Prado, 2012). Kısa veya 
  kalın kuyruklu bir rekor bunu düşürür.
- **Deflate Sharpe Oranı (DSR)** — **deflate edilmiş bir benchmark** karşısında ölçülen PSR: Sıfırın altındaki 
  *N rastgele denemenin en iyisinden* beklediğiniz Sharpe (Yanlış Strateji Teoremi). Ne kadar çok 
  yapılandırmayı denediyseniz, bar o kadar yüksek — bu aşırı uyumlandırmayı yakalar.
- Ortalama dönüşün **t-istatistik**i. Harvey, Liu & Zhu'yu takiben, gerçek bir kenarın **t ≥ 3.0** numarası geçmesi gerekir, 
  ders kitabı 2.0 değil.
- Döndürlerin **çarpıklık / basıklığı**, hangileri PSR/DSR düzeltmelerine beslenmiş.

## Sonuç

| Sonuç | Anlamı | Kural |
|---|---|---|
| **Sağlam** | Kenar denediğiniz denemelerden sağ çıkıyor. | DSR ≥ 95% **ve** PSR ≥ 95% **ve** \|t\| ≥ 3.0 |
| **Kırılgan** | İstatistiksel olarak canlı ama ikna edici değil — bunu tek başına yükseltmeyin. | iki arasında |
| **Aşırı Uyumlandırılmış** | Büyük olasılıkla seçim sapmasının bir artefaktı, gerçek bir kenar değil. | DSR < 90% |

Her sonuç, "neden"in asla gizli kalmadığı düz İngilizce bir mantık taşır.

## Backtest Aşırı Uyumlandırması Olasılığı (denemeler arasında)

Bir deneme *sayısı* beslemek iyi; denediğiniz her yapılandırmanın **gerçek dışı örnek serisini** 
beslemek daha iyidir. Bunları isteğe bağlı **deneme ızgarasına** yapıştırın (satır başına bir seri) ve cMind 
**Kombinatoryal Simetrik Çapraz Doğrulama** (Bailey, Borwein, López de Prado & Zhu, 2015) çalıştırır: gözlemleri gruplara böler ve 
yarısını örnekde seçmenin her yolu için, örneğin içi en iyi yapılandırmayı seçer ve o kazananın dışarıda kalıp kalmadığını kontrol eder 
**örnek dışı**. **Backtest Aşırı Uyumlandırması Olasılığı (PBO)**, kazananın genellemeyi başaramadığı bölünmelerin kesridir. 
0'a yakın bir PBO, en iyi yapılandırmanın gerçekten en iyi olduğu anlamına gelir; 0.5 veya daha yüksek bir PBO, seçim işleminizin 
gürültüyü seçtiği anlamına gelir — sonuç kazananın ne kadar iyi görünmesine bakılmaksızın **Aşırı Uyumlandırılmış** olur.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Yerel cTrader Console iyileştirici geldiğinde, cMind tam deneme yüzeyini buraya otomatik olarak besleyecektir.

## Denemeler — önemli olan sayı

`Trials` **bunu seçmeden önce test ettiğiniz parametre seti sayısı**dır. Bir strateji test etmek ve 
on bini test etmek ve en iyisini tutmak çok farklı şeylerdir: ikincisi şans eseri yüksek bir örnekde Sharpe oluşturur. 
Dürüst deneme sayısını beslemek bütün nokta — bu deflasyonu yükseltir ve "harika" bir backtest'i **Aşırı Uyumlandırılmış**'a taşıyabilir. 
Yerel cTrader Console iyileştirici geldiğinde, cMind tarama ızgarasının gerçek boyutunu otomatik olarak besler.

## Girdiler

- **Periyodik getiriler** — dönem başına bir sayı (örneğin `0.01` = +%1). En az iki. Alan yazarken doğrular: 
  geçerli sayıları sayar, sayı olmayan herhangi bir belirteci işaretler ve en az iki temiz değer mevcut olduktan sonra **Analiz**'i etkinleştirir 
  (deneme ızgarası, dört artı sayıdan oluşan iki seri hazır olduktan sonra **Aşırı uyumlandırmayı değerlendir**'i etkinleştirir).
- **Öz kaynaklar / bakiye eğrisi** — cMind ardışık basit dönüşleri sizin için türetir.
- **Doğrudan bir backtest çalışmasından — kopyala-yapıştır yok.** Tamamlanan her backtest, **Backtest** liste satırında ve 
  örnek ayrıntı görünümünde bir kalkan **Backtest bütünlüğünü kontrol et** simgesi ortaya koymaktadır; bir tıkla Laboratuvarı o çalışmanın saklanan 
  öz kaynaklar eğrisi üzerinde çalıştırır ve sonucu bir iletişim kutusunda gösterir. Simge, backtest tamamlanana ve bir rapor üretene kadar 
  devre dışı bırakılır, bu nedenle asla ölü bir kontrol değildir. Perde arkasında bu `POST /api/quant/integrity/backtest/{instanceId}` 
  yapılır, bu da saklanan raporun öz kaynaklar eğrisini okur.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Sonuç, tüm ölçümleri ve mantığı döndürür. `POST /api/quant/integrity/backtest/{id}` sahip olduğunuz tamamlanmış bir 
backtest üzerinde aynı analizi çalıştırır.

## Neden güvenilir

İstatistikler, etki alanı çekirdeğinde (`Core.Quant`) saf işlevlerdir ve sıfır altyapı 
bağımlılıkları vardır — ağ aksaklığı tarafından düşürülemezler ve yayınlanan formüllere karşı altın vektör birim 
testleri tarafından sabitlenmişlerdir. Normal CDF/ters, kapalı form yaklaşımlarıdır 
(Abramowitz-Stegun / Acklam), bu nedenle aynı girdiler her zaman aynı sonucu verir.
