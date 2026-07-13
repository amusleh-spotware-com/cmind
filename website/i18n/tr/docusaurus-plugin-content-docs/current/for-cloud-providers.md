---
slug: /for-cloud-providers
title: Bulut & VPS sağlayıcıları için cMind
description: Bir bulut veya VPS sağlayıcısının neden yönetilen cMind barındırması sunması gerektiği — algo tüccarları, broker'ları ve prop firmalar için hazır bir, farklılaştırılmış ürün ve işlem, beyaz etiketli yeniden satış ve yönetilen AI'yi para kazanmanın açık yolları.
keywords:
  - yönetilen barındırma
  - VPS sağlayıcısı
  - bulut sağlayıcısı
  - ticaret platformu barındırması
  - beyaz etiketli yeniden satıcı
  - yönetilen AI barındırması
sidebar_position: 7
---

# Bulut & VPS sağlayıcıları için cMind 🖥️

Zaten işlem kaynakları kiralar mısınız. cMind, o işlemi çevresine sarabilir açık kaynak, hazır bir üründür: **yönetilen cMind barındırması** sunun ve yüksek değerli, yapışkan, işlem gücüne aç iş yükü kazanın — algoritmik tüccarlar, broker'lar, prop firmalar ve işlem toplulukları, platformun ops ekibi olunması gereksiniminden çalışmasını isteyenler.

:::tip TL;DR
Durumsuz katmanı + Postgres + bir düğüm filosu çalıştırın; müşterilere markalı bir URL verin. Abonelik, işlem, beyaz etiket ve AI'yi para ile değiştirin. → [Buluta dağıtın](./deployment/cloud.md)
:::

## Neden yönetilen cMind sunarsınız

- **Derleyme maliyeti yok.** Açık kaynak, MIT lisanslı ve zaten belgelenmiş, test edilmiş ve konteynerleştirilmiş. Paketleri ve işletiyorsunüz — onu derlemiyorsunüz.
- **Kazançlı bir niş için farklılaştırılmış bir ürün.** Algo ticareti işlem açısından ağır: backtestler ve canlı düğümler CPU'yu yakar, bu *faturalandırılabilir kullanımdır* zaten satıyorsunuz.
- **Yapışkan müşteriler.** Platformun içinde stratejiler oluşturup çalıştıran tüccarlar sıradan kaybı almaz.
- **Bir uyarıyı bir upsell'e dönüştürür.** cMind tasarımda kendi barındırılan — "ops ekibi olmak istemeyen" müşteriler için *siz* cevapsınız.

## cMind yönetimini sizden kim satın alır

- **Bireysel quant'lar & tüccarlar** barındırılmasını isteyenler. → [Tüccarlar için](./for-traders.md)
- **cTrader broker'ları** müşterileri için beyaz etiketli çalıştıranlar. → [Broker'lar için](./for-brokers.md)
- **Prop firmalar & kopyalama ticareti işletmeleri** markalı, denetlenebilir altyapı gerektiriyorlar.

## "Yönetilen cMind" çalıştırmak ne anlama gelir

Üç katmanı işletirsiniz; müşteri markalı bir web URL'si alır:

