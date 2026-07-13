---
title: Ekonomik takvim
description: "cMind kendi ekonomik takvimini sunar — yayın takvimi, gerçek değerler, tahminler, revizyonlar ve birincil kaynaklardan sıfır bağımlılıkla veri odaklı etki modeli."
---

# Ekonomik takvim

cMind **kendi** ekonomik takvimini sunar — yayın takvimi, gerçek değerler, tahminler, revizyonlar ve
veri odaklı bir etki modeli — **birincil kaynaklardan** (merkez bankaları ve ulusal istatistik
kurumları) alınmış, ForexFactory, FXStreet, Investing.com veya herhangi bir toplayıcıya **sıfır
bağımlılıkla**. Anlık-doğrudur, ≥10 yıllık geçmişi korur ve ticaret, genel API, MCP, cBot'lar, AI,
uyarılar ve backtestlere bağlıdır. Ayrıştırılmış bir modüldür: ticaret çekirdeğine sıfır etkiyle
devre dışı bırakılabilir.

> **Durum.** P0–P4 uygulandı ve gönderildi. Alan çekirdeği, kalıcılık (EF `calendar` şeması,
> yalnızca ekleme okuma/yazma, FRED + BLS + merkez bankası takvim kaynakları, kaynak başına
> tazelik takibi ile yapılandırma kapılı alım işçisi), sürümlü JWT REST API, mobile-öncelikli
> `/economic-calendar` UI, MCP araçları, cBot JWT API, yüksek etkili olay uyarıları, copy-trade
> news-blackout duraklaması, backtest olay katmanı, SSE akışı, HMAC imzalı webhooklar ve tiplenmiş
> `CmindCalendarClient` — tümü uygulandı ve entegrasyon testi yapıldı. P5 ekstraları (sürpriz
> analitiği, iCal/CSV dışa aktarma, anahtar kelime arama, takılabilir konsensüs) kalan öğelerdir —
> aşağıdaki dağıtım aşamalarına bakın.

## Onu farklı kılan nedir?

Önde gelen takvimlere yönelik tekrar eden şikayetler tasarım kısıtlamalarımız oldu:

- **Sessiz etki-derecelendirme değişikliği yok.** Etki derecemiz **deterministik, sürümlü ve
  denetlenebilir**. Her değişiklik zaman damgalı kaydedilmiş bir revizyondur — asla sessiz bir
  üzerine yazma. Kullanıcı bir olayın neden High olduğunu tam olarak görebilir.
- **Olay başına bir UTC çıpası.** Her olay birincil kaynağın resmi takvimine ait tek bir UTC anına
  çıpalanır; kaynağın kendi saat dilimi depolanır ve kullanıcı başına oluşturma, bölge veritabanı
  tarafından işlenen DST ile açık IANA saat dilimi kullanır — asla manuel ±1s geçiş değil.
- **Her yerde tam revizyon zincirleri.** Orijinal değer ve her revizyon, API, MCP ve cBot
  yüzeylerinde özdeş şekilde sunulan birinci sınıf öğelerdir.
- **≥10 yıllık geçmiş, duvar yok.** Kısıtsız tarama aralığı; 60 günlük sınır yok, kayıt kapısı yok.
- **Yapı gereği anlık-doğru.** Her gerçek, `KnownAt` (ne zaman *öğrendik*) ve `EffectiveAt` (olay
  anı) taşır. "T anında takvim nasıl görünüyordu" birinci sınıf bir sorgudur, bu nedenle
  backtestteki bir haber kuralı, geçmişte revize değerlerin kullanımından look-ahead olmaksızın
  canlı gibi tam olarak davranır.

## Etki modeli

Etki puanı, Düşük / Orta / Yüksek / Kritik bandlarına ayrılmış `[0, 100]` içinde saf ve
deterministik bir fonksiyondur. Girdileri yalnızca puanlama zamanında bilinen verilerdir (gelecek
sızıntısı yok):

- **Seri önceliği** — gösterge sınıfı başına temel ağırlık (faiz kararı CPI'dan, CPI küçük bir
  anketten daha ağır basar).
- **Gerçekleşen oynaklık ayak izi** — bu serinin *geçmiş* yayınlarından sonraki pencerede birincil
  etkilenen sembollerin medyan mutlak getirisi: "bu yayın tarihsel olarak fiyatı bu kadar hareket
  ettirir."
- **Sürpriz hassasiyeti** — mutlak sürprizin (z-skoru) tarihsel olarak yayın sonrası hareketle ne
  kadar güçlü korelasyon gösterdiği.

Puan bunları sabit ağırlıklarla harmanlayarak `ImpactModelVersion` damgalar. Yeniden hesaplama, bir
mutasyon değil **yeni bir revizyon** üreten açık, günlüğe kaydedilmiş bir işlemdir — bu nedenle puan
her zaman girdilerinden yeniden üretilebilir.

## Ülke → para birimi → sembol eşleme

