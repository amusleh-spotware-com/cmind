---
description: "cMind bir telefona veya masaüstüne yerel bir uygulama gibi kurulur — ana ekran simgesi, bağımsız pencere, açılış ekranı ve dostça bir çevrimdışı sayfa. Mobil-öncelikli ve…"
---

# Kurulabilir uygulama (PWA)

cMind bir telefona veya masaüstüne yerel bir uygulama gibi kurulur — ana ekran simgesi, bağımsız pencere,
açılış ekranı ve dostça bir çevrimdışı sayfa. **Mobil-öncelikli** ve tamamen duyarlıdır; bkz.
[ui-guidelines.md](../ui-guidelines.md).

## "Kurulabilir"in burada anlamı — ve dürüst sınır

Blazor **Server**, canlı bir SignalR devresi aracılığıyla oluşturur, bu yüzden uygulama tamamen çevrimdışı
çalışamaz. PWA'nın sunduğu:

- **Kurulabilir** — geçerli web manifesti + simgeler, böylece tarayıcılar *Yükle* / *Ana Ekrana Ekle* sunar.
- **Uygulama-kabuğu önbelleğe alınmış** — service worker, statik varlıkları (CSS, simgeler, manifest)
  önbelleğe alır ve ağ düştüğünde bir tarayıcı hatası yerine bir **çevrimdışı sayfa** gösterir.
- **Yerel his** — bağımsız görüntü, markalı tema-rengi/durum çubuğu, uygulama simgesi, iOS ana-ekran simgesi.

Canlı özelliklerin çevrimdışı etkileşimini **sağlamaz** — bu, Blazor WebAssembly gerektirir (ayrı bir
gelecekteki iz). Canlı özelliklerin çevrimdışı kullanımını vaat etmeyin.

## Parçalar

| Parça | Nerede |
|-------|-------|
| Manifest (dinamik, markalı) | `Web/Endpoints/PwaEndpoints.cs` → `GET /manifest.webmanifest` (anonim) |
| Simgeler (192, 512, 512-maskable, apple-touch-180) | `Web/wwwroot/icons/` |
| Service worker (uygulama-kabuğu) | `Web/wwwroot/service-worker.js` |
| Çevrimdışı yedek sayfa | `Web/wwwroot/offline.html` |
| Kayıt + iOS etiketleri + kurulum-istemi yakalama | `Web/Components/App.razor` |
| Rota sabitleri | `Core.Constants.PwaRoutes` |

### Manifest

`BrandingOptions`'tan dinamik olarak sunulur, böylece bir satıcının ürün adı, renkleri ve simgeleri kurulan
uygulamaya taşınır: `ProductName`'den `name`/`short_name`, `description`, `AppBarColor`'dan `theme_color`,
`BackgroundColor`'dan `background_color`, `display: standalone` ve simge seti (temiz bir Android simgesi için
bir **maskable** 512 dahil). Anonim — kurulum istemi giriş öncesinde çalışmalıdır.

### Service worker

Yalnızca uygulama-kabuğu. Blazor devresini (`/_blazor`), çerçeveyi (`/_framework`) veya SignalR hub'larını
(`/hubs`) **asla** kesmez — bunlar her zaman ağdır. Gezinmeler, çevrimdışı sayfa yedek olarak ağ-önce'dir;
statik varlıklar (`/css`, `/icons`, `/_content`), arka planda yeniden-doğrulamayla önbellek-önce'dir.
`updateViaCache: 'none'` ile kaydedilir, böylece worker güncellemeleri güvenilir biçimde uygulanır.
Önbellekler sürümlüdür (`cmind-shell-v<n>`) — kabuk değişikliklerinde artırın.

### iOS

iOS, manifest simgelerini/açılış ekranını yoksayar, bu yüzden `App.razor` ayrıca `apple-touch-icon` ve
`apple-mobile-web-app-*` meta etiketleri yayar. iOS'ta `beforeinstallprompt` yoktur; kullanıcılar Safari'nin
*Ana Ekrana Ekle*'si aracılığıyla kurar. `beforeinstallprompt`, özel bir kurulum olanağı için
Chromium/Android'de `window.deferredInstallPrompt`'a yakalanır.

## Testler

- **E2E** — `E2ETests/PwaTests.cs`: manifest `application/manifest+json` ile sunulur, bir maskable dahil
  boş-olmayan simgeler, `display: standalone`, `apple-touch-icon` bağlı ve service worker kaydolur +
  etkinleşir. `MobileLayoutTests` / `MobileDialogTests`, PWA'nın kurduğu mobil kabuğu kapsar.
