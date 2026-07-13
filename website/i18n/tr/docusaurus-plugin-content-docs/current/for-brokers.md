---
slug: /for-brokers
title: cTrader brokerleri için cMind
description: Bir cTrader brokerinin kendi müşterileri için beyaz etiket cMind neden çalıştırması gerekir — tüccarlarınıza AI, kopya ticareti ve prop-firma zorlukları kendi markanız altında verin, hesapları brokerliğinize sınırlayın ve rakipleri üzerinden bir avantaj kazanın.
keywords:
  - cTrader broker
  - beyaz etiket ticaret platformu
  - broker teknolojisi
  - brokerler için kopya ticareti
  - AI ticaret araçları
  - prop firma yazılımı
sidebar_position: 6
---

# cTrader brokerleri için cMind 🏦

Bir cTrader brokerliği işletiyorsunuz. Müşterileriniz zaten ticaret yapabilir — ama diğer her brokerinin
müşterileri de yapabilir. **cMind, tüccarlarınıza tam bir AI destekli ticaret operasyon platformu verir,
kendi markanız olarak markalaşmış**, böylece stratejileri *sizin* ekosistemininizin içinde inşa ediyor,
backtest, çalıştır, kopya ve izle, bunun yerine üçüncü taraf bir aracı çalışmasına sürüklenirler.
Bu daha yapışkan müşteriler, daha fazla hacim ve terminal dışında hiçbir şey sunmayan brokerler üzerinde gerçek bir avantaj.

:::tip TL;DR
Müşterileriniz için beyaz etiket cMind çalıştırın. Hesapları **sizin** brokerliğinize sınırlayın, AI ve
kopya ticareti aç ve kendi markanız altında gönder. → [İşletmeler için beyaz etiket](./white-label-for-business.md)
:::

## Diğer brokerler üzerinde elde ettiğiniz avantaj

- **Araçları farklılaştırın, sadece spreadleri değil.** Müşterilere AI cBot oluşturma, yönetilen bir
  kümede backtesting, kopya ticareti ve prop-firma zorlukları verin — çoğu brokerinin sunmadığı
  yetenekler.
- **Müşterileri ekosistemininizde tutun.** Tüccarlar stratejileri marka platformunuz içinde inşa ettiğinde
  ve çalıştırdığında, onlar kalırlar. Tutunma tüm oyunu oynar.
- **Kendi markanız altında, kendi etki alanınızda.** Ad, logo, renkler, favicon, hatta yüklenebilir telefon
  uygulaması — tümü sizin. Kimse "cMind" görmez. → [Beyaz etiket özelliği](./features/white-label.md)

## Yalnızca hesaplarınızı sunun (broker allowlist)

Müşterileriniz için bir beyaz etiket çalıştırıyor mu? Kullanıcıların hangi brokerların ticaret hesaplarını
ekleyebileceğini sınırlayın böylece dağıtımınız sadece kitabınızı sunar:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Your Brokerage Name"]
    }
  }
}
```

İzin listesi ayarlandığında, cMind bir kullanıcının eklemeye çalıştığı her hesabı kontrol eder — hem
cTrader Open API aracılığıyla hem de manuel cID girişi aracılığıyla (hesabın gerçek broker adı okunarak
doğrulanır) — ve listede olmayan herhangi bir hesabı reddeder. Boş bırakın ve her broker izin verilir
(varsayılan). Tam mekanizma için [Beyaz etiket özelliği dokümanı](./features/white-label.md#broker-allowlist) bölümüne bakın.

## Tüm kullanıcılarınız için bir Open API uygulaması gönderin

Per-kullanıcı güçlüğü atlayın: **bir cTrader Open API uygulaması** sağlayın ve her müşteri
hesaplarını aracılığıyla yetkilendirir — müşteri hiçbir zaman kendi kaydını yapma yapmaz. Bir tek
yeniden yönlendirme URL'si kaydedin, kimlik bilgilerini yapılandırmaya veya sahip ayarlarına bırakın ve
paylaşılan mod herkes için açılır. cTrader mesaj sınırını müzakere ettiniz? **Per-mesaj-tipi istemci
hız sınırını** (veya tempyi devre dışı bırakın) ayarlayın. → [Paylaşılan Open API uygulaması ve hız
sınırları](./features/open-api-shared-app.md)

## Para kazanmak için yeni yollar

- **AI, müşteriler için sıfır sürtünme ile.** Dağıtım düzeyinde varsayılan bir AI sağlayıcı anahtarı
  sağlayın ve her müşteri AI özelliklerini anında alır — başka hiçbir yere kayıt olmaz. Bunu fiyatında
  işaretleyin veya premium katmanlara paket haline getirin. Müşteriler kendi anahtarlarını getirebilir.
  → [AI özelliği](./features/ai.md)
- **Prop-firma zorlukları.** Canlı öz sermaye izleme ve uygulanmış kurallar ile finanse edilen tüccar
  zorlukları çalıştırın ve girdiler için ücret alın. → [Prop-firma kuralları](./features/prop-firm.md)
- **Kopya ticaret işi.** Performans ücretleri ve bir sağlayıcı pazarı, kopya ticareti gelire dönüştürür.
  → [Performans ücretleri](./features/copy-performance-fees.md) ·
  [Sağlayıcı pazarı](./features/copy-provider-marketplace.md)
- **Özellik katmanları.** Her müşteri segmentinin hangi yetenekleri göreceğine karar verin
  [özellik geçişleri](./features/feature-toggles.md) ile.

## Düzenlenmiş, denetlenebilir, çok kiracılı

- **[Uyum](./features/compliance.md)** günlükleri düzenleyicinizin sorması için denetim izi verir.
- **[İki faktörlü kimlik doğrulama](./features/two-factor-auth.md)** dağıtım başına zorunlu hale
  getirilebilir.
- **Per-müşteri markası** — segmentler başına ayrı bir marka örneği çalıştırın, kendi kontrol
  saçınızdan yönetilir. → [Çok kiracılı per-müşteri markası](./white-label-for-business.md#multi-tenant-per-customer-branding)

## Nasıl başlanır

1. 60 saniyelik rebranding için [İşletmeler için beyaz etiket](./white-label-for-business.md) 'i okuyun.
2. `App:Accounts:AllowedBrokers` 'i brokerliğinize ayarlayın ve [özellik setini](./features/feature-toggles.md) seçin.
3. [Konuşlandırın](./deployment/cloud.md) — Docker, Kubernetes, Azure veya AWS.

Altyapıyı kendiniz çalıştırmak istemiyor musunuz? Bir barındırma sağlayıcısı sizin için yönetilen bir
cMind işletebilir — [Bulut ve VPS sağlayıcıları için](./for-cloud-providers.md) bölümüne yönlendirin.

## Yol haritasını şekillendir

cMind açık kaynaktır. Üzerinde inşa eden brokerler nereye gittiğine daha büyük bir söyişe sahibi olur — sizin
ihtiyaç duyduğunuz entegrasyonları ve kontrolleri isteyin ve [Katkılama rehberi](./contributing.md) aracılığıyla
geri katkıda bulunun.
