---
slug: /for-cloud-providers
title: Bulut ve VPS sağlayıcıları için cMind
description: Bir bulut veya VPS sağlayıcısının neden yönetilen cMind barındırma sunması gerekir — algo tüccarları, brokerler ve prop firmalar için hazır, farklılaştırılmış bir ürün ve işlem gücü, beyaz etiket yeniden satışı ve yönetilen AI'yı para haline getirmenin açık yolları.
keywords:
  - yönetilen barındırma
  - VPS sağlayıcı
  - bulut sağlayıcı
  - ticaret platformu barındırması
  - beyaz etiket satıcı
  - yönetilen AI barındırması
sidebar_position: 7
---

# Bulut ve VPS sağlayıcıları için cMind 🖥️

Zaten işlem gücü kiralıyorsunuz. cMind, o işlem gücünü etrafında sarabilileceğiniz hazır, açık kaynaklı
bir üründür: **yönetilen cMind barındırması sunun** ve yüksek değerli, yapışkan, işlem gücü açısından açlık
çeken bir iş yükü edinin — algoritma tüccarları, brokerler, prop firmalar ve platform platformu çalışan
operasyon ekibi olmadan istenen ticaret topluluğu.

:::tip TL;DR
Durumsuz katman + Postgres + bir düğüm filosu çalıştırın; müşterileri marka URL'ye teslim edin.
Aboneliği, işlem gücünü, beyaz etiketi ve AI'yı para haline getirin. → [Buluta konuşlandırın](./deployment/cloud.md)
:::

## Neden yönetilen cMind sunulsun

- **Bina maliyeti yok.** Açık kaynaktır, MIT lisanslıdır ve zaten belgelenmiş, test edilmiş ve
  konteynerleştirilmiştir. Paketini ve işletini — bunu inşa etmeyin.
- **Lücrâtif bir niş için farklılaştırılmış ürün.** Algo ticareti işlem gücü açısından açlık çeker: backtestler
  ve canlı düğümler CPU'yu yakar, ki bu zaten sattığınız *faturalandırılabilir kullanımdır*.
- **Yapışkan müşteriler.** Stratejileri platform içinde inşa eden ve çalıştıran tüccarlar rasgele asıl yapmaz.
- **Bir uyarı uyarısı için upsell'ye dönüştür.** cMind tasarımına göre kendi kendini barındırılır — "operasyon
  ekibi olmak istemeyenler" için müşteriler için *siz* cevapsınız.

## Kim sizden yönetilen cMind alır

- **Bireysel quants ve tüccarlar** barındırılan istiyorum. → [Tüccarlar için](./for-traders.md)
- **cTrader brokerler** müşterileri için beyaz etiket çalıştırılıyor. → [Brokerler için](./for-brokers.md)
- **Prop firmalar ve kopya ticareti işletmeleri** marka, denetlenebilir altyapı ihtiyacı.

## "Yönetilen cMind" ne çalıştırılacağı anlamına gelir

Üç katman çalıştırırsınız; müşteri marka web URL alır:

| Katman | Ne olduğu | Nerede çalışır |
|---|---|---|
| Durumsuz (Web + MCP) | Uygulama + API + MCP sunucusu | Herhangi bir konteyner platformu, otomatik ölçekli |
| Veri tabanı | PostgreSQL | Yönetilen Postgres (RDS / Flexible Server / sizin) |
| Düğüm filosu | Derleme ve cTrader konteynerler çalıştır | **VM'ler veya Kubernetes — ayrıcalıklı Docker gerekir** |

:::warning Başlangıçta kapsam için bir şey
Düğüm aracıları cTrader konteynerler derler ve çalıştırır, bu nedenle **ayrıcalıklı Docker** gerekir.
Bu sunucusuz konteyner çalışma zamanlarını kurallar dışı (Azure Container Apps, AWS Fargate)
*aracılar için* — [Kubernetes](./deployment/kubernetes.md), VM veya EC2'de çalıştırın. Durumsuz katman
herhangi bir yerde çalışır.
:::

Gerçek, kopyala-yapıştır dağıtım rehberleri bunu somut kılar: [bulut genel bakış](./deployment/cloud.md) ·
[Azure](./deployment/cloud-azure.md) · [AWS](./deployment/cloud-aws.md) ·
[Kubernetes](./deployment/kubernetes.md) · [Ölçekleme](./deployment/scaling.md).

## Nasıl para haline getirirsiniz

