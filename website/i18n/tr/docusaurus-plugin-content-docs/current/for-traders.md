---
slug: /for-traders
title: cTrader tüccarları için cMind
description: Bir cTrader tüccarının neden cMind'ı kendi sunucusunda barındırması gerekir — yığını ve verilerini sahibi olun, bir AI destekli konsolda cBot'ları yazar, backtest, çalıştır ve izle, dizüstü bilgisayarınızda, VPS'de veya telefonunuzda.
keywords:
  - cTrader
  - algoritmik ticaret
  - kendi kendini barındıran ticaret platformu
  - cBot backtesting
  - AI ticaret botları
  - açık kaynak ticaret yazılımı
sidebar_position: 5
---

# cTrader tüccarları için cMind 📈

Zaten cTrader'da ticaret yapıyorsunuz. Zaten bir kod editörü, backtester, VPS ve üç
tarayıcı sekmesini dengeliyorsunuz. **cMind tüm bunları kendi kendini çalıştırdığınız tek koyu, klavye dostu konsolda birleştirir**
— ve açık kaynaktır, bu nedenle kenarınız, stratejileriniz veya kimlik bilgileriniz hakkında hiçbir şey
asla kutudan çıkmazız.

:::tip TL;DR
cMind'ı bir dizüstü bilgisayarda, ucuz bir VPS'de veya ev sunucusunda barındırın. cBot'ları
bir yerde yazın, backtest, çalıştırın ve izleyin, AI çekirdeği işleri yapıyor. → [5 dakikada çalıştırın](./deployment/local.md)
:::

## Barındırılan bir hizmet yerine neden kendi barındırmalı?

- **Yığını ve verilerinizi sahibi olun.** cBot'larınız, kimlik bilgileriniz, jetonlarınız ve öz sermaye geçmişiniz **sizin**
  altyapısında yaşar — üçüncü taraf yok, kilitlenme yok, "bu ürünü güneşten batıyoruz" e-postası yok.
- **Gerçekten bunu değiştirmek sizindir.** C# 14 / .NET 10, katı DDD, EF Core + PostgreSQL, bir MCP
  sunucusu — tümü açık kaynaktır ve hackable. Çatalıdı, genişletini, PR gönderi.
- **Hiçbir per-özellik ödemeli duvar.** Herhangi bir sağlayıcı için kendi AI anahtarınızı getirin; her AI özelliği açıktır.

Sunucuları kendiniz çalıştırmayı tercih etmiyorsanız? Bir barındırma şirketi sizin için yönetilen
bir cMind çalıştırabilir — bkz. [Bulut ve VPS sağlayıcıları için](./for-cloud-providers.md).

## Bir konsol, sekme dengeleme yok

- **Yazar** gerçek bir Monaco IDE'de (VS Code editörü), C# **ve** Python şablonları ve
  sandbox edilmiş `dotnet build` atılabilir konteynerler ile. → [Derleme ve backtest](./features/build-and-backtest.md)
- **Backtest** bir düğüm filosu arasında ve öz sermaye eğrilerinin canlı akışını izle.
- **Çalıştır** stratejiler canlı ve **izle** bunları bir panodan. → [Pano](./features/dashboard.md)
- **Kopya** bir ana hesaptan birçok hesaba brokerler ve cTrader ID'leri arasında, düşen bağlantılar
  ve dönen jetonları dayanıklı uzlaştırma ile. → [Kopya ticareti](./features/copy-trading.md)

## İşleri yapan AI, küçük konuşmaya değil

Kendi API anahtarınızı getirin (desteklenen herhangi bir sağlayıcı — bulut veya yerel bir model) ve
basit İngiltere → kendi kendini onarma döngüsü, parametre tuning, backtest post-mortem'ler ve
kötü davranan bir botu otomatik olarak durdturabilen bir risk koruması olan gerçek derleme cBot'unu alın.
→ [AI çekirdeği ile tanışın](./features/ai.md)

## Kurumsal sınıf araçlar, bir kişi için

Bir masanın ödediği aynı rigor, kendi kutunuzda:

- [Backtest bütünlüğü](./features/backtest-integrity.md) · [Pozisyon boyutlandırma](./features/position-sizing.md)
- [Strateji sağlığı](./features/strategy-health.md) · [Rejim laboratuvarı](./features/regime-lab.md)
- [Yürütme TCA](./features/execution-tca.md) · [Ticaret dergisi](./features/trading-journal.md)
- [Aracı Studio](./features/agent-studio.md) · [Karşıt konumlandırma](./features/contrarian-positioning.md)

## Sizin nerede olduğunuzda çalışır

`docker compose up` ile dizüstü bilgisayarınızda başlayın, hazır olduğunuzda ucuz bir VPS veya ev sunucusuna
geçin ve telefonunuzdan botlarınızı kontrol edin — cMind yüklü, mobil-ilk
[PWA](./features/pwa.md) dir. → [Yerel olarak çalıştırın](./deployment/local.md)

AI istemcinizin bunu sürmesini istiyorsanız? İç [MCP sunucusu](./features/mcp.md) var.

## Bunu daha iyi hale getirmeye yardımcı olun

cMind açık kaynaktır ve MIT lisanslıdır — yol haritası topluluk tarafından şekillendirilmiştir:

- Sorunları ve özellik isteklerini açın ve önem verdiğinize oy verin.
- cBot şablonları, AI sağlayıcı adaptörleri veya UI çevirilerini ekleyin.
- PR gönderin — üç test katmanı (birim + entegrasyon + E2E) ve katı DDD bar yüksek tutarlar ve
  [Katkılama rehberi](./contributing.md) sizi yol gösterir.

Hazır? → [Girişi okuyun](./intro.md) sonra [yerel olarak çalıştırın](./deployment/local.md).
