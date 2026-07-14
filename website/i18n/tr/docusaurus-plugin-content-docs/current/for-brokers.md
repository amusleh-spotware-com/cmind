---
slug: /for-brokers
title: cTrader broker'ları için cMind
description: Bir cTrader broker'ının kendi müşterileri için neden beyaz etiketli cMind'i çalıştırması gerektiği — tüccarlarına AI, kopyalama ticareti ve prop-firm zorlukları kendi markanız altında verin, hesapları borsanıza kısıtlayın ve rakiplere karşı bir avantaj kazanın.
keywords:
  - cTrader broker
  - beyaz etiketli ticaret platformu
  - broker teknolojisi
  - broker'lar için kopyalama ticareti
  - AI ticaret araçları
  - prop firm yazılımı
sidebar_position: 6
---

# cTrader broker'ları için cMind 🏦

Bir cTrader borsası yönetiyorsunuz. Müşterileriniz zaten ticaret yapabilir — ancak diğer broker'ların müşterileri de yapabilir. **cMind, tüccarlarınıza tam bir AI destekli ticaret operasyonları platformu vererek kendi markanızla etiketlenir**, böylece bir üçüncü taraf aracına sürüklenme yerine *sizin* ekosisteminizin içinde stratejiler oluşturur, test eder, çalıştırır, kopyalayarak izlerler. Bu, yapışkan müşteriler, daha fazla hacim ve yalnızca terminal sunan broker'lardan daha gerçek bir avantaj.

:::tip[TL;DR]
Müşterileriniz için beyaz etiketli cMind çalıştırın. Hesapları **sizin** borsanıza kısıtlayın, AI ve kopyalama ticaretini açın ve markanız altında gönderin. → [İşletme için beyaz etiketli](./white-label-for-business.md)
:::

## Diğer broker'lara karşı kazandığınız avantaj

- **Araçlarla farklılaşın, sadece spreadler değil.** Müşterilere AI cBot üretimi, yönetilen küme üzerinde backtesting, kopyalama ticareti ve prop-firm zorlukları verin — çoğu broker'un basitçe sunmadığı yetenekler.
- **Müşterileri ekosisteminizde tutun.** Tüccarlar stratejileri markalı platformunuzun içinde oluşturduğunda ve çalıştırdığında, kalırlar. Tutuş oyunun bütünüdür.
- **Markanız altında, kendi alanınızda.** Ad, logo, renkler, favicon, hatta yüklenebilir telefon uygulaması — hepsi sizindir. Kimse "cMind" görmez. → [Beyaz etiketli özellik](./features/white-label.md)

## Sadece hesaplarınızı sunun (broker izin listesi)

*Sizin* müşterileriniz için beyaz etiketli çalıştırmak mı? Kullanıcıların hangi broker'ların ticaret hesaplarını ekleyebileceğini kısıtlayın, böylece dağıtımınız sadece kitabınıza hizmet verir:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Your Brokerage Name"]
    }
  }
}
```

İzin listesi ayarlandığında, cMind, bir kullanıcı eklemeye çalışan her hesabını kontrol eder — hem cTrader Open API aracılığıyla hem de manuel cID oturumu aracılığıyla (hesabın gerçek broker adını okuarak doğrulanır) — ve listenizde olmayan herhangi bir hesabı reddeder. Boş bırakın ve her broker izin verilir (varsayılan). Tüm mekanikler için [Beyaz etiketli özellik belgesi](./features/white-label.md#broker-allowlist) bölümüne bakın.

## Tüm kullanıcılarınız için bir Open API uygulaması gönderin

Kullanıcı başına güçlükleri atlayın: **bir cTrader Open API uygulaması** sağlayın ve her istemci hesaplarını — hiçbir istemci asla kendi'ni kaydolmaz. Tek bir yeniden yönlendirme URL'sini kaydettirin, kimlik bilgilerini yapılandırmaya veya sahip ayarlarına bırakın ve paylaşılan mod herkes için açılır. Daha yüksek cTrader ileti sınırını müzakere etmiş misiniz? **İleti türü başına istemci hız sınırlarını** ayarlayın (veya hızı devre dışı bırakın). → [Paylaşılan Open API uygulaması & hız sınırları](./features/open-api-shared-app.md)

## Para kazanmanın yeni yolları

- **AI, müşteriler için sıfır sürtünme ile.** Dağıtım düzeyinde varsayılan bir AI sağlayıcı anahtarı sağlayın ve her istemci AI özelliklerini hemen alır — başka yerlerde kaydolma yok. İşaretleyin veya premium katmanlara paketleyin. İstemciler yine de kendi anahtarlarını getirebilir. → [AI özelliği](./features/ai.md)
- **Prop-firm zorlukları.** Canlı özkaynaklar izleme ve uygulanmış kurallarla finanse edilen tüccar zorlukları çalıştırın ve girişler için ücret alın. → [Prop-firm kuralları](./features/prop-firm.md)
- **Kopyalama ticareti işi.** Performans ücretleri ve bir sağlayıcı pazarı kopyalama ticaretini gelir kaynağına dönüştürür. → [Performans ücretleri](./features/copy-performance-fees.md) · [Sağlayıcı pazarı](./features/copy-provider-marketplace.md)
- **Özellik katmanları.** [Özellik geçiş anahtarları](./features/feature-toggles.md) ile her istemci segmentinin hangi yetenekleri göreceğine karar verin.

## Düzenlenmiş, denetlenebilir, çok kiracılı

- **[Uyum](./features/compliance.md)** günlükleri, düzenleyicinin soracağı denetim izini verir.
- **[İki faktörlü kimlik doğrulama](./features/two-factor-auth.md)** dağıtım başına zorunlu hale getirilebilir.
- **İstemci başına markalama** — segment başına ayrı markalı bir örnek çalıştırın, kendi kontrol düzleminizden yönetilir. → [Çok kiracılı markalama](./white-label-for-business.md#multi-tenant-per-customer-branding)

## Başlamak için nasıl

1. 60 saniyelik rebrand için [İşletme için beyaz etiketli](./white-label-for-business.md) okuyun.
2. `App:Accounts:AllowedBrokers` değerini borsanıza ayarlayın ve [özellik setinizi](./features/feature-toggles.md) seçin.
3. [Dağıtın](./deployment/cloud.md) — Docker, Kubernetes, Azure veya AWS.

Altyapıyı kendiniz çalıştırmak istemiyorsunuz mu? Bir barındırma sağlayıcısı, sizin için yönetilen cMind'i işletebilir — onları [Bulut & VPS sağlayıcıları için](./for-cloud-providers.md) yönlendirin.

## Yol haritasını şekillendirin

cMind açık kaynak. Üzerine inşa eden Broker'lar, nereye gittiğine karşı abartılı bir söz alır — ihtiyaç duyduğunuz entegrasyonları ve kontrolleri talep edin ve onları [Katkı kılavuzu](./contributing.md) aracılığıyla geri katkıda bulunun.
