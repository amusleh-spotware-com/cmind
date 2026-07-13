# AI makro para birimi gücü & ileri görünüm

cMind, **AI-destekli, matematik-deterministik** bir makro para birimi gücü motoru gönderir.
Yapılandırılabilir bir para birimi evrenini — 8 major artı gelişmekte olan piyasa ve egzotik para
birimleri — **mevcut** temel güce göre sıralar ve seçilen bir ufuk (1M / 3M / 6M / 12M) boyunca her çift
için bir **ileri yönlü görünüm** öngörür. Her sıralama, her çift eğilimi ve her sayı, alan çekirdeğindeki
saf deterministik matematik tarafından hesaplanır; LLM yalnızca verinin yayımlayamadığı ileri-bakışlı
girdileri *toplar* ve sonucu sade İngilizceyle *açıklar*. Asla bir sıralama, bir yön veya bir sayı icat
etmez.

> **Dürüst kısıtlama.** Temeller orta-uzun vadeli değeri iyi, kısa vadeli değeri kötü tahmin eder. Bunu
> bir konumlandırma / doğrulama filtresi olarak ele alın, kısa vadeli bir zamanlama sinyali **olarak
> değil**. Yüksek-etkili yayınların (NFP/CPI/merkez-bankası) yakınındaki okumalar gürültülüdür. Finansal
> tavsiye değildir.

## Nasıl çalışır

1. **Mevcut temeller LLM'den değil, Ekonomik Takvim'den gelir.** Sabit sayılar — politika oranları,
   hedefe karşı CPI, GSYİH, istihdam, ticaret dengesi — ve onların **sürpriz z-skorları**,
   [ekonomik takvim](./economic-calendar.md) modülünden (FRED/BLS/BEA/ECB ve merkez-bankası
   programları) **zaman-noktası** olarak kaynaklanır. Bir tarihsel anlık görüntü asla ileri-bakış
   sızdırmaz.
2. **LLM yalnızca takvimin yayımlayamadığını toplar** — para birimi başına: **ileri** yörünge (bp
   cinsinden beklenen politika-oranı yolu, hedefe-karşı-enflasyon-eğilimi, büyüme momentumu) ve bir
   **jeopolitik** görünüm (risk-on/off, tarifeler, mali/borç, seçimler), artı takvimin eksik olduğu
   herhangi bir EM/egzotik mevcut rakam. Katı JSON, katman-bilinçli doğrulama, web araması açık.
