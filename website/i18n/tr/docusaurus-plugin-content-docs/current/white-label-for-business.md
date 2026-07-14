---
slug: /white-label-for-business
title: İşletme için beyaz etiketli
description: cMind'i kendi markalı ürün olarak gönderin — prop firmlar, broker'lar ve kopyalama ticareti işletmeleri için. Yapılandırma yoluyla her yüzeyi yeniden etiketleyin, kod değişikliği yok.
sidebar_position: 4
---

# İşletmesi için Beyaz Etiketli cMind 🏢

Prop firma, broker masası veya kopyalama ticareti hizmeti yönetiyor musunuz? cMind günden birinden **kendi ürün olarak yeniden satılacak** şekilde oluşturuldu. Her yüzey — ad, logo, favicon, renkler, hatta yüklenebilir telefon uygulaması — markanız içinde bükülür. Müşterileriniz *sizin* şirketinizi görür. Kod değişikliği yok, fork yok, sadece yapılandırma.

:::tip[TL;DR]
`App:Branding` adınıza, renklerinize ve logonuza işaret edin. Yeniden başlatın. Bitti. Tam teknik referans [Beyaz etiketli özellik belgesi](./features/white-label.md) içinde yaşıyor.
:::

## Ne yeniden etiketleyebilirsiniz

| Yüzey | Ne değişir |
|---|---|
| **Ürün adı** | Uygulama çubuğu metni + tarayıcı sekmesi başlığı |
| **Logo & favicon** | Tarayıcı sekmesi dahil her yerde sizin işaretleriniz |
| **Renkler** | Tam palet — birincil, yüzeyler, durum renkleri — tüm UI'de *ve* uygulamanın kendi CSS'sinde tasarım token'ları aracılığıyla akar |
| **Yüklenebilir uygulama (PWA)** | Ev ekranına ekleme adı, ikon ve açılış görüntüsü markanızı kullanır |
| **Meta / SEO** | Açıklama ve destek URL'si sizindir |
| **Özel CSS** | Son yüzde 5 için kendi cilasını enjekte edin |

Her şey stok cMind kimliğine varsayılan olarak ayarlanır, bu nedenle sadece önemsediğiniz şeyi geçersiz kılarsınız.

## 60 saniyelik rebrand

Dağıtımınızda bunları ayarlayın (JSON yapılandırması veya ortam değişkenleri):

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

Ortam değişkeni formu: `App__Branding__ProductName=AcmeFX`. Renkler başlangıçta doğrulanır — kötü bir hex değeri kırık bir sayfayı oluşturmak yerine net bir iletiyle önyüklemeyi başarısız kılar. Güzel ve yüksek, tam olarak istediğiniz zaman.

## "Powered by cMind" Bağlantısı

Varsayılan olarak, pano küçük, zarif bir **"Powered by cMind"** bağlantısı gösterir ki bu ziyaretçileri bu siteye geri işaret eder. Projenin gurur duyduğumuz ve diğer tüccarların onu bulmasına yardımcı olduğu için varsayılan olarak açık — ancak bu **sizin çağrınız**.

- **Sakla** (varsayılan): pano üzerinde hafif bir kredi bağlantısı. Sana hiçbir şey maliyeti olmaz, projeye yardımcı olur.
- **Gizle**: `App__Branding__ShowSiteLink=false` ayarlayın ve tamamen kaybolur — ürün açıkça *sizin* olduğu tamamen beyaz etiketli bir dağıtım için mükemmel.

Tam olarak nerede gösterildiği için [Beyaz etiketli özellik belgesi](./features/white-label.md#powered-by-link) bölümüne bakın.

## Çok kiracılı, müşteri başına markalama

Markalama sadece dağıtım yapılandırması olduğundan, her kiracı dağıtımı kendi kimliğini taşıyabilir. Müşteri başına ayrı bir örnek çalıştırın veya kendi kontrol düzleminizden markalamayı yönlendirin — uygulama `IOptionsMonitor` konusundan onu okur, bu nedenle seçenekler değiştiğinde tema gerçek zamanlı yeniden oluşturabilir.

Bunu şu kişiler ile eşleştirin:

- **[Özellik geçiş anahtarları](./features/feature-toggles.md)** — her kiracının hangi yetenekleri göreceğine karar verin.
- **[Prop-firm kuralları](./features/prop-firm.md)** — zorluk kurallarınızı canlı özkaynaklar izleme ile uygulayın.
- **[Performans ücretleri](./features/copy-performance-fees.md)** + **[sağlayıcı pazarı](./features/copy-provider-marketplace.md)** — kopyalama ticaretini para ile değiştirin.
- **[Uyum](./features/compliance.md)** — düzenleyicininizin soracağı denetim izini tutun.

## Varlıklar & barındırma

Logonuzu/faviconunuzu Web uygulamasının `wwwroot/branding/` bölümüne bırakın (veya `LogoUrl`/`FaviconUrl` adresini herhangi bir mutlak URL'ye işaret ettirin). İstediğiniz şekilde dağıtın — [Docker](./deployment/local.md), [Kubernetes](./deployment/kubernetes.md), [Azure](./deployment/cloud-azure.md) veya [AWS](./deployment/cloud-aws.md).

Bunu sizinki yapmaya hazır? [Teknik beyaz etiketli referans →](./features/white-label.md) ile başlayın
