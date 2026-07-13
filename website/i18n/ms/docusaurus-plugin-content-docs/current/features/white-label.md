---
description: "Penjual semula jenama semula apl — nama produk, logo, favicon, warna, CSS tersuai — melalui konfigurasi penempatan, tanpa perubahan kod. Setiap nilai branding lalai kepada identiti saham…"
---

# Branding white-label

Penjual semula jenama semula apl — nama produk, logo, favicon, warna, CSS tersuai — melalui konfigurasi penempatan, tanpa perubahan kod. Setiap nilai branding **lalai kepada identiti saham**: penempatan yang tidak dikonfigurasi kelihatan sama seperti sebelum ini; penjual semula override hanya apa yang diperlukan.

## Model

- `Core.Options.BrandingOptions` — diikat dari `App:Branding`. Berasaskan rentetan (pinggir konfigurasi); setiap warna disahkan apabila tema dibina.
- `Core.Branding.HexColor` — objek nilai untuk warna CSS hex (`#RGB` / `#RRGGBB`), tidak berubah, mengesahkan dirinya. Warna tidak sah membaling `DomainException` (`domain.branding.color_invalid`) apabila tema dibina — penempatan yang salah konfigurasi gagal cepat pada permulaan, bukan memapar palet yang rosak.
- `Web.Components.Theme.Build(BrandingOptions)` — hasil tema MudBlazor daripada branding. Hanya entri palet berjenama berasal dari konfigurasi; tipografi, reka letak, nada permukaan neutral kekal tetap supaya produk mengekalkan penampilan koheren merentasi penjual semula.
- `Web.Branding.IBrandingThemeProvider` — singleton, bina tema sekali, bina semula pada perubahan pilihan. Disuntik oleh `MainLayout`/`EmptyLayout` untuk `MudThemeProvider`, oleh app bar untuk nama produk/logo. `App.razor` baca `IOptionsMonitor<AppOptions>` direct untuk `<head>` halaman (tajuk, penerangan, favicon, warna tema, CSS tersuai).

## Konfigurasi

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "Description": "AcmeFX — salinan perdagangan dan automasi strategi.",
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

Bentuk pembolehubah env: `App__Branding__ProductName=AcmeFX`, `App__Branding__PrimaryColor=%232D7FF9`.

| Kunci | Kesan | Lalai |
|-----|--------|---------|
| `ProductName` | Teks app bar + `<title>` halaman | `cMind` |
| `LogoUrl` | Imej logo app bar; apabila kosong, nama produk teks ditunjuk | *(kosong)* |
| `FaviconUrl` | `<link rel="icon">` | `favicon.svg` |
| `Description` | `<meta name="description">` | Penerangan saham |
| `PrimaryColor` / `SecondaryColor` | aksen, ikon laci, butang | `#26C281` / `#1FB97A` |
| `AppBarColor` / `BackgroundColor` / `SurfaceColor` | chrome + permukaan; `AppBarColor` memacu `<meta theme-color>` + `theme_color` manifest PWA, `BackgroundColor` manifest `background_color` | palet gelap |
| `SuccessColor` / `ErrorColor` / `WarningColor` / `InfoColor` | warna status | saham |
| `CustomCss` | `<style>` yang disuntik dalam `<head>` (dipercayai penempatan) | *(kosong)* |
| `ShowSiteLink` | tunjuk liaisons "Powered by cMind" pada papan pemuka | `true` |
| `RequireMfa` | perlu setiap pengguna menyediakan pengesahan dua faktor sebelum menggunakan apl | `false` |
| `NodesUi` | berapa banyak permukaan Nod dihantar: `Full` (senarai + tambah/padam manual), `Monitor` (senarai baca sahaja, tiada tambah/padam), `Hidden` (tiada nav, tiada halaman, tiada API manual) | `Full` |
| `RestrictNodesToOwner` | apabila `true`, hanya pemilik boleh lihat/urus nod; jika tidak, seluruh permukaan kakitaban admin-or-above boleh. Pengguna biasa tidak pernah melihat nod sama ada | `false` |

Aset yang dirujuk oleh `LogoUrl`/`FaviconUrl` dihidangkan dari Web apl `wwwroot` (cth mount folder `wwwroot/branding/`) atau mana-mana URL mutlak.

`App:Branding` disahkan pada permulaan (`BrandingOptionsValidator`, dijalankan melalui `ValidateOnStart`): setiap warna mestilah hex yang sah, `CustomCss` tidak boleh mengandungi `<`/`>` (tidak boleh keluar dari tag `<style>`). Penempatan yang salah konfigurasi gagal boot dengan mesej jelas, bukan memapar halaman rosak.

## Pautan powered-by

Papan pemuka memapar liaisons kecil **"Powered by cMind"** yang menunjuk ke tapak dokumentasi projek. Ia dikawal oleh `App:Branding:ShowSiteLink` dan **`true` secara lalai** — penempatan yang tidak dikonfigurasi menunjukkannya. Penjual semula yang mengendalikan instance white-label sepenuhnya menetapkan
`App__Branding__ShowSiteLink=false` untuk mengalihkannya sama sekali.

