---
title: Mimari Karar Kayıtları
description: cMind'ın arkasındaki açık olmayan tasarım kararları — bağlam, karar ve sonuçlar — kodu okuyarak çıkaramazsınız.
---

# Mimari Karar Kayıtları

Bunlar **kodu çıkaramazsınız** tasarım kararlarını kaydeder — değiş tokuşlar, alınan yollar değildi ve neden.
Her biri kısa: *Bağlam → Karar → Sonuçlar*. Yeni yapısal karar → bir ADR burada (sonraki numara) ekleyin,
böylece sonraki mühendis (insan veya AI) mantığı, sadece sonucu değil, miras alır.

| # | Karar |
|---|---|
| [0001](./0001-strict-ddd-pure-core.md) | Saf `Core` ile katı DDD |
| [0002](./0002-tph-instance-replaces-entity.md) | Örnek durum TPH'dir; bir geçiş varlığı değiştirir |
| [0003](./0003-external-nodes-http-jwt.md) | cTrader CLI düğümleri HTTP + JWT'dir, SSH/kabuk yok |
| [0004](./0004-cbotbuilder-on-web-host.md) | `CBotBuilder` bir sandbox konteynerinde web sunucusunda çalışır |
| [0005](./0005-anthropic-raw-http.md) | AI istemcisi raw HTTP kullanır, Anthropic SDK değil |
| [0006](./0006-copy-profile-db-lease.md) | Kopya barındırması atomik bir DB kirasıyla koordine edilir |