En çok alıntılanan algo entegrasyon can sıkıcı noktası bir kez saf bir fonksiyon olarak çözülür: bir
ülke para birimine eşlenir (her euro bölgesi üyesi EUR'a dahil olur) ve bir para birimi her iki ayakta
da kotasyon yapan izleme listesi sembollerine eşlenir. Dolayısıyla **EURUSD hem AB hem de ABD
olaylarından etkilenir**; XAUUSD USD'ye maruz kalır; US500, USD ile eşlenir. Bu, haber filtresini,
etkilenen sembol çözümünü ve kara kutu matematiğini yönetir.

## Haber penceresi politikası

Bir `NewsWindowRule`, `{ minImpact, beforeMinutes, afterMinutes, currencies?, series? }` olarak
tanımlanır. Tek, paylaşılan, saf bir uygulama "T anı, S sembolü için bir kara kutu içinde mi?"
sorusunu yanıtlar — cBot haber filtresi, copy-trade duraklaması ve AI risk koruması tarafından
kullanılır, bu nedenle asla ayrışamazlar.

## Anlık-doğruluk ve revizyonlar

Gerçekler, tahminler ve etki puanları **yalnızca eklemelidir**. Her olay, `KnownAt` içinde monoton
olan sıralı bir revizyon zincirine sahiptir:

- `Scheduled` — olay ilk zamanlandı (önceki etki, gerçek yok).
- `Released` — ilk basılmış gerçek geldi.
- `Revised` — sonraki revize edilmiş değer geldi.
- `Rescheduled` — kaynak yayın anını taşıdı (denetlenebilir, uyarılabilir).
- `Rescored` — etki puanı yeni bir model sürümü altında yeniden hesaplandı.

Geçmiş bir ana `as of` sorgusu, tam olarak o zaman bilinen revizyonu döndürür.

## Tahmin / konsensüs

Ekonomistlerin anket medyanı birincil kaynaklarca serbestçe yayınlanmaz. Olay şeması nullable bir
`Forecast` taşır; bir dağıtım, isteğe bağlı `IForecastProvider` portu aracılığıyla lisanslı bir
konsensüs beslemesi bağlayabilir (kendi anahtarınızı getirin, varsayılan olarak kapalı).

## Veri kaynakları

İki ayrıştırılmış katman, tümü birincil — asla bir toplayıcı değil:

- **Takvim / zamanlama:** FRED yayın takvimi; ulusal istatistik kurumları (BLS, BEA, Census,
  Eurostat, ONS, Destatis, INSEE, e-Stat, ABS, StatCan); merkez bankası toplantı takvimleri (Fed,
  ECB, BoE, BoJ, RBA, BoC, SNB, RBNZ).
- **Gerçek değerler:** FRED (revizyonlar ve anlık-doğruluk için vintage tarihleriyle birlikte), ayrıca
  BLS, BEA, Census, ECB SDW, Eurostat ve OECD SDMX API'leri.

## Etkinleştir / devre dışı bırak

İki bağımsız katman:

- **Katman 1 — çalışma zamanı özellik geçişi** (`Feature.EconomicCalendar`) Özellikler yönetici
  kullanıcı arayüzünden değiştirilir; yeniden dağıtım yok, canlı olarak geçerlilik kazanır.
- **Katman 2 — white-label sabit kapısı** (`App:Branding:EnableEconomicCalendar`, varsayılan `true`).

Etkin durum `Branding.EnableEconomicCalendar && FeatureToggle.EconomicCalendar`'dır. Devre dışı
bırakıldığında, gezinme girişi gizlenir ve `/economic-calendar`, `/api/calendar/**` ile MCP takvim
araçları temiz bir özellik-devre dışı `404` döndürür — asla `500` değil.

## Dağıtım aşamaları

- **P0 — alan çekirdeği** *(uygulandı)*: toplu nesneler, değer nesneleri, portlar, etki modeli.
- **P1 — kalıcılık + bir kaynak** *(uygulandı)*: EF `calendar` şeması, FRED bağlayıcısı, yapılandırma
  kapılı alım işçisi; Testcontainers entegrasyon testleri.
- **P2 — genel JWT REST API + Web UI** *(uygulandı)*: sürümlü, JWT güvenlikli `/api/calendar/v1` API
  ve mobile-öncelikli `/economic-calendar` sayfası.
- **P3 — daha fazla kaynak ve ısınma** *(başlandı)*: çekirdek seri kataloğu, proaktif geri dolum,
  varsayılan olarak açık alım, BLS ve merkez bankası takvim kaynağı.
- **P4 — derin entegrasyon** *(uygulandı)*: MCP araçları, uyarılar, copy-trade kara kutu duraklaması,
  backtest olay katmanı.
- **P5 — ekstralar**: sürpriz analitiği, iCal/CSV dışa aktarma, anahtar kelime arama, takılabilir
  konsensüs.

[cBot ve REST API referansına](calendar-cbot-api.md) bakın.

## Veri kaynağı gereklidir (kaynak olmadan özellik gizlidir)

Takvim, gerçek/tahmin/önceki değerleri yalnızca yapılandırılmış bir değer kaynağından (FRED veya BLS)
gösterir. `App:Calendar:FredApiKey` veya `App:Calendar:BlsApiKey` olmadan özellik gezintiden
**gizlenir**; bir kaynak olmadan zorla etkinleştirilirse, sayfa boş değerler yerine eyleme geçirilebilir
"bir veri kaynağı yapılandırın" bildirimi gösterir.
