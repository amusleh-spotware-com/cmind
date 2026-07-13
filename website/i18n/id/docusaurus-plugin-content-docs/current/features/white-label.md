---
description: "Reseller rebrand app — product name, logo, favicon, colours, custom CSS — melalui deployment config, tidak ada code change. Setiap branding value defaults ke stock…"
---

# White-label branding

Reseller rebrand app — product name, logo, favicon, colours, custom CSS — melalui deployment config, tidak ada code change. Setiap branding value **defaults ke stock identity**: unconfigured deployment terlihat sama seperti sebelumnya; reseller override hanya apa yang dibutuhkan.

## Model

- `Core.Options.BrandingOptions` — bound dari `App:Branding`. String-based (config edge); setiap colour divalidasi ketika theme dibangun.
- `Core.Branding.HexColor` — value object untuk CSS hex colour (`#RGB` / `#RRGGBB`), immutable, self-validating.
  Invalid colour throws `DomainException` (`domain.branding.color_invalid`) ketika theme dibangun — misconfigured deployment fail fast pada startup, tidak render palette yang rusak.
- `Web.Components.Theme.Build(BrandingOptions)` — menghasilkan MudBlazor theme dari branding. Hanya branded palette entries berasal dari config; typography, layout, neutral surface tones tetap fixed jadi produk maintain coherent look di seluruh reseller.
- `Web.Branding.IBrandingThemeProvider` — singleton, build theme once, rebuild pada options change.
  Injected oleh `MainLayout`/`EmptyLayout` untuk `MudThemeProvider`, oleh app bar untuk product name/logo. `App.razor` read `IOptionsMonitor<AppOptions>` direct untuk page `<head>` (title, description, favicon, theme-colour, custom CSS).

## Configuration

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

Environment-variable form: `App__Branding__ProductName=AcmeFX`, `App__Branding__PrimaryColor=%232D7FF9`.

| Key | Effect | Default |
|-----|--------|---------|
| `ProductName` | App-bar text + page `<title>` | `cMind` |
| `LogoUrl` | App-bar logo image; ketika kosong, product name text menampilkan | *(kosong)* |
| `FaviconUrl` | `<link rel="icon">` | `favicon.svg` |
| `Description` | `<meta name="description">` | stock description |
| `PrimaryColor` / `SecondaryColor` | accent, drawer icon, buttons | `#26C281` / `#1FB97A` |
| `AppBarColor` / `BackgroundColor` / `SurfaceColor` | chrome + surfaces; `AppBarColor` drives `<meta theme-color>` + PWA manifest `theme_color`, `BackgroundColor` manifest `background_color` | dark palette |
| `SuccessColor` / `ErrorColor` / `WarningColor` / `InfoColor` | status colours | stock |
| `CustomCss` | injected `<style>` dalam `<head>` (deployment-trusted) | *(kosong)* |
| `ShowSiteLink` | tampilkan "Powered by cMind" credit link pada dashboard | `true` |
| `RequireMfa` | require setiap user untuk setup two-factor authentication sebelum menggunakan app | `false` |
| `NodesUi` | seberapa banyak Nodes surface dikirim: `Full` (list + manual add/delete), `Monitor` (read-only list, tidak ada add/delete), `Hidden` (tidak ada nav, tidak ada page, tidak ada manual API) | `Full` |
| `RestrictNodesToOwner` | ketika `true`, hanya owner yang dapat see/manage nodes; sebaliknya seluruh admin-or-above staff surface dapat. Normal users tidak pernah melihat nodes baik caranya | `false` |

Assets yang direferensikan oleh `LogoUrl`/`FaviconUrl` disajikan dari Web app `wwwroot` (misalnya mount `wwwroot/branding/` folder) atau URL absolute apa pun.

`App:Branding` divalidasi pada startup (`BrandingOptionsValidator`, run via `ValidateOnStart`): setiap colour harus valid hex, `CustomCss` tidak boleh berisi `<`/`>` (tidak dapat break out dari `<style>` tag). Misconfigured deployment fail boot dengan clear message, tidak render broken page.

## Link Powered-by

Dashboard merender kecil **"Powered by cMind"** credit link yang menunjuk ke dokumentasi proyek
site. Dikontrol oleh `App:Branding:ShowSiteLink` dan adalah **`true` secara default** — unconfigured
deployment menampilkannya. Reseller yang menjalankan fully white-labeled instance mengatur
`App__Branding__ShowSiteLink=false` untuk menghapusnya sepenuhnya.