Pautan dipancarkan oleh komponen papan pemuka dan membaca bendera melalui `IBrandingThemeProvider` /
`BrandingOptions`, jadi Togol nó ialah perubahan konfigurasi sahaja (tiada bina semula). Lihat
[White-label untuk perniagaan](../white-label-for-business.md#the-powered-by-cmind-link) untuk ringkasan yang menghadap perniagaan.

## Senarai benarkan broker

Penempatan white-label boleh mengekang broker mana perdagangan akaunnya boleh ditambah oleh penggunanya — jadi broker
yang mengendalikan cMind untuk klien他自己的nya hanya pernah menghidangkan bukunya sendiri. Dikonfigurasi di bawah `App:Accounts`:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Pepperstone", "IC Markets"]
    }
  }
}
```

Bentuk pembolehubah env: `App__Accounts__AllowedBrokers__0=Pepperstone`.

**Perlakuan:**

- **Senarai kosong (lalai) ⇒ tanpa sekatan.** Setiap broker dibenarkan dan **tiada pengesahan berjalan** — penempatan saham tidak berubah langsung.
- **Bukan kosong ⇒ terhad.** cMind menyemak setiap akaun yang pengguna cuba tambah terhadap senarai (case-insensitive):
  - **Pautan Open API (OAuth)** — nama broker dilaporkan secaraotoritatif oleh cTrader Open API, jadi
    akaun yang tidak dibenarkan hanya **dilangkau** (akaun yang dibenarkan dalam gerlan yang sama masih dipaut); halaman
    kebenaran memberitahu pengguna broker yang mana yang dilangkau.
  - **cID manual (nama pengguna / kata laluan)** — broker yang ditaip pengguna **tidak** dipercayai. cMind **menesahkan**
    broker sebenar akaun dengan menjalankan cBot probe broker yang dihantar melalui cTrader CLI (membaca
    `Account.BrokerName`) dan mengekalkan nama yang disahkan. Broker yang tidak dibenarkan ditolak dengan
    pemberitahuan; kegagalan pengesahan (kredensi buruk, tiada nod, masa tamat) juga dipapar, dan akaun tidak ditambah.

**Model:**

- `Core.Options.AccountsOptions` — diikat dari `App:Accounts` (`AllowedBrokers`, `BrokerProbeTimeout`,
  `BrokerProbeAlgoPath`).
- `Core.Accounts.BrokerName` — objek nilai (dipangkas, kesamaan case-insensitive).
- `Core.Accounts.BrokerAllowlist` — `IsRestricted` / `Allows(broker)`; kosong = benarkan semua. Dikuatkuasakan sebagai
  invariant dalam `CTraderIdAccount.AddTradingAccount` / `LinkOpenApiAccount`
  (`domain.account.broker_not_allowed`).
- `Core.Accounts.IBrokerVerifier` → `Web.Accounts.BrokerVerifier` — menjalankan probe container pada hos web
  (yang mempunyai soket Docker), tail logs, dan menghuraikan broker melalui
  `Core.Accounts.BrokerProbeOutput`. Hanya digunakan apabila senarai benarkan terhad.

**cBot broker-probe:** `broker-probe.algo` pra-binaan dihantar dengan apl Web (`src/Web/BrokerProbe/`,
disalin ke output sebagai `broker-probe/broker-probe.algo`), jadi lalai
`App:Accounts:BrokerProbeAlgoPath` diselesaikan di luar kotak — laluan relatif diselesaikan terhadap direktori asas apl, laluan mutlak digunakan sebagaimana given. Apabila algo tidak wujud, pengesahan cID manual gagal ditutup — akaun di bawah senarai terhad masih boleh dipaut melalui laluan Open API, yang tidak memerlukan probe.

## Senarai benarkan broker — ujian

- **Unit** — `UnitTests/Accounts/`: objek nilai `BrokerName`/`BrokerAllowlist`, penghurai `BrokerProbeOutput`,
  dan invariant senarai benarkan `CTraderIdAccount`.
- **Integrasi** — `IntegrationTests/BrokerAllowlistTests.cs`: titik akhir cID manual dengan verifier palsu
  (tanpa sekatan / disahkan / tidak dibenarkan / pengesahan-gagal) + pemaut Open API melangkau akaun yang tidak dibenarkan.
  `BrokerVerifierLiveTests.cs` menjalankan **probe sebenar** apabila kredensi cID + algo dibekalkan
  (langkau dengan kemas jika tidak).
- **E2E** — `E2ETests/BrokerAllowlistTests.cs`: penempatan terhad menolak tambah manual melalui
  UI sebenar dan mempamerkan pemberitahuan "tidak boleh disahkan" (tiada baris akaun ditambah).

## Kebolehlihatan UI Nod

Nod ialah infrastruktur yang kebanyakan penyewa tidak pernah urus secara manual — ejen CLI cTrader
[mendafarkan diri danHeartbeat sendiri](../operations/node-discovery.md), jadi penempatan white-label boleh menyembunyikan
kawalan manual, atau permukaan Nod seluruhnya, dan masih menjalankan kluster yang sihat melalui
penemuan automatik. Dua kunci branding konfigurasi-only mentadbir nó:

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

Bentuk pembolehubah env: `App__Branding__NodesUi=Hidden`, `App__Branding__RestrictNodesToOwner=true`.

**`NodesUi` — tiga mod:**

- **`Full` (lalai)** — produk saham: senarai nod ditambah kawalan **Nod Baharu** dan **Padam** manual.
  `POST`/`DELETE /api/nodes` berfungsi.
- **`Monitor`** — permukaan baca sahaja: senarai dan statistik langsung kekal, tetapi tambah dan padam manual dialih keluar. `POST`/`DELETE /api/nodes` mengembalikan **404**.
- **`Hidden`** — pautan nav Nod dan halaman hilang sepenuhnya dan laluan halaman mengalihkan ke papan pemuka; tambah/padam API manual mati. Kluster penemuan automatik sahaja.
- **`RestrictNodesToOwner`** lantai siapa boleh lihat dan urus nod. Lalai `false` menyimpan стандартную
  permukaan kakitaban **admin-or-above** (`AdminOrAbove`); tetapkan `true` untuk menjadikannya **pemilik sahaja** (`Owner`). Sama ada
  cara **pengguna biasa tidak pernah melihat nod** — ini hanya memilih antara pemilik sahaja dan permukaan kakitaban yang lebih luas.

**Penemuan automatik tidak terjejas oleh kedua-dua kunci**: titik akhir pendaftaran diri `/api/nodes/register` + heartbeat anonim sentiasa berfungsi, jadi penempatan `Hidden`/`Monitor` masih membesar kluster nó secara automatik.

**Model:**

- `Core.Nodes.NodesUiMode` — `Full` / `Monitor` / `Hidden`.
- `Core.Nodes.NodesUiAccess` — sumber kebenaran tunggal menggabungkan mod + sekatan pemilik:
  `IsPageVisible`, `AllowsManualManagement`, `RequiredPolicy(restrictToOwner)`. Nav
  (`NavMenu.razor`), halaman (`Pages/Nodes.razor`) dan titik akhir (`NodeEndpoints`) semua membacanya supaya UI dan API tidak boleh tidak setuju.
- `Core.Options.BrandingOptions.NodesUi` / `.RestrictNodesToOwner` — diikat dari `App:Branding`.

## Kebolehlihatan UI Nod — ujian

- **Unit** — `UnitTests/Nodes/NodesUiAccessTests.cs`: keterlihatan halaman, pengurusan manual dan
  resolusi dasar yang diperlukan merentasi setiap mod + branding lalai.
- **Integrasi** — `IntegrationTests/NodeUiGatingTests.cs`: melalui HTTP + Postgres sebenar — `Full` membenarkan
  tambah manual, `Monitor`/`Hidden` 404 tambah dan padam, dan `RestrictNodesToOwner` melarang admin sementara pemilik masih membaca senarai.
- **E2E** — `E2ETests/NodesUiTests.cs` (`Full` lalai: pautan nav + halaman + butang Nod Baharu dipapar) dan
  `E2ETests/NodesHiddenTests.cs` (`Hidden`: pautan nav hilang, `/nodes` mengalihkan).

## Token reka (pembolehubah CSS)

Branding juga sampai ke **stylesheet** apl sendiri + komponen tersuai, bukan sahaja MudBlazor. `Web.Branding.BrandingCss.BuildRootVariables(BrandingOptions)` memancarkan palet berjenama sebagai hartanah tersuai CSS pada `:root` (`--app-primary`, `--app-primary-hover`, `--app-surface`, `--app-appbar`, `--app-success`/`--app-error`/`--app-warning`/`--app-info`, …), disuntik dalam `App.razor` sejurus selepas `site.css`. `site.css` dan setiap komponen membaca `var(--app-*)` — **tiada warna yang di-hardcode** — jadi palet penjual semula sampai ke mana-mana (login hero, nav bawah, help tips, halaman offline) secara percuma. Nada permukaan neutral lalai dalam `site.css :root`; `CustomCss` (disuntik terakhir) boleh mengOverride mana-mana token. Lihat [ui-guidelines.md](../ui-guidelines.md) §2.

## PWA berjenama

Apl yang boleh pasang juga berjenama — titik akhir manifest (`/manifest.webmanifest`) dibina dari `BrandingOptions` (`ProductName` → `name`/`short_name`, `Description`, `AppBarColor`/`BackgroundColor` → tema/background). Lihat [pwa.md](pwa.md).

## Ujian

- **Unit** — `UnitTests/Branding/HexColorTests.cs`: pengesahan hex sah/masalah.
- **Integrasi** — `IntegrationTests/ThemeBuildTests.cs`: warna memetakan ke palet, warna tidak sah membaling;
  `IntegrationTests/BrandingHttpTests.cs`: `ProductName`/penerangan/warna-tema tersuai dipapar dalam `<head>` halaman yang dihidangkan (WebApplicationFactory + Postgres), lalai mengekalkan nama saham.
- **E2E** — `E2ETests/BrandingTests.cs`: nama produk berjenama dipapar dalam app bar dalam pelayar sebenar.
