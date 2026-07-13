---
description: "Satıcı uygulamayı yeniden markalar — ürün adı, logo, favicon, renkler, özel CSS — dağıtım yapılandırması aracılığıyla, kod değişikliği olmadan. Her markalama değeri standart kimliğe varsayılan olarak ayarlanır…"
---

# White-label markalama

Satıcı uygulamayı yeniden markalar — ürün adı, logo, favicon, renkler, özel CSS — dağıtım yapılandırması aracılığıyla, kod değişikliği olmadan. Her markalama değeri **standart kimliğe varsayılan olarak ayarlanır**: yapılandırılmamış dağıtım eskisiyle aynı görünür; satıcı yalnızca gereken şeyi geçersiz kılar.

## Model

- `Core.Options.BrandingOptions` — `App:Branding`'den bağlanır. Dize-tabanlı (yapılandırma kenarı); her renk tema oluşturulurken doğrulanır.
- `Core.Branding.HexColor` — CSS hex rengi (`#RGB` / `#RRGGBB`) için değer nesnesi, değişmez, kendi kendini doğrulayan.
  Geçersiz renk, tema oluşturulurken bir `DomainException` (`domain.branding.color_invalid`) fırlatır — yanlış yapılandırılmış dağıtım, bozuk bir paleti render etmek yerine başlangıçta hızlı başarısız olur.
- `Web.Components.Theme.Build(BrandingOptions)` — markalamadan bir MudBlazor teması üretir. Yalnızca markalı palet girişleri yapılandırmadan gelir; tipografi, düzen, nötr yüzey tonları sabit kalır, böylece ürün satıcılar arasında tutarlı bir görünüm korur.
- `Web.Branding.IBrandingThemeProvider` — singleton, temayı bir kez oluşturur, seçenekler değiştiğinde yeniden oluşturur.
  `MudThemeProvider` için `MainLayout`/`EmptyLayout` tarafından, ürün adı/logosu için uygulama çubuğu tarafından enjekte edilir. `App.razor`, sayfa `<head>`'i için (başlık, açıklama, favicon, tema-rengi, özel CSS) `IOptionsMonitor<AppOptions>`'u doğrudan okur.

## Yapılandırma

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "Description": "AcmeFX — copy trading and strategy automation.",
      "LogoUrl": "/branding/acme-logo.svg",
      "FaviconUrl": "/branding/acme.ico",
      "PrimaryColor": "#2D7FF9",
      "SecondaryColor": "#1E63C8",
      "AppBarColor": "#0B1220",
      "BackgroundColor": "#0E1525",
      "SurfaceColor": "#161E30",
      "SuccessColor": "#3FB950",
      "ErrorColor": "#F85149",
      "WarningColor": "#D29922",
      "InfoColor": "#2D7FF9",
      "CustomCss": ".mud-appbar { letter-spacing: 1px; }"
    }
  }
}
```

Ortam-değişkeni formu: `App__Branding__ProductName=AcmeFX`, `App__Branding__PrimaryColor=%232D7FF9`.

| Anahtar | Etki | Varsayılan |
|-----|--------|---------|
| `ProductName` | Uygulama çubuğu metni + sayfa `<title>` | `cMind` |
| `LogoUrl` | Uygulama çubuğu logo görseli; boş olduğunda ürün adı metni gösterilir | *(boş)* |
| `FaviconUrl` | `<link rel="icon">` | `favicon.svg` |
| `Description` | `<meta name="description">` | standart açıklama |
| `PrimaryColor` / `SecondaryColor` | vurgu, çekmece simgesi, düğmeler | `#26C281` / `#1FB97A` |
| `AppBarColor` / `BackgroundColor` / `SurfaceColor` | krom + yüzeyler; `AppBarColor`, `<meta theme-color>` + PWA manifest `theme_color`'ı yönlendirir, `BackgroundColor` manifest `background_color`'ı | koyu palet |
| `SuccessColor` / `ErrorColor` / `WarningColor` / `InfoColor` | durum renkleri | standart |
| `CustomCss` | `<head>`'e enjekte edilen `<style>` (dağıtım-güvenilir) | *(boş)* |
| `ShowSiteLink` | gösterge panelinde "Powered by cMind" kredi bağlantısını göster | `true` |
| `RequireMfa` | uygulamayı kullanmadan önce her kullanıcının iki-faktörlü kimlik doğrulama kurmasını iste | `false` |
| `NodesUi` | Nodes yüzeyinin ne kadarının gönderileceği: `Full` (liste + manuel ekle/sil), `Monitor` (salt-okunur liste, ekle/sil yok), `Hidden` (nav yok, sayfa yok, manuel API yok) | `Full` |
| `RestrictNodesToOwner` | `true` olduğunda, yalnızca sahip düğümleri görebilir/yönetebilir; aksi takdirde tüm yönetici-veya-üstü personel yüzeyi görebilir. Normal kullanıcılar her iki durumda da düğümleri asla görmez | `false` |