Link diemit oleh dashboard component dan membaca flag melalui `IBrandingThemeProvider` /
`BrandingOptions`, jadi toggling itu adalah config-only change (tidak ada rebuild). Lihat
[White-label for business](../white-label-for-business.md#the-powered-by-cmind-link) untuk business-facing
summary.

## Broker allowlist

Deployment white-label dapat membatasi broker mana yang trading accounts pengguna dapat menambahkan — jadi broker
yang menjalankan cMind untuk klien mereka sendiri hanya melayani buku mereka sendiri. Dikonfigurasi di bawah `App:Accounts`:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Pepperstone", "IC Markets"]
    }
  }
}
```

Environment-variable form: `App__Accounts__AllowedBrokers__0=Pepperstone`.

**Behaviour:**

- **Empty list (default) ⇒ unrestricted.** Setiap broker diizinkan dan **tidak ada verification runs** — stock
  deployment sepenuhnya tidak berubah.
- **Non-empty ⇒ restricted.** cMind memeriksa setiap account yang pengguna coba tambahkan terhadap daftar
  (case-insensitive):
  - **Open API (OAuth) link** — broker name dilaporkan secara authoritatively oleh cTrader Open API, jadi
    disallowed account adalah **dilewati** (allowed accounts dalam grant yang sama masih link); halaman
    authorization menceritakan pengguna broker mana yang dilewati.
  - **Manual cID (username / password)** — broker yang diketikkan pengguna **tidak** dipercaya. cMind **verifies**
    account's real broker dengan menjalankan shipped broker-probe cBot melalui cTrader CLI (membaca
    `Account.BrokerName`) dan persists nama yang diverifikasi. Disallowed broker ditolak dengan notifikasi;
    verification failure (bad credentials, tidak ada node, timeout) juga dipermukaan, dan account tidak ditambahkan.

**Model:**

- `Core.Options.AccountsOptions` — bound dari `App:Accounts` (`AllowedBrokers`, `BrokerProbeTimeout`,
  `BrokerProbeAlgoPath`).
- `Core.Accounts.BrokerName` — value object (trimmed, case-insensitive equality).
- `Core.Accounts.BrokerAllowlist` — `IsRestricted` / `Allows(broker)`; kosong = allow all. Diterapkan sebagai
  invariant di dalam `CTraderIdAccount.AddTradingAccount` / `LinkOpenApiAccount`
  (`domain.account.broker_not_allowed`).
- `Core.Accounts.IBrokerVerifier` → `Web.Accounts.BrokerVerifier` — menjalankan probe container pada web
  host (yang memiliki Docker socket), tails logs, dan parses broker via
  `Core.Accounts.BrokerProbeOutput`. Hanya diinvoke ketika allowlist restricted.

**Broker-probe cBot:** prebuilt `broker-probe.algo` dikirim dengan Web app (`src/Web/BrokerProbe/`,
disalin ke output sebagai `broker-probe/broker-probe.algo`), jadi default
`App:Accounts:BrokerProbeAlgoPath` resolves out of the box — relative path diresolve terhadap app
base directory, absolute path digunakan sebagai given. Source tinggal dalam `tools/broker-probe/`. Ketika
algo tidak ada, manual-cID verification gagal closed — accounts di bawah restricted allowlist masih dapat
linked via Open API path, yang tidak memerlukan probe.

## Broker allowlist — tests

- **Unit** — `UnitTests/Accounts/`: `BrokerName`/`BrokerAllowlist` value objects, `BrokerProbeOutput`
  parser, dan `CTraderIdAccount` allowlist invariant.
- **Integration** — `IntegrationTests/BrokerAllowlistTests.cs`: manual-cID endpoint dengan fake verifier
  (unrestricted / verified / disallowed / verification-failed) + Open API linker skipping disallowed
  accounts. `BrokerVerifierLiveTests.cs` menjalankan **real** probe ketika cID creds + algo disediakan
  (melewati cleanly sebaliknya).
- **E2E** — `E2ETests/BrokerAllowlistTests.cs`: restricted deployment menolak manual add melalui
  real UI dan menampilkan "couldn't verify" notification (tidak ada account row ditambahkan).

## Nodes UI visibility

Nodes adalah infrastructure sebagian besar tenants tidak pernah kelola dengan tangan — cTrader CLI agents
[self-register dan heartbeat](../operations/node-discovery.md), jadi white-label deployment dapat hide manual
controls, atau Nodes surface sepenuhnya, dan tetap jalankan healthy cluster melalui auto-discovery.
Dua config-only branding keys mengatur ini:

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

Environment-variable form: `App__Branding__NodesUi=Hidden`, `App__Branding__RestrictNodesToOwner=true`.

**`NodesUi` — tiga mode:**

- **`Full` (default)** — stock product: node list ditambah manual **New Node** dan **Delete**
  controls. `POST`/`DELETE /api/nodes` bekerja.
- **`Monitor`** — read-only surface: list dan live stats tetap, tetapi manual add dan delete
  dihapus. Nodes hanya pernah muncul melalui auto-discovery. `POST`/`DELETE /api/nodes` return **404**.
- **`Hidden`** — Nodes nav link dan page hilang sepenuhnya dan page route redirects ke
  dashboard; manual add/delete API adalah off. Cluster adalah auto-discovery hanya.

**`RestrictNodesToOwner`** floors siapa yang dapat see dan manage nodes. Default `false` menjaga standard
**admin-or-above** staff surface (`AdminOrAbove`); set `true` untuk membuat itu **owner-only** (`Owner`). Either
way **normal users tidak pernah melihat nodes** — ini hanya memilih antara owner-only dan wider staff surface.

Node **auto-discovery tidak terpengaruh oleh kedua keys**: anonymous `POST /api/nodes/register` self-register
+ heartbeat endpoint selalu bekerja, jadi `Hidden`/`Monitor` deployment masih tumbuh klusternya
secara otomatis.

**Model:**

- `Core.Nodes.NodesUiMode` — `Full` / `Monitor` / `Hidden`.
- `Core.Nodes.NodesUiAccess` — single source of truth composing mode + owner-restriction:
  `IsPageVisible`, `AllowsManualManagement`, `RequiredPolicy(restrictToOwner)`. Nav
  (`NavMenu.razor`), page (`Pages/Nodes.razor`) dan endpoints (`NodeEndpoints`) semua membacanya jadi
  UI dan API tidak pernah tidak setuju.
- `Core.Options.BrandingOptions.NodesUi` / `.RestrictNodesToOwner` — bound dari `App:Branding`.

## Nodes UI visibility — tests

- **Unit** — `UnitTests/Nodes/NodesUiAccessTests.cs`: page-visibility, manual-management dan
  required-policy resolution di seluruh setiap mode + default branding.
- **Integration** — `IntegrationTests/NodeUiGatingTests.cs`: over real HTTP + Postgres — `Full` memungkinkan
  manual add, `Monitor`/`Hidden` 404 add dan delete, dan `RestrictNodesToOwner` melarang admin sementara
  owner masih membaca list.
- **E2E** — `E2ETests/NodesUiTests.cs` (default `Full`: nav link + page + New Node button render) dan
  `E2ETests/NodesHiddenTests.cs` (`Hidden`: nav link gone, `/nodes` redirects).

## Design tokens (CSS variables)

Branding juga mencapai aplikasi **sendiri** stylesheet + custom components, tidak hanya MudBlazor. `Web.Branding.BrandingCss.BuildRootVariables(BrandingOptions)` emits branded palette sebagai CSS custom properties pada `:root` (`--app-primary`, `--app-primary-hover`, `--app-surface`, `--app-appbar`, `--app-success`/`--app-error`/`--app-warning`/`--app-info`, …), injected dalam `App.razor` right after `site.css`. `site.css` dan setiap component membaca `var(--app-*)` — **tidak ada hard-coded colours** — jadi reseller's palette flows di mana-mana (login hero, bottom nav, help tips, offline page) untuk gratis. Neutral surface tones default dalam `site.css :root`; `CustomCss` (injected last) dapat override token apa pun. Lihat [ui-guidelines.md](../ui-guidelines.md) §2.

## Branded PWA

Aplikasi yang dapat diinstal juga di-brand — manifest endpoint (`/manifest.webmanifest`) dibangun dari `BrandingOptions` (`ProductName` → `name`/`short_name`, `Description`, `AppBarColor`/`BackgroundColor` → theme/background). Lihat [pwa.md](pwa.md).

## Tests

- **Unit** — `UnitTests/Branding/HexColorTests.cs`: valid/invalid hex validation.
- **Integration** — `IntegrationTests/ThemeBuildTests.cs`: colours map into palette, invalid colour throws;
  `IntegrationTests/BrandingHttpTests.cs`: custom `ProductName`/description/theme-colour render dalam served page `<head>` (WebApplicationFactory + Postgres), defaults menjaga stock name.
- **E2E** — `E2ETests/BrandingTests.cs`: branded product name renders dalam app bar dalam real browser.
