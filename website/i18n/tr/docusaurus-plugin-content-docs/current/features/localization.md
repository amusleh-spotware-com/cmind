# Yerelleştirme (i18n)

cMind tamamen yerelleştirilebilir ve **cTrader'ın kendisinin desteklediği aynı 23 dilde** gönderilir,
böylece bir yatırımcı platformu — ve bu belgeleri — kendi dilinde kullanır. İngilizce yedektir; eksik
herhangi bir çeviri, boş bir ekran veya ham bir anahtar göstermek yerine zarifçe İngilizceye iner.

## Desteklenen diller

Arapça (RTL), Çince (Basitleştirilmiş), Çekçe, İngilizce, Fransızca, Almanca, Yunanca, Macarca, Endonezce,
İtalyanca, Japonca, Korece, Malayca, Lehçe, Portekizce (Brezilya), Rusça, Sırpça, Slovakça, Slovence,
İspanyolca, Tayca, Türkçe, Vietnamca.

Tek doğruluk kaynağı `Core.Constants.SupportedCultures`'tır — istek-kültürü ara yazılımı, dil değiştirici,
kaynak-parite testi ve sabit-kodlanmamış-dize kapısının tümü ondan okur. Bir dil eklemek, orada bir satırlık
bir değişiklik artı kaynak dosyalarıdır.

## Nasıl çalışır (Blazor Server)

- **Kaynaklar.** UI dizeleri `src/Web/Resources/Ui.resx`'te (İngilizce temel) artı dil başına bir
  `Ui.<culture>.resx`'te yaşar. Bileşenler onları `IStringLocalizer<Ui>` aracılığıyla okur — `@L["key"]`,
  asla bir değişmez. `.resx` dosyaları, çevirmen-dostu doğruluk kaynağı olan
  `tools/i18n/ui-translations.json`'dan (`pwsh tools/i18n/gen-resx.ps1`) oluşturulur.
- **Kültür çözümü.** `RequestLocalizationMiddleware`, kültürü önce `.AspNetCore.Culture` çerezinden, sonra
  tarayıcının `Accept-Language`'ından, sonra İngilizceden seçer.
- **Değiştirme.** Uygulama-çubuğu dil değiştirici (ve **Settings → Language** bölümü),
  `GET /set-culture` uç noktasına gider — Blazor devresi dışında tam bir yeniden yükleme, çünkü bir devre
  kültürü canlı değiştiremez. Çerezi yazar ve giriş yapmış bir kullanıcı için seçimi profiline
  (`UserProfile.Locale`) kalıcılaştırır; yeniden yükleme, seçilen dilde yeni bir devre başlatır.
- **Kalıcılık ve giriş.** Kaydedilen profil yereli, girişte kültür çerezine geri yazılır, böylece bir
  kullanıcı her cihazda kendi dilinde iner.
- **Sağdan-sola.** Arapça (ve gelecekteki herhangi bir RTL dili) `<html dir="rtl">` ayarlar ve düzeni
  MudBlazor'un `MudRTLProvider`'ında sarar, tüm kabuğu yansıtır.
- **ICU.** Web host'u ICU etkin çalışır (`InvariantGlobalization=false`); tel/ayrıştırma kodu
  `CultureInfo.InvariantCulture`'da kalır, böylece yalnızca kültür-başına UI biçimlendirmesi etkilenir —
  asla bir backtest veya CSV.

## Kapı — sabit-kodlanmış UI metni yok

Yeni kullanıcıya-dönük dizeler, kapsanan kapsamda yerelleştirilmemiş olarak birleştirilemez:

- Derlemeyi-başarısız-kılan bir mimari-koruma testi (`NoHardcodedUiTextTests`), taşınan `.razor` dosyalarını
  tarar ve `@L["…"]` araması olmayan herhangi bir değişmez, metin-taşıyan öznitelikte (`Label`, `Text`,
  `Title`, `Placeholder`, `HelperText`, `aria-label`, `alt`) başarısız olur.
- Bir kaynak-parite testi (`ResourceParityTests`), herhangi bir dil bir anahtarı kaçırırsa veya boş bir değer
  gönderirse derlemeyi başarısız kılar — her dilde her zaman her anahtar bulunur.

## Bir dize ekleme veya değiştirme

1. `tools/i18n/ui-translations.json`'da anahtarı **her** kültür için ekleyin/düzenleyin.
2. `.resx`'i yeniden oluşturun: `pwsh tools/i18n/gen-resx.ps1`.
3. Bileşende `@L["your.key"]` ile referans verin.
4. `dotnet test` — parite ve sabit-kodlanmış-metin kapıları sizi dürüst tutar.

## Belge yerelleştirmesi

Bu belgeler de yerelleştirilmiştir. Docusaurus i18n, navbar'da bir yerel açılır menüsü ve Arapça için RTL ile
tüm 23 yerel için yapılandırılmıştır (`website/i18n/`). Bir yerelin çeviri dosyalarını
`npm run write-translations -- --locale <code>` ile iskeletleyin ve `website/i18n/<code>/` altında çevirin.
Yerelleştirme yönergesi gereği, **herhangi bir belgeyi eklemek veya değiştirmek, her yereli aynı değişiklikte güncellemek anlamına gelir.**
