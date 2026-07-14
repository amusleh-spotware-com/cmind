---
description: "Gösterilen her saat kendi saat diliminizde görünür — ilk ziyarette tarayıcıdan algılanır ve Ayarlar'dan değiştirilebilir. Depolama ve API'ler UTC kalır."
---

# Saat dilimi

Uygulamanın gösterdiği her saat, sunucununki değil, kendi saat diliminizde işlenir. Seçiminiz profilinize kaydedilir ve cihazlar arasında sizi takip eder.

İlk ziyaretinizde uygulama tarayıcınızın saat dilimini otomatik benimser. İstediğiniz zaman Ayarlar → Saat dilimi'nden değiştirebilirsiniz; dağıtım varsayılanı white-label seçeneği App:Branding:DefaultTimeZone'dur (varsayılan UTC). Saatler her zaman UTC olarak saklanır ve API'den döner — yalnızca görüntüleme dönüştürülür.

- Çözümleme sırası: profil dilimi, ardından çerez, ardından dağıtım varsayılanı, ardından UTC.
- Algılama bir kez çalışır ve seçtiğiniz dilimi asla geçersiz kılmaz.
- Biçimlendirme dilinizi izler; «2 dakika önce» gibi göreli etiketler etkilenmez.
