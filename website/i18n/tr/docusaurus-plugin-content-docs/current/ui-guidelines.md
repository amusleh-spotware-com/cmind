---
title: UI Yönergeleri
sidebar_position: 8
---

# UI Yönergeleri

cMind UI bileşenleri, desen ve tasarım kararları.

## İlkeler

- **Mobil öncelikli:** 360px telefondan 1920px masaüstüne kadar ölçekle; yatay kaydırma yok.
- **Diyaloglar:** Tüm ekle/düzenle/sil eylemler MudBlazor diyaloglarında; sayfa formu yok.
- **Tasarım token'ları:** Renkler, yazı tipi boyutları ve boşluklar `custom.css` aracılığıyla; inline stiller yok.
- **Erişilebilirlik:** Semantik HTML, ARIA etiketleri, klavye navigasyonu desteklenmiş.
- **Sağdan sola:** MudRTLProvider + MudThemeProvider'ın RTL modunu kullanın; Arapça, Farsça, İbranice kullanıcıları için test edin.

## Bileşen Kitaplığı

- **MudBlazor** tüm UI bileşenleri için — butonlar, girdiler, kartlar, tablolar, diyaloglar.
- **ApexCharts** grafikler ve göstergeler için.
- **Markdown** kalma belgeleri için (not: Blazor özellik sınırlaması — tam HTML oluştur yok).

## Renk & Tema

Tasarım token'ları (aşağıya bakın) dağıtım yapılandırmasından iletilir:

```json
{
  "App": {
    "Branding": {
      "PrimaryColor": "#2D7FF9",
      "SecondaryColor": "#1E63C8"
    }
  }
}
```

`src/Web/wwwroot/css/custom.css` adresinde varsayılan değerler tanımlanır. Sahibin override'ları Ayarlar > Dağıtım'da tanıtılır ve canlı uygulanır.

## İletişim Yazı

Hiçbir hard-kodlanmış string. Tüm kullanıcı karşılıklı metin:

- Blazor bileşenlerinde: `@L["key"]` (IStringLocalizer<Ui>)
- Uç noktalarda: veritabanı / DomainErrors'den mesajlar

Her yazı dizesi tüm desteklenen dillerde `tools/i18n/ui-translations.json` adresinde yaşar. Düzenleme → `pwsh tools/i18n/gen-resx.ps1`.

## Navigasyon

- **Kabuk:** üst düzey sekme (Pano, cBot'lar, Kopyalama, Ayarlar)
- **Ayarlar:** Profil, Dağıtım (sahip), İntegrasyon, Hakkında
- **Özellik kapısı:** Devre dışı bırakılan özellikler 404 veya gizli nav dönüş öğeleri

## Sayfalar & Diyaloglar

Tüm yeni iş akışları diyaloglar:

```csharp
// ❌ Kötü: sayfa formu
@page "/bots/create"
<form>...</form>

// ✅ İyi: diyalog
<MudDialog>
  <MudForm @ref="form">
    ...
  </MudForm>
</MudDialog>
```

## E2E Test Kapsama

Yeni Blazor sayfaları → `PageSmokeTests.Routes()` adresinde kaydettirin. Yeni etkileşimli kontroller → Playwright E2E test edin.

Daha fazla: [Deployment kılavuzu →](./deployment/local.md)
