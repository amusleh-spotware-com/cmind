---
id: white-label-owner-settings
title: Sahip ayarlarında white-label seçenekleri
sidebar_label: White-label sahip ayarları
---

# Sahip ayarlarında white-label seçenekleri

Bir dağıtımın yapılandırma (`appsettings`/env) aracılığıyla ayarlayabileceği her white-label seçeneği,
**uygulama sahibi tarafından çalışma zamanında da ayarlanabilir**, **Settings → Deployment**'tan, yeniden
dağıtım olmadan. Bir sahip geçersiz kılması **yapılandırmayı yener**; onu temizlemek seçeneği dağıtımın
yapılandırılmış (veya yerleşik varsayılan) değerine döndürür.

Bu, bir white-label *dağıtımının* ürünü nasıl yapılandırdığını yansıtır — aynı düğmeler, aynı etki —
böylece bir operatör markalamayı, kapıları ve politikayı canlı ayarlayabilir ve sonucu hemen görebilir.

## Nerede yaşar

- **UI:** ayarlar iletişim kutusundaki yalnızca-sahip **Deployment** bölümü ve derin-bağlanabilir sayfa
  **`/settings/deployment`**. Seçenekler **kategori başına bir sekmeye** gruplandırılır (Markalama, Tema,
  Özellikler, Kayıt, Hesaplar, E-posta, AI, Open API, Prop firm), mobil-öncelikli, masaüstünde pencereli bir
  iletişim kutusu ve telefonlarda tam-ekran bir yüzeyle.
- **API:** `/api/whitelabel` (yalnızca-sahip, asla özellik-kapılı değil):
  - `GET /api/whitelabel` — her seçenek etkin değeriyle, kaynağıyla (`Config` / `Owner` / `Default`) ve bir
    geçersiz kılmanın ayarlanıp ayarlanmadığıyla. **Sırlar maskelenir** (değer asla döndürülmez).
  - `PUT /api/whitelabel/{key}` `{ "value": "…" }` — bir geçersiz kılma ayarla (seçenek türüne göre
    doğrulanmış). Bir **sır** üzerindeki boş değer mevcut sırrı korur.
  - `DELETE /api/whitelabel/{key}` — bir geçersiz kılmayı temizle (yapılandırmaya dön).
  - `POST /api/whitelabel/reset` — **tüm** geçersiz kılmaları temizle (dağıtımı saf yapılandırmaya döndür).

## Geçersiz kılmalar nasıl etkili olur

Sahip geçersiz kılmaları, gereken yerde şifrelenmiş `AppSetting` satırları olarak saklanır ve dekore edilmiş
bir `IOptionsMonitor<AppOptions>` tarafından bağlı `AppOptions`'ın üzerine katmanlanır. Her tüketici zaten o
monitör aracılığıyla seçenekleri okuduğundan, bir geçersiz kılma tüm uygulamada **canlı** uygulanır — tema,
sayfa başlığı, MFA kapısı, AI-sağlayıcı kapıları, broker izin-listesi, kayıt politikası, e-posta taşıma
ayarları vb. bir sonraki okumada güncellenir (tema/markalama hemen yeniden oluşturulur). Veritabanı kısa süre
kullanılamazsa, katman yapılandırılmış temele **açık başarısız olur**, böylece bir geçersiz kılma okuması
uygulamayı asla bozamaz.

**Özellik bayrakları** aynı yüzeyin parçasıdır ancak mevcut özellik-geçersiz-kılma deposu (`IFeatureGate`)
aracılığıyla kalıcılaştırılır, böylece Features sekmesi ve bağımsız özellik anahtarları asla ayrışmaz.

**Sırlar** (SMTP parolası, CAPTCHA sırrı, sağlama sırrı) durağan hâlde şifrelenir (`ISecretProtector`, amaç
`whitelabel.secret`), UI'da yalnızca-yazılır ve API tarafından asla döndürülmez.

## Devredilen seçenekler

**Paylaşılan Open API uygulaması** kimlik bilgileri ve **mesaj-türü başına hız limitleri**, **Open API**
ayarlar bölümünde yönetilir (kopya-işlem / Open API belgelerine bakın). Deployment kataloğunda *devredilmiş*
girişler olarak görünürler (burada salt-okunur, bir bağlantıyla), böylece hiçbir şey çoğaltılmaz ve senk.
garantisi onları hâlâ kapsanmış sayar.

## Her zaman senkronize (zorunlu)

Yapılandırmaya yeni bir white-label seçeneği eklemek, onu aynı işlemde sahip ayarlarında yüzeye çıkarmalıdır.
Bu, `WhiteLabelCatalogParityTests` tarafından zorunlu kılınır: her white-label seçenek-kaydı özelliği
üzerinde yansıtır ve özellik `Core/WhiteLabel/WhiteLabelCatalog`'ta kayıtlı olmadıkça (veya bir nedenle
`IntentionallyExcluded`'da açıkça listelenmedikçe) derlemeyi başarısız kılar. `CLAUDE.md`'deki 10. yönergeye bakın.

## Notlar

- **Hiç** e-posta yapılandırılmamış başlayan bir dağıtımda SMTP'yi etkinleştirmek yeniden başlatma gerektirir
  (gönderici türü başlangıçta seçilir); zaten yapılandırılmış bir göndericinin host/kimlik bilgileri canlı güncellenir.
- Seçenek **etiketleri/açıklamaları**, veri olarak gösterilen teknik yapılandırma-düğmesi tanımlayıcılarıdır;
  sekme etiketleri ve tüm etkileşimli çerçeve tamamen yerelleştirilmiştir.
