---
title: Mimari Karar Kayıtları
description: cMind arkasındaki belirgin olmayan tasarım kararları — bağlam, karar ve sonuçlar — kodu okuyarak çıkaramayacağınız.
---

# Mimari Karar Kayıtları

Bunlar **koddan çıkaramayacağınız** tasarım kararlarını kaydeder — ticaretler, alınan yollar ve neden. Her biri kısa: *Bağlam → Karar → Sonuçlar*. Yeni yapısal karar → buraya bir ADR ekleyin (sonraki numara) böylece sonraki mühendis (insan veya AI) sadece sonucu değil, akıl yürütmeyi devralır.

| # | Karar |
|---|---|
| [0001](./0001-strict-ddd-pure-core.md) | Saf `Core` ile kesin DDD |
| [0002](./0002-tph-instance-replaces-entity.md) | Örnek durum TPH'dir; bir geçiş varlığı değiştirir |
| [0003](./0003-external-nodes-http-jwt.md) | cTrader CLI düğümleri HTTP + JWT, SSH/kabuk yok |
| [0004](./0004-cbotbuilder-on-web-host.md) | `CBotBuilder` bir sandbox konteyneri içinde web ana bilgisayarında çalışır |
| [0005](./0005-anthropic-raw-http.md) | AI istemcisi ham HTTP kullanır, Anthropic SDK değil |
| [0006](./0006-copy-profile-db-lease.md) | Kopyalama barındırması atomik DB kiralama tarafından koordine edilir |
