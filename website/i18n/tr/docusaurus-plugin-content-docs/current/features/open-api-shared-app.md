---
description: "Her kullanıcı için tek bir cTrader Open API uygulaması gönder (white-label paylaşımlı mod), kaydolunacak tek yönlendirme URL'si ve mesaj-türü başına istemci hız limitleri."
---

# Paylaşılan Open API uygulaması ve hız limitleri

Varsayılan olarak her kullanıcı **Settings → Open API** altında **kendi** cTrader Open API uygulamasını
kaydeder. Bir white-label operatörü (tipik olarak bir cTrader broker'ı veya satıcısı) bunun yerine **tüm
kullanıcılar için tek bir paylaşılan Open API uygulaması** gönderebilir — kimse kendininkini kaydetmez;
herkes hesaplarını operatörün tek uygulaması aracılığıyla yetkilendirir.

## Paylaşılan uygulamayı sağlamanın iki yolu

Paylaşılan uygulama, dağıtım yapılandırmasından **veya** sahip ayarları UI'sinden sağlanır (sahip-ayarlı
değer kazanır). Bir kez sağlayın ve paylaşımlı mod herkes için açılır.

### 1. Dağıtım yapılandırması (başlangıçta tohumlanır)

```jsonc
"App": {
  "OpenApi": {
    "PublicBaseUrl": "https://cmind.yourbroker.com",   // BU dağıtımın kanonik genel URL'si
    "SharedApp": {
      "Enabled": true,
      "Name": "YourBroker Open API",
      "ClientId": "1234_abcd...",
      "ClientSecret": "…"                                // durağan hâlde şifreli; asla günlüğe kaydedilmez
    }
  }
}
```

Başlangıçta uygulama, sahip hesabına ait bir paylaşılan uygulama tohumlar (idempotent — bir sahip-düzenlenmiş
çalışma zamanı değerini asla üzerine yazmaz ve yeniden-tohumlama bir no-op'tur).

### 2. Sahip ayarları (çalışma zamanı, yeniden dağıtım yok)

**Settings → Open API** (yalnızca sahip), bir **Deployment shared application** kartı gösterir: paylaşılan
uygulamayı ekle / düzenle / sil, kopyala-yapıştır için görüntülenen yönlendirme URL'siyle. Değişiklikler yeni
yetkilendirmeler için hemen etkili olur.

## Yönlendirme URL'si (bunu cTrader'da kaydedin)

Her cTrader Open API uygulaması **bir** yönlendirme URL'si kaydeder — paylaşılan uygulama ve herhangi bir
kullanıcı-başına uygulama için **aynı tek değer**:

```
{dağıtım URL'niz}/openapi/callback
```

örneğin `https://cmind.yourbroker.com/openapi/callback`.

- Uygulama, Open API ayarları sayfasında **tam değeri görüntüler** (bir kopyalama düğmesiyle) — Open API
  uygulamasını oluşturduğunuzda onu cTrader ortak portalına yapıştırın.
- `App:OpenApi:PublicBaseUrl`'den oluşturulur, böylece bir ters vekil / CDN arkasında kararlı kalır; bu
  ayarlanmadığında gelen istek host'una geri döner.
- Davet vs normal-kullanıcı deneyimi yalnızca kullanıcının geri-çağrıdan **sonra** nereye indiğinde farklıdır
  (hesap listeleri vs bir "hesaplar eklendi" onayı) — kayıtlı yönlendirme URL'si değişmez.

## Kullanıcılar paylaşımlı modda ne görür

Bir paylaşılan uygulama var olduğunda:

- Kullanıcılar kendi Open API uygulamalarını kaydetme **seçeneği almaz** — ayarlar sayfası **"Open API
  sağlayıcınız tarafından yönetiliyor"** ve paylaşılan uygulamayı kullanan bir **Authorize accounts**
  düğmesi gösterir.
- Önceden var olan kişisel uygulamalar **kaldırılır**; yetkilendirilmiş hesapları paylaşılan uygulamaya
  yeniden yönlendirilir ve **yeniden yetkilendirilmelidir** (eski belirteçleri farklı bir istemci kimliği
  altında verilmişti). Kişisel bir uygulama oluşturmaya çalışmak "sağlayıcınız tarafından yönetiliyor" hatası döndürür.

## İstemci hız limitleri (mesaj türü başına)

İstemci, giden cTrader Open API mesajlarını hızlandırır, böylece bir patlama asla sunucu-tarafı hız-limit
blokunu tetiklemez. Limitler, cTrader Open API belgeleriyle eşleşerek **mesaj türü başınadır**:

| Kategori | Neyi kapsar | Varsayılan |
|---|---|---|
| `General` | işlem + okuma mesajları (emirler, semboller, hesap sorguları) | 45 mesaj/s |
| `HistoricalData` | trendbar / tik-verisi istekleri (cTrader tarafından daha sert kısıtlanır) | 5 mesaj/s |

Bir geçmiş-verisi isteği **hem** kendi kovasına hem de genel kovaya sayılır. Kalp atışı ve kimlik doğrulama
mesajları asla hızlandırılmaz. Mesajlar kuyruğa girer ve mevcut hızda boşalır — hiçbir şey düşürülmez ve
sıra korunur.

Broker'ınız **daha yüksek** cTrader limitleri müzakere ettiyse onları ayarlayın veya hızlandırmayı tamamen
devre dışı bırakmak için bir kategoriyi **`0`** yapın (sınırsız):

- **Yapılandırma:** `App:OpenApi:RateLimits:General` / `App:OpenApi:RateLimits:HistoricalData` (mesaj/saniye).
- **Sahip ayarları:** **Settings → Open API**'daki **Client rate limits** kartı (sahip geçersiz kılması
  kazanır, yeni bağlantılara / yeniden bağlanmada uygulanır).