| Katman | Ne olduğu | Nerede çalışır |
|---|---|---|
| Durumsuz (Web + MCP) | Uygulama + API + MCP sunucusu | Herhangi bir konteyner platformu, otomatik ölçeklenebilir |
| Veritabanı | PostgreSQL | Yönetilen Postgres (RDS / Esnek Sunucu / kendi'siniz) |
| Düğüm filosu | cTrader konteynerleri oluşturur ve çalıştırır | **VM'ler veya Kubernetes — ayrıcalıklı Docker ihtiyaç** |

:::warning Başlangıçta kapsam dışına çıkılacak bir şey
Düğüm aracıları cTrader konteynerleri oluşturur ve çalıştırır, bu nedenle **ayrıcalıklı Docker** gerekir. Bu sunucusuz konteyner çalışma zamanlarını (Azure Container Apps, AWS Fargate) *aracılar* için dışlar — bunları [Kubernetes](./deployment/kubernetes.md), bir VM veya EC2 üzerinde çalıştırın. Durumsuz katman her yerde çalışır.
:::

Gerçek, kopyala-yapıştır dağıtım kılavuzları bunu somutlaştırır: [bulut genel bakışı](./deployment/cloud.md) · [Azure](./deployment/cloud-azure.md) · [AWS](./deployment/cloud-aws.md) · [Kubernetes](./deployment/kubernetes.md) · [Ölçekleme](./deployment/scaling.md).

## Bunu nasıl para ile değiştirirsiniz

- **Yönetilen barındırma aboneliği.** Aylık Başlangıç / Ekip / İşletme planları düğüm filosu ve backtest eşzamanlılığı tarafından boyutlandırılır.
- **Kullanım & işlem ölçümü.** Backtest saatlerini, canlı düğüm saatlerini ve depolamayı faturalandırın — zaten çalıştırdığınız konteyner filosu tarafından doğal olarak ölçülür.
- **Beyaz etiketli yeniden satıcı katmanları.** Tam bir rebrand için (logo, renkler, PWA, `ShowSiteLink=false`) ve [özellik geçiş anahtarları](./features/feature-toggles.md) aracılığıyla premium yetenekleri etkinleştirmek için daha fazla ücret alın. → [Beyaz etiketli](./features/white-label.md)
- **Yönetilen AI.** Varsayılan bir AI sağlayıcı anahtarını paketleyin, böylece her müşteri kullanıcısı kuruluma ihtiyaç duymadan AI alır ve kullanımı işaretleyin — veya kendi anahtarını getir seçeneğini sunun. → [AI özelliği](./features/ai.md)
- **Prop-firm & kopyalama ticareti gelir paylaşımı.** Zorlukları ve performans ücretlerini çalıştıran firmalar barındırın ve platform payını alın. → [Prop-firm](./features/prop-firm.md) · [Performans ücretleri](./features/copy-performance-fees.md) · [Sağlayıcı pazarı](./features/copy-provider-marketplace.md)
- **Kurulum, onboarding & SLA.** Profesyonel hizmetleri ve premium desteği ekleyin.

## Çok kiracılı desenler

- **Dağıtım başına kiracı (önerilen).** Müşteri başına bir markalı örnek — güçlü yalıtım, kiracı başına markalama ve veritabanı, kiracı başına farklı bir düğüm birleşim token'ı. Markalama `IOptionsMonitor`'dan okunur, bu nedenle her örnek kendi kimliğini taşır. → [Çok kiracılı markalama](./white-label-for-business.md#multi-tenant-per-customer-branding) · [Düğüm keşfi](./operations/node-discovery.md)
- **Paylaşılan kontrol düzlemi (gelişmiş).** Kendi sağlama katmanınızdan birçok örneği yönlendirin, markalama ve özellikleri kiracı başına programlı olarak tohum.

## Faturalama için kullanımı ölçün

Sahip/yönetici-yalnız **`GET /api/usage`** uç noktası, bir sağlayıcı tarafından anket yapıp faturalandırabilecek salt okunur bir özeti döndürür — yeni bir etki alanı veya ısrarı olmadan, mevcut durumu yansıtır:

```json
{
  "users": { "total": 42 },
  "nodes": { "total": 6, "online": 5 },
  "instances": { "total": 1280, "backtestsRunning": 3, "runsRunning": 11 },
  "cbots": { "total": 210 },
  "tradingAccounts": { "total": 88 }
}
```

Koltuk temelli, filo temelli veya iş yükü temelli fiyatlandırmayı yönlendirmek için kiracı dağıtımı başına anket yapın. [Günlüğe kaydetme ve gözlemlenebilirlik](./operations/logging.md) ile daha ince işlem ölçümü için eşleştirin.

## Marjları öngörülebilir tutun

Talep için düğümleri ölçekleyin, Postgres katmanlarını paylaşın ve durumsuz katmanı otomatik ölçekleyin. İhtiyaç duyduğunuz operasyonel yüzeyler zaten var:

- [Ölçekleme & kendi kendini iyileştirme](./deployment/scaling.md)
- [Günlüğe kaydetme & gözlemlenebilirlik](./operations/logging.md)
- [Yedekleme & kurtarma](./operations/backup-recovery.md)

## Başlamak için

1. [Bulut kılavuzlarından](./deployment/cloud.md) bir referans dağıtım kurun.
2. Kiracı başına şablon (markalama + birleşim token'ı + DB) ve faturalamayı işlem kullanımına bağlayın.
3. Listeleyin — şimdi satmak için yönetilen bir algo-ticaret platformunuz var.

## Geri katkıda bulunun

cMind'i ölçekte çalıştıran sağlayıcılar keskin kenarları önce vurur. Operasyonel düzeltmeleri ve IaC iyileştirmelerinizi yukarı akışa yollama filo'yu ucuz tutmaya devam eder — [Katkı kılavuzu](./contributing.md) ile başlayın.