`LogoUrl`/`FaviconUrl` tarafından referans verilen varlıklar, Web uygulaması `wwwroot`'undan (örn. `wwwroot/branding/` klasörünü bağlayın) veya herhangi bir mutlak URL'den sunulur.

`App:Branding` başlangıçta doğrulanır (`BrandingOptionsValidator`, `ValidateOnStart` aracılığıyla çalıştırılır): her renk geçerli hex olmalı, `CustomCss` `<`/`>` içermemeli (`<style>` etiketinden dışarı çıkamaz). Yanlış yapılandırılmış dağıtım, bozuk bir sayfayı render etmek yerine açık bir mesajla başlatılamaz.

## Powered-by bağlantısı

Gösterge paneli, projenin dokümantasyon sitesine işaret eden küçük bir **"Powered by cMind"** kredi bağlantısı
render eder. `App:Branding:ShowSiteLink` tarafından kontrol edilir ve **varsayılan olarak `true`'dur** —
yapılandırılmamış bir dağıtım onu gösterir. Tamamen white-label bir örnek çalıştıran bir satıcı, onu tamamen
kaldırmak için `App__Branding__ShowSiteLink=false` ayarlar.

Bağlantı, gösterge paneli bileşeni tarafından yayınlanır ve bayrağı `IBrandingThemeProvider` /
`BrandingOptions` aracılığıyla okur, bu nedenle onu değiştirmek yalnızca-yapılandırma değişikliğidir (yeniden derleme yok). İş
odaklı özet için [İşletmeler için white-label](../white-label-for-business.md#the-powered-by-cmind-link)'a bakın.

## Broker izin-listesi

Bir white-label dağıtımı, kullanıcılarının hangi broker'ların ticaret hesaplarını ekleyebileceğini kısıtlayabilir — böylece
kendi müşterileri için cMind çalıştıran bir broker yalnızca kendi kitabına hizmet eder. `App:Accounts` altında yapılandırılır:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Pepperstone", "IC Markets"]
    }
  }
}
```

Ortam-değişkeni formu: `App__Accounts__AllowedBrokers__0=Pepperstone`.

**Davranış:**

- **Boş liste (varsayılan) ⇒ kısıtlamasız.** Her broker'a izin verilir ve **hiçbir doğrulama çalışmaz** — bir
  standart dağıtım tamamen değişmez.
- **Boş olmayan ⇒ kısıtlanmış.** cMind, bir kullanıcının eklemeye çalıştığı her hesabı listeye karşı kontrol eder
  (büyük/küçük harfe duyarsız):
  - **Open API (OAuth) bağlantısı** — broker adı cTrader Open API tarafından yetkili olarak bildirilir, bu nedenle
    izin verilmeyen bir hesap basitçe **atlanır** (aynı yetkilendirmedeki izin verilen hesaplar yine de bağlanır);
    yetkilendirme sayfası kullanıcıya hangi broker'ların atlandığını söyler.
  - **Manuel cID (kullanıcı adı / parola)** — kullanıcının yazdığı broker'a **güvenilmez**. cMind, cTrader CLI
    aracılığıyla gönderilen broker-probe cBot'unu çalıştırarak (`Account.BrokerName`'i okuyarak) hesabın gerçek broker'ını
    **doğrular** ve o doğrulanmış adı kalıcılaştırır. İzin verilmeyen bir broker bir bildirimle reddedilir;
    bir doğrulama başarısızlığı (kötü kimlik bilgileri, düğüm yok, zaman aşımı) da yüzeye çıkarılır ve
    hesap eklenmez.

**Model:**

- `Core.Options.AccountsOptions` — `App:Accounts`'tan bağlanır (`AllowedBrokers`, `BrokerProbeTimeout`,
  `BrokerProbeAlgoPath`).
- `Core.Accounts.BrokerName` — değer nesnesi (kırpılmış, büyük/küçük harfe duyarsız eşitlik).
- `Core.Accounts.BrokerAllowlist` — `IsRestricted` / `Allows(broker)`; boş = hepsine izin ver. `CTraderIdAccount.AddTradingAccount` / `LinkOpenApiAccount`
  içinde bir değişmez olarak uygulanır (`domain.account.broker_not_allowed`).
- `Core.Accounts.IBrokerVerifier` → `Web.Accounts.BrokerVerifier` — probe konteynerini web
  host'unda (Docker soketine sahip olan) çalıştırır, logları izler ve broker'ı
  `Core.Accounts.BrokerProbeOutput` aracılığıyla ayrıştırır. Yalnızca izin-listesi kısıtlanmış olduğunda çağrılır.

**Broker-probe cBot'u:** Web uygulamasıyla önceden derlenmiş bir `broker-probe.algo` gönderilir (`src/Web/BrokerProbe/`,
çıktıya `broker-probe/broker-probe.algo` olarak kopyalanır), böylece varsayılan
`App:Accounts:BrokerProbeAlgoPath` kutudan çıkar çıkmaz çözümlenir — göreli bir yol, uygulama
temel dizinine karşı çözümlenir, mutlak bir yol verildiği gibi kullanılır. Kaynak `tools/broker-probe/`'de bulunur.
Algo mevcut olmadığında, manuel-cID doğrulaması kapalı başarısız olur — kısıtlanmış bir izin-listesi altındaki hesaplar yine de
probe gerektirmeyen Open API yolu aracılığıyla bağlanabilir.

## Broker izin-listesi — testler

- **Birim** — `UnitTests/Accounts/`: `BrokerName`/`BrokerAllowlist` değer nesneleri, `BrokerProbeOutput`
  ayrıştırıcısı ve `CTraderIdAccount` izin-listesi değişmezi.
- **Entegrasyon** — `IntegrationTests/BrokerAllowlistTests.cs`: sahte bir doğrulayıcı ile manuel-cID uç noktası
  (kısıtlamasız / doğrulanmış / izin verilmeyen / doğrulama-başarısız) + izin verilmeyen hesapları atlayan Open API
  bağlayıcısı. `BrokerVerifierLiveTests.cs`, cID kimlik bilgileri + algo sağlandığında **gerçek** probe'u çalıştırır
  (aksi takdirde temiz bir şekilde atlar).
- **E2E** — `E2ETests/BrokerAllowlistTests.cs`: kısıtlanmış bir dağıtım, gerçek UI aracılığıyla bir manuel eklemeyi
  reddeder ve "couldn't verify" bildirimini gösterir (hiçbir hesap satırı eklenmez).

## Nodes UI görünürlüğü

Düğümler, çoğu kiracının asla elle yönetmediği altyapıdır — cTrader CLI ajanları
[kendi kendine kaydolur ve nabız gönderir](../operations/node-discovery.md), bu nedenle bir white-label dağıtımı,
manuel kontrolleri veya Nodes yüzeyini tamamen gizleyebilir ve yine de otomatik-keşif yoluyla sağlıklı bir küme çalıştırabilir.
Bunu iki yalnızca-yapılandırma markalama anahtarı yönetir:

```json
{
  "App": {
    "Branding": {
      "NodesUi": "Monitor",
      "RestrictNodesToOwner": true
    }
  }
}
```

Ortam-değişkeni formu: `App__Branding__NodesUi=Hidden`, `App__Branding__RestrictNodesToOwner=true`.

**`NodesUi` — üç mod:**

- **`Full` (varsayılan)** — standart ürün: düğüm listesi artı manuel **New Node** ve **Delete**
  kontrolleri. `POST`/`DELETE /api/nodes` çalışır.
- **`Monitor`** — salt-okunur bir yüzey: liste ve canlı istatistikler kalır, ancak manuel ekleme ve silme
  kaldırılır. Düğümler yalnızca otomatik-keşif yoluyla görünür. `POST`/`DELETE /api/nodes` **404** döndürür.
- **`Hidden`** — Nodes nav bağlantısı ve sayfası tamamen kaybolur ve sayfa rotası gösterge paneline
  yönlendirir; manuel ekleme/silme API'si kapalıdır. Küme yalnızca otomatik-keşiftir.

**`RestrictNodesToOwner`**, kimin düğümleri görüp yönetebileceğini belirler. Varsayılan `false`, standart
**yönetici-veya-üstü** personel yüzeyini (`AdminOrAbove`) korur; **yalnızca-sahip** (`Owner`) yapmak için `true` ayarlayın. Her
iki durumda da **normal kullanıcılar asla düğümleri görmez** — bu yalnızca yalnızca-sahip ile daha geniş personel yüzeyi arasında seçim yapar.

Düğüm **otomatik-keşfi her iki anahtardan da etkilenmez**: anonim `POST /api/nodes/register` kendi kendine kaydolma
+ nabız uç noktası her zaman çalışır, bu nedenle bir `Hidden`/`Monitor` dağıtımı yine de kümesini otomatik olarak büyütür.

**Model:**

- `Core.Nodes.NodesUiMode` — `Full` / `Monitor` / `Hidden`.
- `Core.Nodes.NodesUiAccess` — modu + sahip-kısıtlamasını oluşturan tek doğruluk kaynağı:
  `IsPageVisible`, `AllowsManualManagement`, `RequiredPolicy(restrictToOwner)`. Nav
  (`NavMenu.razor`), sayfa (`Pages/Nodes.razor`) ve uç noktalar (`NodeEndpoints`) hepsi onu okur, böylece
  UI ve API asla anlaşmazlığa düşemez.
- `Core.Options.BrandingOptions.NodesUi` / `.RestrictNodesToOwner` — `App:Branding`'den bağlanır.

## Nodes UI görünürlüğü — testler

- **Birim** — `UnitTests/Nodes/NodesUiAccessTests.cs`: her mod + varsayılan markalama boyunca sayfa-görünürlüğü,
  manuel-yönetim ve gerekli-politika çözümü.
- **Entegrasyon** — `IntegrationTests/NodeUiGatingTests.cs`: gerçek HTTP + Postgres üzerinden — `Full` bir
  manuel eklemeye izin verir, `Monitor`/`Hidden` ekleme ve silmeyi 404 yapar ve `RestrictNodesToOwner` bir yöneticiyi yasaklarken
  sahip yine de listeyi okur.
- **E2E** — `E2ETests/NodesUiTests.cs` (varsayılan `Full`: nav bağlantısı + sayfa + New Node düğmesi render edilir) ve
  `E2ETests/NodesHiddenTests.cs` (`Hidden`: nav bağlantısı kaybolur, `/nodes` yönlendirir).

## Tasarım token'ları (CSS değişkenleri)

Markalama ayrıca uygulamanın **kendi** stil sayfasına + özel bileşenlerine ulaşır, yalnızca MudBlazor'a değil. `Web.Branding.BrandingCss.BuildRootVariables(BrandingOptions)`, markalı paleti `:root` üzerinde CSS özel özellikleri olarak yayınlar (`--app-primary`, `--app-primary-hover`, `--app-surface`, `--app-appbar`, `--app-success`/`--app-error`/`--app-warning`/`--app-info`, …), `App.razor`'da `site.css`'in hemen ardından enjekte edilir. `site.css` ve her bileşen `var(--app-*)` okur — **sabit-kodlanmış renk yok** — böylece bir satıcının paleti bedavaya her yere akar (giriş kahramanı, alt nav, yardım ipuçları, çevrimdışı sayfa). Nötr yüzey tonları `site.css :root`'ta varsayılan olarak ayarlanır; `CustomCss` (en son enjekte edilir) herhangi bir token'ı geçersiz kılabilir. Bkz. [ui-guidelines.md](../ui-guidelines.md) §2.

## Markalı PWA

Kurulabilir uygulama da markalıdır — manifest uç noktası (`/manifest.webmanifest`), `BrandingOptions`'tan oluşturulur (`ProductName` → `name`/`short_name`, `Description`, `AppBarColor`/`BackgroundColor` → tema/arka plan). Bkz. [pwa.md](pwa.md).

## Testler

- **Birim** — `UnitTests/Branding/HexColorTests.cs`: geçerli/geçersiz hex doğrulaması.
- **Entegrasyon** — `IntegrationTests/ThemeBuildTests.cs`: renkler palete eşlenir, geçersiz renk fırlatır;
  `IntegrationTests/BrandingHttpTests.cs`: özel `ProductName`/açıklama/tema-rengi sunulan sayfa `<head>`'inde render edilir (WebApplicationFactory + Postgres), varsayılanlar standart adı korur.
- **E2E** — `E2ETests/BrandingTests.cs`: markalı ürün adı gerçek tarayıcıda uygulama çubuğunda render edilir.
