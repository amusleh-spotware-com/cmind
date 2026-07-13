---
slug: /for-traders
title: cTrader tüccarları için cMind
description: Bir cTrader tüccarının neden cMind'i kendi barındırması gerektiği — yığın ve verilerinize sahip olun, bir AI destekli konsolda cBot'ları yazın, test edin, çalıştırın ve izleyin, dizüstü, VPS veya telefonda.
keywords:
  - cTrader
  - algoritmik ticaret
  - kendi barındırılan ticaret platformu
  - cBot backtesting
  - AI ticaret botları
  - açık kaynak ticaret yazılımı
sidebar_position: 5
---

# cTrader tüccarları için cMind 📈

Zaten cTrader'da ticaret yapıyorsunuz. Zaten kod editörü, backtester, VPS ve üç tarayıcı sekmesi ile uğraşıyorsunuz. **cMind, tüm bunları, kendiniz çalıştırdığınız bir koyu, tuş tahtası dostu konsolda birleştirir** — ve açık kaynak, bu nedenle avantajınız, stratejileriniz veya kimlik bilgileriniz hakkında hiçbir şey hiçbir zaman kutudan çıkmaz.

:::tip TL;DR
cMind'i dizüstü, ucuz VPS'de veya ev sunucusunda barındırın. Bir yerde cBot'ları yazın, test edin, çalıştırın ve izleyin, AI çekirdeği işleri yapıyor. → [5 dakikada çalıştırın](./deployment/local.md)
:::

## Barındırılan hizmet yerine neden kendi barındırsınız?

- **Yığın ve verilerinize sahip olun.** cBot'larınız, kimlik bilgileriniz, token'larınız ve özkaynaklar tarihiniz **sizin** altyapınızda yaşar — üçüncü taraf yok, kilitlenme yok, "bu ürünü güneşe batırıyoruz" e-postası yok.
- **Değiştirmek için gerçekten sizindir.** C# 14 / .NET 10, kesin DDD, EF Core + PostgreSQL, bir MCP sunucusu — tüm açık kaynak ve hacklenebilir. Çatala ayırın, genişletin, bir PR gönderin.
- **Özellik başına duvar yok.** Herhangi bir sağlayıcı için kendi AI anahtarınızı getirin; her AI özelliği açık.

Sunucuları kendiniz çalıştırmamayı tercih ediyor musunuz? Bir barındırma şirketi, sizin için yönetilen cMind çalıştırabilir — [Bulut & VPS sağlayıcıları için](./for-cloud-providers.md) bölümüne bakın.

## Bir konsol, sekme sallanması yok

- **Yaz** gerçek Monaco IDE'de (VS Code editörü), C# **ve** Python şablonları ve atılabilir konteynerlerde korumalı `dotnet build` ile. → [Derle & backtest](./features/build-and-backtest.md)
- **Test** düğümleri filosu arasında ve özkaynaklar eğrilerinin canlı akması izleyin.
- **Çalıştır** stratejileri canlı ve **izle** onları bir panosundan. → [Pano](./features/dashboard.md)
- **Kopyala** bir ana hesabı, broker'lar ve cTrader ID'leri arasında birçok hesaba, düşen bağlantılar ve dönen token'ları atlatır. → [Kopyalama ticareti](./features/copy-trading.md)

## AI ki işleri yapar, küçük konuşma değil

Kendi API anahtarınızı getirin (desteklenen herhangi bir sağlayıcı — bulut veya yerel model) ve düz İngilizce → kendini onarma döngüsü olan gerçek derlenme cBot, parametre tuning, backtest otopsileri ve kötü davranış gösteren bir botu otomatik olarak durdurabilecek bir risk koruma alın. → [AI çekirdeğini tanıyın](./features/ai.md)

## Kurumsal sınıf araçlar, bir kişi için

Masanın ödediği aynı katılık, kendi kutunuzda:

- [Backtest bütünlüğü](./features/backtest-integrity.md) · [Pozisyon boyutlandırması](./features/position-sizing.md)
- [Strateji sağlığı](./features/strategy-health.md) · [Rejim laboratuvarı](./features/regime-lab.md)
- [Yürütme TCA](./features/execution-tca.md) · [Ticaret günlüğü](./features/trading-journal.md)
- [Ajan Stüdyosu](./features/agent-studio.md) · [Tersine pozisyon](./features/contrarian-positioning.md)

## Sizin yaptığınız yerde çalışır

Dizüstü ile `docker compose up` ile başlayın, hazır olduğunuzda ucuz VPS'ye veya ev sunucusuna geçin ve telefonunuzdan bot'larınızı kontrol edin — cMind, yüklenebilir, mobil öncelikli [PWA](./features/pwa.md). → [Yerel olarak çalıştırın](./deployment/local.md)

AI istemcinizin onu yönetmesini istiyorsunuz mu? Yerleşik bir [MCP sunucusu](./features/mcp.md) var.

## Bunu daha iyi hale getirmesine yardım edin

cMind açık kaynak ve MIT lisanslı — yol haritası topluluk şeklinde:

- Sorunları ve özellik taleplerini açın ve önem verdiğiniz şeylere oy verin.
- cBot şablonları, AI sağlayıcı adaptörleri veya UI çevirilerini ekleyin.
- PR'ları gönderin — üç test katmanı (birim + entegrasyon + E2E) ve kesin DDD çubuğu yüksek tutar ve [Katkı kılavuzu](./contributing.md) sizi yol gösterir.

Hazır? → [Girişi oku](./intro.md) sonra [yerel olarak çalıştır](./deployment/local.md).
