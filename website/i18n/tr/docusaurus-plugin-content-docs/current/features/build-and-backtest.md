---
title: Derle & Backtest
description: Monaco IDE'de C# ve Python cBot yazılı — dan kaynağına derle bir sandbox Docker konteyneri - Backtest tarihsel veriler karşı.
sidebar_position: 3
---

# cBot Derleme & Backtesting

Tarayıcıda kod yazılı, docker'da derle, gerçek veriler üzerinde backtest çalıştırın.

## Monaco IDE

- **C# & Python** kod şablonları
- **IntelliSense** - tamamlama, çabuk bilgi
- **Kaydet & Sürüm** — her sürümü tutun

## Derleme

1. "Derle" 'ye basıyor
2. `CBotBuilder` dockerfile'dan başlatır
3. Sorunu yapı → çıkış ile derle
4. Başarıyı sürünürlük çalışlasını hazırlar

## Backtest

1. Backtest parametrelerini ayarla (tarihi, sembol, kütüphane)
2. "Backtest Koş" tıklayın
3. cTrader Console konteyneri tarihsel veriler sürüyor
4. Öz eğri & kişileri gözleyin gerçek zamanlı olarak
5. PDF raporu veya CSV dışarı aktar

## Kütüphane

cMind cTrader Console ve cTrader Open API'ye erişim sağlar:

```csharp
[Parameter("Take Profit", DefaultValue = 50)]
public double TakeProfitPips { get; set; }

protected override void OnTick()
{
  var ask = Symbol.Ask;
  // Ticaret mantığı
}
```

Daha fazla: [Backtest Bütünlüğü →](./backtest-integrity.md)

## Kod düzenleyiciden çalıştırma

Kod düzenleyicide **Çalıştır**'a tıklamak, kör ve sabit kodlanmış bir çalıştırma başlatmak yerine bir iletişim kutusu açar:

- **İşlem hesabı** (gerekli) — cBot'un bağlanacağı cTrader hesabı.
- **Parametre kümesi** (isteğe bağlı) — mevcut bir küme seçin veya cBot'un **varsayılan parametre değerleriyle** çalıştırmak için boş bırakın. Seçicinin yanındaki **+** düğmesi, satır içinde yeni bir parametre kümesi oluşturur (aşağıya bakın) ve onu seçer.
- **Sembol / Zaman dilimi** varsayılan olarak `EURUSD` / `h1`'dir ve değiştirilebilir; **İptal** veya **Çalıştır**.

**Çalıştır** ile düzenleyici mevcut kaynağı kaydeder ve derler, seçilen hesapta seçilen parametrelerle örneği başlatır, ardından canlı konteyner günlüklerini izler. (Günlük akışı, oturum açmış kullanıcının kimlik doğrulama çerezini SignalR hub'ı `/hubs/logs`'a iletir; böylece `Invalid negotiation response received` hatasıyla başarısız olmak yerine bağlanır.)

## Parametre kümeleri

Bir **parametre kümesi**, her parametre adını bir skaler değere eşleyen düz bir JSON nesnesi olarak saklanan, adlandırılmış ve yeniden kullanılabilir cBot parametre geçersiz kılmaları kümesidir, örn. `{"Period": 14, "Label": "trend"}`. Çalıştırma/backtest sırasında cTrader `params.cbotset` dosyasına (`{ "Parameters": { … } }`) dönüştürülür. Bir kümeyi cBot'un **Parametre kümeleri** iletişim kutusundan ham JSON olarak veya Çalıştır iletişim kutusundan satır içinde oluşturabilir/düzenleyebilirsiniz.

JSON kaydederken **doğrulanır**: değerlerinin tümü skaler (dize / sayı / bool) olan tek bir düz nesne olmalıdır. Nesne olmayan bir kök, bir dizi, iç içe bir nesne, bir `null` değer veya hatalı biçimlendirilmiş JSON reddedilir (iletişim kutusunda net bir hata, API'de `400 Bad Request`). Boş bir nesne `{}` izinlidir ve "geçersiz kılma yok" anlamına gelir.

## Örnek yaşam döngüsü denetimleri

Her örnek satırında (ve ayrıntı sayfasında) duruma uygun denetimler bulunur. **Etkin** bir örnek **Durdur**; **sonlanmış** bir örnek (Durduruldu / Tamamlandı / Başarısız) aynı cBot, hesap, sembol, zaman dilimi, parametre kümesi ve görüntüyle yeniden başlatmak için **Başlat (▶)** gösterir (bir çalıştırma çalıştırma olarak, bir backtest backtest olarak yeniden başlar). Durdur'a tıklamak "Durduruluyor…" bildirimi gösterir ve çözülene kadar simgeyi devre dışı bırakır; yeni oluşturulan bir çalıştırma listede hemen görünür — sayfa yeniden yüklenmeden.

Konsol günlükleri **bir örnek sonlandığında kalıcı hale getirilir** — hem bir çalıştırma için (durdurmada) hem de bir **backtest** için (tamamlanmada) — böylece son çalıştırmanın günlükleri ayrıntı sayfasında görünür kalır ve konteyner gittikten sonra bile **Günlükleri indir** simgesiyle indirilebilir.