3. **Alan, sıralamayı ve ileri matrisi deterministik olarak hesaplar.** Her sürücü bir **katman-içi
   z-skoru** olarak puanlanır (böylece %50-enflasyonlu bir egzotik major'ları asla çarpıtmaz),
   winsorize edilir, ağırlık-toplanarak bir bileşiğe dönüştürülür ve kararlı bir ISO eşitlik-bozma ile
   güçlüden→zayıfa sıralanır. İleri katman her bileşiği yörüngesi boyunca taşır —
   `projected = current + horizonScale · Σ trajectoryDriver·weight` — ve her çiftin öngörülen farkını
   bir güven ile bir **yönlü eğilime** (▲ değer kazanır / ▬ nötr / ▼ değer kaybeder) eşler.
4. **LLM açıklar** sıralamayı ve en iyi çift çağrılarını sade dille.

## Sürücüler

| Sürücü | Güce etkisi | Notlar |
|---|---|---|
| Politika oranı & yörünge | Daha yüksek / şahin ⇒ daha güçlü | En yüksek ağırlık; merkez-bankası ayrışması en büyük boşlukları sürükler. |
| Enflasyon (hedefe karşı CPI) | Hedefin üstünde ⇒ daha zayıf | Ters puanlanır (satın-alma-gücü frenlemesi). |
| GSYİH büyümesi | Daha yüksek göreli büyüme ⇒ daha güçlü | Panele karşı fark. |
| İstihdam | Daha güçlü işgücü ⇒ daha güçlü | Politika yolunu besler. |
| Ticaret dengesi / cari hesap | Fazla ⇒ daha güçlü | Yapısal talep. |
| Politika duruşu | Şahin ⇒ daha güçlü | Birincil uzun vadeli sürücü. |
| Sürpriz momentumu | Son beklentileri aşmalar ⇒ daha güçlü | Takvimin sürpriz z-skorlarından. |
| Jeopolitik / risk | Risk-off ⇒ güvenli limanlar (USD/JPY/CHF) daha güçlü | Sınırlı ileri risk deltası. |
| Reel getiri / carry *(EM/egzotik)* | Pozitif reel oran ⇒ daha güçlü | Sakin rejimlerde baskın EM sürücüsü. |
| Dış kırılganlık *(EM/egzotik)* | Açıklar / düşük rezervler / USD borcu ⇒ daha zayıf | Yapısal değer kaybı baskısı. |
| Ticaret hadleri *(emtia ihracatçıları)* | Yükselen ihracat fiyatları ⇒ daha güçlü | BRL, ZAR, CLP, NOK, AUD, CAD. |
| Siyasi / kurumsal risk *(EM/egzotik)* | İstikrarsızlık ⇒ daha zayıf | Daha geniş ölü-bant, sınırlı güven. |

## Katmanlı evren (major'lar + EM + egzotikler)

Evren **dağıtım-yapılandırılabilir** (`App:CurrencyStrength:Universe`) — bir para birimi eklemek kod
değil, yapılandırmadır. Her para birimi, ağırlıklandırmayı, ölü-bant genişliğini ve güven sınırını ayarlayan
bir **katman** (`Major` / `EmergingMarket` / `Exotic`) taşır:

- **Major'lar** — USD, EUR, GBP, JPY, AUD, NZD, CAD, CHF (oran-seviyesi öncülüklü).
- **Gelişmekte olan piyasalar** — CNH, INR, BRL, MXN, ZAR, KRW, SGD, PLN (+ İskandinav NOK/SEK); carry
  + risk + dış-kırılganlık ağırlıklandırılmış, orta güven.
- **Egzotikler** — TRY, HUF, CZK, artı USD-sabitli HKD/SAR; düşük güven, daha geniş ölü-bant, sınırlı
  güven. **Sabitli / yoğun-yönetilen** para birimleri (HKD, SAR, CNH) işaretlenir, yörüngeleri
  aşağı-ağırlıklandırılır ve çift görünümleri `Neutral`'a doğru kenetlenir, böylece bir sabit asla
  serbest-yüzen bir sinyal olarak okunmaz.

Resmi EM/egzotik istatistikleri daha düşük-frekanslı, revize edilmiş ve bazen opak olduğundan,
AI-toplanmış rakamlar bir güvenilirlik rozeti olarak gösterilen bir **katman-başına güven** taşır.

## Zarif bozulma

| Takvim | AI | Sonuç |
|---|---|---|
| ✅ | ✅ | Tam sıralama + ileri projeksiyon + anlatı (`CalendarAndAi`). |
| ✅ | ❌ | Yalnızca-takvim mevcut sıralama, ileri projeksiyon yok (`CalendarOnly`). |
| ❌ | ✅ | AI-toplanmış mevcut rakamlar + ileri, daha düşük güven (`AiOnly`). |
| ❌ | ❌ | Anlık görüntü yok — widget gizlenir ve sayfa boş bir durum gösterir. |

Uygulama her iki durumda da değişmeden çalışır. AI, AI anahtarıyla kapılıdır; takvim ayağı kendi
beyaz-etiket kapısına + çalışma zamanı geçişine saygı gösterir.

## Kullanımı

- **AI'yi etkinleştirin** (Settings → AI) ve kendi gösterge panelinizin **Customize** iletişim
  kutusundan **widget'i açın** ("Currency strength" — opt-in, varsayılan olarak gizli). Widget en güçlü/
  zayıf para birimlerini ve en iyi 3M çift çağrısını gösterir; tam sayfaya bağlanır.
- **Tam sayfa** — `/ai/currency-strength`: bir ufuk seçici (1M/3M/6M/12M), bir katman filtresi
  (All/Majors/EM/Exotics), mevcut sıralama, ileri tahmin, çift-görünüm matrisi (eğilim + güven,
  sabitli/düşük-güvenli işaretli) ve AI anlatısı. Yeniden oluşturmak için **Refresh now** (sahip)
  düğmesine basın. Bir arka plan işçisi (`App:CurrencyStrength:RefreshEnabled`, **varsayılan `true`**)
  bir programa göre yeniler, böylece sayfa kutudan çıktığı gibi doldurulur; bir dağıtım veya sahip onu
  kapatır (veya AI / ekonomik-takvim özelliğini devre dışı bırakır, ki yenileyici bunu anlık görüntü
  yok'a bozularak onurlandırır).

## Programatik erişim

Bir paylaşılan okuma modeli (`ICurrencyStrengthQuery`) üç yolla erişilebilir:

- **Uygulama-içi AI** — doğrudan (süreç-içi) AI özelliklerine enjekte edilir.
- **MCP** — AI istemcileri/aracıları için `currency_strength` aracı (parametreler `horizon`, `tier`).
- **cBot REST** — `GET /api/market/v1/currency-strength/{latest,history,pair/{base}/{quote}}`,
  [takvim cBot API'si](./calendar-cbot-api.md) ile **aynı** `CalendarJwt` mekanizmasıyla güvenli, eklenen
  bir **`market:read`** kapsamıyla. Bir cBot `market:read` ile bir API istemcisi kaydeder, id + secret'ini
  `POST /api/calendar/v1/token`'da kısa ömürlü bir JWT ile değiştirir ve uç noktaları bir `Bearer`
  token'ıyla çağırır. İkinci bir JWT şeması yok, ikinci bir secret yok — sızdırılmış bir token
  yalnızca-okunur, market-kapsamlı, kısa-ömürlü ve iptal edilebilirdir.

Token akışı ve kopyala-yapıştır bir örnek için [takvim cBot API'sine](./calendar-cbot-api.md) bakın.