- **Yönetilen barındırma aboneliği.** Aylık Başlangıç / Takım / İşletme planları düğüm filosu ve
  backtest eşzamanlılığı tarafından boyutlandırılmış.
- **Kullanım ve işlem gücü ölçümü.** Backtest-saatleri, canlı-düğüm-saatleri ve depolama fatura — doğal olarak
  zaten çalıştırdığınız konteyner filosu tarafından ölçümlü.
- **Beyaz etiket satıcı katmanları.** Tam bir rebranding (logo, renkler, PWA, `ShowSiteLink=false`)
  ve [özellik geçişleri](./features/feature-toggles.md) aracılığıyla premium yetenekleri etkinleştirmek için
  daha fazla ücretlendir. → [Beyaz etiket](./features/white-label.md)
- **Yönetilen AI.** Her müşterinin kullanıcılarının kurulum olmadan AI alması için varsayılan bir AI
  sağlayıcı anahtarı paket haline getirin ve kullanımı işaretleyin — veya kendi anahtarınızı getirin. →
  [AI özelliği](./features/ai.md)
- **Prop-firma ve kopya ticareti gelir payı.** Zorluk ve performans ücretleri çalıştıran firmalar barındırın
  ve platform kesintisi alın. → [Prop-firma](./features/prop-firm.md) ·
  [Performans ücretleri](./features/copy-performance-fees.md) ·
  [Sağlayıcı pazarı](./features/copy-provider-marketplace.md)
- **Kurulum, Onboarding ve SLA.** Profesyonel hizmetleri ve premium desteği ekleyin.

## Çok kiracılı desenler

- **Dağıtım-per-tenant (önerilen).** Müşteri başına bir marka örneği — güçlü izolasyon,
  per-tenant markalandırma ve veri tabanı, per tenant farklı bir düğüm birleştirme jetonunu. Marka
  `IOptionsMonitor` okunur, bu nedenle her örnek kendi kimliğini taşır.
  → [Çok kiracılı markası](./white-label-for-business.md#multi-tenant-per-customer-branding) ·
  [Düğüm keşfi](./operations/node-discovery.md)
- **Paylaşılan kontrol düzlemi (gelişmiş).** Kendi sağlama katmanınızdan birçok örneği çalıştırın,
  programlı olarak per-tenant markalama ve özellikleri tohumla.

## Faturalandırma için kullanım ölçümü

Sadece sahip/yönetici **`GET /api/usage`** uç noktası sağlayıcının yoklaması ve fatura alması için
— herhangi bir yeni etki alanı veya kalıcılık olmadan, mevcut durumu projeleri yapması:

```json
{
  "users": { "total": 42 },
  "nodes": { "total": 6, "online": 5 },
  "instances": { "total": 1280, "backtestsRunning": 3, "runsRunning": 11 },
  "cbots": { "total": 210 },
  "tradingAccounts": { "total": 88 }
}
```

Oturum tabanlı, filo tabanlı veya iş yükü tabanlı fiyatlandırma sürmeye her tenant dağıtımını yoklayın.
İnce işlem gücü ölçümü için [günlükleme ve gözlemlenebilirlik](./operations/logging.md) ile
eşleştirin.

## Marjları tahmin edilebilir tutmak

Düğümleri talebe ölçeklendir, Postgres katmanlarını paylaş ve durumsuz katmanı otomatik ölçekle.
İhtiyaç duyduğunuz operasyonel yüzeyler zaten var:

- [Ölçekleme ve kendi kendini iyileştirme](./deployment/scaling.md)
- [Günlükleme ve gözlemlenebilirlik](./operations/logging.md)
- [Yedekleme ve kurtarma](./operations/backup-recovery.md)

## Başlayın

1. [Bulut rehberleri](./deployment/cloud.md) 'den bir referans dağıtımını ayarlayın.
2. Her tenant (markalama + birleştirme jetonunu + DB) şablonu ve faturalandırmayı işlem gücü kullanımına
   bağlayın.
3. Listeleyin — artık satmak için yönetilen bir algo-ticaret platformunuz var.

## Geri katkıda bulunun

Ölçekte cMind çalıştıran sağlayıcılar keskin kenarları ilk kez tutturur. Operasyonel düzeltmeleri ve
IaC iyileştirmelerini hava koruması yaparak filo işletimini ucuz tutmanızı sağlar — [Katkılama
rehberi](./contributing.md) ile başlayın.
