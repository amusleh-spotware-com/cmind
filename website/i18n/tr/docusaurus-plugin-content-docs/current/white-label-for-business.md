---
slug: /white-label-for-business
title: İşletme için beyaz etiket
description: cMind'ı kendi markalı ürün olarak gönderin — prop firmalar, brokerler ve kopya ticareti işletmeleri için. Yapılandırma aracılığıyla her yüzeyi yeniden markalaştırın, kod değişikliği yok.
sidebar_position: 4
---

# İşletmeniz için cMind'ın beyaz etiketi 🏢

Bir prop firma, broker masası veya kopya ticareti hizmetini işletiyorsunuz? cMind **günden birinden itibaren
kendi ürünü olarak satılmak üzere** inşa edildi. Her yüzey — ad, logo, favicon, renkler, hatta yüklenebilir
telefon uygulaması — markanıza büker. Müşterileriniz *sizin* şirketinizi görür. Kod değişikliği yok,
çatal yok, sadece config.

:::tip TL;DR
`App:Branding` 'i adınıza, renklerinize ve logonuza yönlendirin. Yeniden başlatın. Bitti. Tam teknik
referans [Beyaz etiket özelliği dokümanı](./features/white-label.md) 'nde yaşar.
:::

## Yeniden markalaştırabileceğiniz şey

| Yüzey | Ne değişir |
|---|---|
| **Ürün adı** | Uygulama çubuğu metni + tarayıcı sekmesi başlığı |
| **Logo ve favicon** | Tarayıcı sekmesi dahil her yerde kendi işaretleri |
| **Renkler** | Tam palet — birincil, yüzeyler, durum renkleri — tasarım jetonları aracılığıyla tüm UI'de ve uygulamanın kendi CSS'inde akar |
| **Yüklenebilir uygulama (PWA)** | Ana ekrana ekleme adı, simgesi ve sıçraması markanızı kullanır |
| **Meta / SEO** | Açıklama ve destek URL'si sizin |
| **Özel CSS** | Son %5 için kendi cilasını enjekte edin |

Her şey stok cMind kimliğine varsayılan olur, bu nedenle sadece önem verdiğiniz şeyi geçersiz kılarsınız.

## 60 saniyelik rebranding

Dağıtımınızda bunu ayarlayın (JSON yapılandırması veya ortam değişkenleri):

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "LogoUrl": "/branding/acme-logo.svg",
      "FaviconUrl": "/branding/acme.ico",
      "PrimaryColor": "#2D7FF9",
      "SecondaryColor": "#1E63C8",
      "ShowSiteLink": false
    }
  }
}
```

Ortam değişkeni formu: `App__Branding__ProductName=AcmeFX`. Renkler başlangıçta doğrulanır —
kötü bir hex değeri kırık bir sayfa yerine açık bir iletiyle açılışı başarısız kılar. Güzel ve
yüksek, tam istediğiniz zaman.

## "cMind tarafından Powered By" bağlantısı

**Varsayılan olarak**, pano, ziyaretçileri bu siteye geri gönderen küçük, sofistike bir **"cMind tarafından
Powered"** bağlantısı gösterir. Projeden gurur duyduğumuz ve diğer tüccarların onu bulmasına yardımcı
olduğu için varsayılan olarak açık — ama bu **sizin aramanız**.

- **Bunu tutun** (varsayılan): pano üzerinde hafif bir kredi bağlantısı. Size hiçbir şey kaybetmez,
  projeye yardımcı olur.
- **Gizleyin**: `App__Branding__ShowSiteLink=false` ayarlayın ve tamamen kaybolur — ürün
  açıkça *sizin* olan tamamen beyaz etiketli bir dağıtım için mükemmel.

Tam olarak nerede işlediği için [Beyaz etiket özelliği dokümanı](./features/white-label.md#powered-by-link) 'na
bakın.

## Çok kiracılı, per-müşteri markalandırması

Markalaştırma sadece dağıtım yapılandırması olduğu için, her tenant dağıtımı kendi kimliğini taşıyabilir.
Müşteri başına ayrı bir örnek çalıştırın veya kendi kontrol düzleminizdeki markalandırmayı yönlendirin —
uygulama `IOptionsMonitor` 'dan okur, bu nedenle seçenekler değiştiğinde hatta temanı canlı yeniden inşa
edebilir.

Bunu eşleştirin:

- **[Özellik geçişleri](./features/feature-toggles.md)** — her tenant'ın hangi yetenekleri göreceğine karar verin.
- **[Prop-firma kuralları](./features/prop-firm.md)** — canlı öz sermaye izleme ile zorluk kurallarını
  uygulayın.
- **[Performans ücretleri](./features/copy-performance-fees.md)** + **[sağlayıcı pazarı](./features/copy-provider-marketplace.md)**
  — kopya ticareti para haline getirin.
- **[Uyum](./features/compliance.md)** — düzenleyicinizin soracağı denetim izini tutun.

## Varlıklar ve barındırma

Logonuzu/favicon'unuzu Web uygulamasının `wwwroot/branding/` 'e bırakın (veya `LogoUrl`/`FaviconUrl` 'u
herhangi bir mutlak URL'ye yönlendirin). Sizin için uygun olan herhangi bir şekilde konuşlandırın — [Docker](./deployment/local.md),
[Kubernetes](./deployment/kubernetes.md), [Azure](./deployment/cloud-azure.md) veya
[AWS](./deployment/cloud-aws.md).

Bunu sizin yapmaya hazır? [Teknik beyaz etiket referansı ile başlayın →](./features/white-label.md)
