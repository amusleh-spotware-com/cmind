---
title: cTrader CLI Düğümleri HTTP + JWT, SSH Yok
---

# Dış cTrader CLI Düğümleri HTTP + JWT'dir, SSH/Kabuk Yok

**Bağlam:** cMind, güvenilmeyen cBot kaynaklarını (kullanıcı seçimi) derler ve çalıştırır. MSBuild veya cTrader CLI'ye erişim sağlamak, bir kötü niyetli cBot bir ev sahibi sisteminin belleğini sızmaya izin verebilir.

**Karar:** Düğüm aracıları SSH kabuk erişimi veya genel paylaşım almayan. Etkileşim:

- Web uygulaması HTTP POST çağrıları yapıyor
- Her istek, düğümün sırrıyla imzalanmış bir kısa süreli HS256 JWT taşır (5 dakika, `iss=app-main` / `aud=app-node`)
- Aracı `ArgumentList` (hiçbir kabuk) aracılığıyla docker çalıştırır
- Yalnızca sanal görüntüleri (belirli `AllowedImagePrefix`) yerine getirir

**Sonuçlar:**

✅ **Sınırlı saldırı yüzeyi:** Kabuk yok, env okunması sınırlı.

✅ **Türk kuralı:** Her istek kesinlikle hedef; aracı başka bir şey yapamaz.

❌ **Karmaşık Kurulum:** Düğümün sırrını, Join token'ı, URL'sini ayarlamalı.

İlgili: [0004-cbotbuilder-on-web-host →](./0004-cbotbuilder-on-web-host.md)
