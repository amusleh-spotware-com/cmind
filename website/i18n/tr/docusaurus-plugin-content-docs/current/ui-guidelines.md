---
description: "Bu uygulamadaki her yeni veya değiştirilmiş UI için bağlayıcı (Blazor sayfaları, iletişim kutuları, bileşenler). Bu, `CLAUDE.md` tarafından referans alınan…"
---

# UI Tasarım Yönergeleri — ZORUNLU

Bu uygulamadaki **her** yeni veya değiştirilmiş UI için bağlayıcı (Blazor sayfaları, iletişim kutuları, bileşenler).
Bu, `CLAUDE.md` tarafından referans alınan doğruluk kaynağıdır. Bir kural sizi engelliyorsa, durun ve sorun — kuralı ihlal eden UI yayınlamayın. `plans/ui-overhaul.md` temelinde yazılmıştır.

## 1. Mobil-birinci, her zaman

- **İlk olarak 360–430px telefon için yazın**, ardından `min-width` medya sorguları / MudBlazor
  breakpoint özellikleriyle yukarı doğru geliştirin. `max-width` geçersiz kılmalarla asla masaüstü-birinci olmayın.
- **320–1920px aralığında hiçbir genişlikte yatay kaydırma yok.** İçerik görüntüleme alanından daha geniş ise, bu bir hatadır.
- Dokunma hedefleri ≥ **44px** (`var(--app-touch-target)`). Metin girişleri ≥ 16px yazı tipi (iOS odak üzerine zum'u engeller).
- Çentiği dikkate alın: `env(safe-area-inset-*)`'i kullanın; görüntüleme alanı zaten `viewport-fit=cover`'ı ayarlamıştır.
- `prefers-reduced-motion`'ı onaylamalı — temel bilgi yalnızca animasyonla iletilmemelidir.

## 2. Tasarım belirteçleri — sabit kodlanmış değer yok

- Tüm renk/yarıçap/boşluk **tasarım belirteçlerinden** gelir: MudBlazor teması (`Web/Components/Theme.cs`) +
  `Web/Branding/BrandingCss.cs` tarafından yayılan CSS özel özellikleri (`var(--app-primary)`,
  `--app-surface`, `--app-border`, `--app-text*`, `--app-radius`, …).
- **Bir bileşenin veya CSS kuralının içine hiçbir zaman hex rengi, yarıçapı veya marka dizesini sabit kodlamayın.** Bir belirteci oku.
  Belirteçler beyaz etiket `BrandingOptions` öğesinden akıyor, bu nedenle bir satıcının paleti UI'niz için ücretsiz ulaşmalıdır.
- Yeni marka etkileyen değer → bir belirteç + marka alanı ekleyin; satır içi kodlamayın.

## 3. Duyarlı düzen ve veri

- **Tablolar telefonda kartlara daraltılır.** Her `MudTable` `Breakpoint="Breakpoint.Sm"` ayarlar ve her
  `MudTd` bir `DataLabel` sahibidir. Mobilde ham geniş tablo yok. (Şablon: `Components/Pages/Nodes.razor`.)
- Kılavuzlar: `MudItem xs="12" sm="6" md="4"` — telefonda tam genişlik, yukarı doğru çok sütunlu.
- Mobilde tek sütunlu formlar; büyük dokun hedefleri; giriş alanlarında `inputmode`/`autocomplete`; para/yüzde için sayısal/ondalık
  inputmode.
- **Yapılandırılmış giriş için uygun denetimler — asla sayılar veya listeler için ham metin kutusu.** Sayıları,
  parayı, yüzdeleri, tarihleri, numaralandırmaları ve herhangi bir çoklu değer verisi uygun denetimle toplayın (`MudNumericField`,
  `MudDatePicker`, `MudSelect`, yazılı alanların düzenlenebilir ekleme/kaldırma satırı listesi veya tablo), her alan
  ayrı ayrı doğrulandı. Kullanıcının virgül/boşluk/satırsonuna ayrılmış bir blob yazması gereken ve daha sonra ayrıştırdığınız tek ücretsiz metin `MudTextField`
  — **yasaktır**: hata yapmaya eğilimliydir, doğrulanmamıştır ve telefonda çatışmacıdır. **Kimse bir blob yazmak istemiyor.** Çoklu değer girişi yazılı satırların düzenlenebilir listesidir (ekle/
  kaldır), veya mevcut alan verilerinden yüklenir (örneğin, tamamlanmış bir arka testin sayılarını yeniden girmek yerine doğrulamayı doğrudan çalıştırın).
  Düz `MudTextField` yalnızca gerçek serbest metin — adlar, notlar, arama, açıklamalar için geçerlidir.
- Her liste/ayrıntı üzerinde **yükleme, boş ve hata** durumları sağlayın — mobil için boyutlandırılmış.
- Mobil **alt navigasyonu** (`Components/Layout/BottomNav.razor`) birincil telefon nav'ıdır; gruplandırılmış çekmece tam menüdür. Yüksek trafik hedeflerini ekleyin; ≤5 öğede tutun.

## 4. İletişim Kutuları (oluştur/düzenle)

- Tüm ekleme/oluşturma/düzenleme/yeni eylemler **MudBlazor iletişim kutusu** (`IDialogService.ShowAsync<TDialog>`) kullanır, hiçbir zaman
  satır içi sayfa formu. İletişim kutuları `Web/Components/Dialogs/` içinde yaşar, `[Parameter]`'leri gösterir, iç içe bir
  `public sealed record …Result(...)` döndürür. Liste satırı eylemleri (başlat/durdur/sil) satır içi ikon düğmeleri olarak kalır.
- Telefonda, iletişim kutuları **tam ekran / tam genişlik** ve klavye bilincinde olmalıdır.

## 5. Satır içi yardım — her denetim

- Her açık olmayan seçenek, seçim, anahtar veya eylem bir **`<HelpTip Text="…" />`** alır
  (`Components/HelpTip.razor`) — masaüstünde gezin, **mobilde dokun**. Metni `docs/` kaynağından alın; rehberlik davranış ile senkronize kalır; her ikisini de aynı commit içinde güncelleyin.

## 6. Beyaz etiket

- Ürün adı, logo, açıklama, destek/şirket, renkler, favicon hepsi `BrandingOptions` öğesinden gelir.
  Bunları başvur (`IBrandingThemeProvider` / `IOptionsMonitor<AppOptions>`), hiçbir zaman hazır "cMind" veya marka rengi. PWA manifestosu, simgeler, tema rengi ve giriş kahramanı hepsi markalı.

## 7. PWA

- Uygulama kurulabilirdir. Manifest uç noktasını (`/manifest.webmanifest`) markaladı tutun, simgeler mevcut
  (192/512/maskable + apple-touch), hizmet çalışanı uygulama kabuğu-yalnız (Blazor
  devresi/`_framework`/hub'lara asla dokunmayan), ve çevrimdışı sayfa çalışıyor. Yeni statik rota → manifest `scope`'yi koruyun.
- Blazor Server canlı SignalR devresi gerektirir → **kurulabilir + uygulama kabuğu**, tam çevrimdışı değil. Çevrimdışı etkileşim vaadi vermeyin.

## 8. Erişilebilirlik

- Giriş alanlarında etiketler, özel denetimler üzerinde `aria-*`, görünür odak, mantıksal odak sırası. Tema beyaz etiketlenebilir olduğundan, **kontrast** sabit bir palet değil, etkin tema karşısında doğrulayın.

## 9. E2E — hiçbir UI test edilmeden gemiye çıkmaz (engelleme)

Her kullanıcı yüzü değişikliği Playwright E2E'yi `tests/E2ETests` içinde sevk eder, gerçek bir kullanıcı gibi yürütüldü, **mobil
cihaz öykünmesi** artı masaüstü:

- Yeni rota → `PageSmokeTests` **ve** `MobileLayoutTests` öğesine ekleyin (oluşturur, alt nav, hata UI yok).
- Tablo/sayfa dönüştürün → rotasını mobil **no-overflow** kümesine ekleyin.
- Yeni akış → gerçekçi mobil yolculuğu (oluştur/düzenle/kaydet gidiş-dönüş) **ve** mutsuz yolu
  (geçersiz giriş, boş liste, rol başına izin reddedildi).
- Yeni yardım ipucu → dokun üzerine açıldığını iddia et (`HelpTipTests` düzeni).
- `AppFixture.NewAuthedMobilePageAsync` / `NewAnonymousMobilePageAsync` kullanın (cihaz öykünmesi).
- `dotnet test` "bitti" öncesinde yeşil. Öykünülen WebKit ≠ mobil Safari — gerçek cihaz kapılı ayrı bir sürüm adımıdır.

## 10. Tanım (UI) tamamlanmış

- [ ] Mobil-birinci; 320–1920px aralığında hiçbir yatay taşma yok; dokunma hedefleri ≥44px.
- [ ] Yalnızca tasarım belirteçleri — sıfır sabit kodlanmış renkler/yarıçaplar/marka dizeler.
- [ ] Tablolar → telefonda kartlara (`DataLabel` + `Breakpoint.Sm`); yükleme/boş/hata durumları mevcut.
- [ ] Yapılandırılmış giriş uygun doğrulanmış denetimler kullanır (sayısal/tarih/seçim/düzenlenebilir satır listesi) — kullanıcının sınırlandırılmış sayı/değer blob'unu yazması gereken ham metin kutusu yok.
- [ ] İletişim kutusu aracılığıyla oluştur/düzenle; mobilde tam ekran.
- [ ] Her denetimin `HelpTip` doc'lardan kaynak aldığı bir değeri vardır.
- [ ] Beyaz etiket + PWA dikkate alındı.
- [ ] Mobil + masaüstü E2E eklendi (smoke, no-overflow, journey, unhappy path); `dotnet test` yeşil.
- [ ] Rider `get_file_problems` + `dotnet format analyzers` dokunulan dosyalar üzerinde temiz.
