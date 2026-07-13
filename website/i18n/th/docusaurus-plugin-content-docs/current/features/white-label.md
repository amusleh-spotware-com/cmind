---
description: "Reseller rebrand app — product name logo favicon colours custom CSS — ผ่าน deployment config ไม่มี code change ทุก ๆ branding value **defaults เป็น stock identity**: unconfigured deployment look same เป็น ก่อน; reseller override เพียง what ต้อง"
---

# White-label branding

Reseller rebrand app — product name logo favicon colours custom CSS — ผ่าน deployment config ไม่มี code change ทุก ๆ branding value **defaults เป็น stock identity**: unconfigured deployment look same เป็น ก่อน; reseller override เพียง what ต้อง

## Model

- `Core.Options.BrandingOptions` — bound จาก `App:Branding` String-based (config edge); ทุก ๆ colour validated เมื่อ theme built
- `Core.Branding.HexColor` — value object สำหรับ CSS hex colour (`#RGB` / `#RRGGBB`) immutable self-validating invalid colour throws `DomainException` (`domain.branding.color_invalid`) เมื่อ theme built — misconfigured deployment fail fast ที่ startup ไม่ render broken palette
- `Web.Components.Theme.Build(BrandingOptions)` — produce MudBlazor theme จาก branding เพียง branded palette entries มาจาก config; typography layout neutral surface tones stay fixed ดังนั้น product keep coherent look ข้ามบน resellers
- `Web.Branding.IBrandingThemeProvider` — singleton build theme once rebuild บน options change injected โดย `MainLayout`/`EmptyLayout` สำหรับ `MudThemeProvider` โดย app bar สำหรับ product name/logo `App.razor` read `IOptionsMonitor<AppOptions>` direct สำหรับ page `<head>` (title description favicon theme-colour custom CSS)

## Configuration

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "Description": "AcmeFX — copy trading และ strategy automation",
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

Environment-variable form: `App__Branding__ProductName=AcmeFX`, `App__Branding__PrimaryColor=%232D7FF9`

| Key | Effect | Default |
|-----|--------|---------|
| `ProductName` | App-bar text + page `<title>` | `cMind` |
| `LogoUrl` | App-bar logo image; เมื่อ empty product name text shows | *(empty)* |
| `FaviconUrl` | `<link rel="icon">` | `favicon.svg` |
| `Description` | `<meta name="description">` | stock description |
| `PrimaryColor` / `SecondaryColor` | accent drawer icon buttons | `#26C281` / `#1FB97A` |
| `AppBarColor` / `BackgroundColor` / `SurfaceColor` | chrome + surfaces; `AppBarColor` drives `<meta theme-color>` + PWA manifest `theme_color`, `BackgroundColor` the manifest `background_color` | dark palette |
| `SuccessColor` / `ErrorColor` / `WarningColor` / `InfoColor` | status colours | stock |
| `CustomCss` | injected `<style>` ใน `<head>` (deployment-trusted) | *(empty)* |
| `ShowSiteLink` | show "Powered by cMind" credit link บน dashboard | `true` |
| `RequireMfa` | require ทุก ๆ user เพื่อ set ขึ้น two-factor authentication ก่อน using app | `false` |
| `NodesUi` | how much ของ Nodes surface ships: `Full` (list + manual add/delete), `Monitor` (read-only list no add/delete), `Hidden` (no nav no page no manual API) | `Full` |
| `RestrictNodesToOwner` | เมื่อ `true` เพียง owner อาจ see/manage nodes; otherwise ทั้งหมด admin-or-above staff surface สามารถ normal users ไม่เคย see nodes either way | `false` |

Assets referenced โดย `LogoUrl`/`FaviconUrl` served จาก web app `wwwroot` (e.g. mount `wwwroot/branding/` folder) หรือ any absolute URL

`App:Branding` validated ที่ startup (`BrandingOptionsValidator` run ผ่าน `ValidateOnStart`): ทุก ๆ colour ต้องเป็น valid hex `CustomCss` ต้องไม่มี `<`/`>` (ไม่สามารถ break out ของ `<style>` tag) misconfigured deployment fail เพื่อ boot ด้วย clear message ไม่ render broken page

## Powered-by link

dashboard renders small **"Powered by cMind"** credit link ที่ points ไปยัง project's documentation site มัน controlled โดย `App:Branding:ShowSiteLink` และ เป็น **`true` โดย default** — unconfigured deployment แสดง มัน reseller running fully white-labeled instance sets `App__Branding__ShowSiteLink=false` เพื่อ remove มันทั้งหมด

link emitted โดย dashboard component และ reads flag ผ่าน `IBrandingThemeProvider` / `BrandingOptions` ดังนั้น toggling มัน เป็น config-only เปลี่ยน (no rebuild) ดู [White-label สำหรับ business](../white-label-for-business.md#the-powered-by-cmind-link) สำหรับ business-facing summary

## Broker allowlist

white-label deployment สามารถ restrict ผู้ให้บริการใด brokers' trading accounts users อาจ add — ดังนั้น broker running cMind สำหรับ ของเขาเอง clients เพียง ever serves ของเขาเอง book configured ภายใต้ `App:Accounts`:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Pepperstone", "IC Markets"]
    }
  }
}
```

Environment-variable form: `App__Accounts__AllowedBrokers__0=Pepperstone`

**Behaviour:**

- **Empty list (default) ⇒ unrestricted** ทุก ๆ broker allowed และ **ไม่มี verification วิ่ง** — stock deployment ทั้งหมด unchanged
- **Non-empty ⇒ restricted** cMind ตรวจสอบ ทุก ๆ account user ลอง add ต้านแบบ list (case-insensitive):
  - **Open API (OAuth) link** — broker name reported authoritatively โดย cTrader Open API ดังนั้น disallowed account เพียง **skipped** (allowed accounts ใน same grant still link); authorization page บอก user ผู้ให้บริการใด skipped
  - **Manual cID (username / password)** — user-typed broker **ไม่** trusted cMind **verifies** account's real broker โดย running shipped broker-probe cBot ผ่าน cTrader CLI (reading `Account.BrokerName`) และ persists ที่ verified name disallowed broker rejected ด้วย notification; verification failure (bad credentials no node timeout) surfaced ด้วย และ account ไม่ added

**Model:**

- `Core.Options.AccountsOptions` — bound จาก `App:Accounts` (`AllowedBrokers` `BrokerProbeTimeout` `BrokerProbeAlgoPath`)
- `Core.Accounts.BrokerName` — value object (trimmed case-insensitive equality)
- `Core.Accounts.BrokerAllowlist` — `IsRestricted` / `Allows(broker)`; empty = allow ทั้งหมด enforced เป็น invariant ภายใน `CTraderIdAccount.AddTradingAccount` / `LinkOpenApiAccount` (`domain.account.broker_not_allowed`)
- `Core.Accounts.IBrokerVerifier` → `Web.Accounts.BrokerVerifier` — runs probe container บน web host (ซึ่ง มี Docker socket) tails logs และ parses broker ผ่าน `Core.Accounts.BrokerProbeOutput` เพียง invoked เมื่อ allowlist restricted

**Broker-probe cBot:** prebuilt `broker-probe.algo` ships ด้วย web app (`src/Web/BrokerProbe/` copied เป็น output เป็น `broker-probe/broker-probe.algo`) ดังนั้น default `App:Accounts:BrokerProbeAlgoPath` resolves out ของ box — relative path resolved ต้านแบบ app base directory absolute path used เป็น given source อยู่ใน `tools/broker-probe/` เมื่อ algo absent manual-cID verification fails closed — accounts ภายใต้ restricted allowlist still สามารถ linked ผ่าน open API path ซึ่ง needs no probe

## Broker allowlist — tests

- **Unit** — `UnitTests/Accounts/`: `BrokerName`/`BrokerAllowlist` value objects `BrokerProbeOutput` parser และ `CTraderIdAccount` allowlist invariant
- **Integration** — `IntegrationTests/BrokerAllowlistTests.cs`: manual-cID endpoint ด้วย fake verifier (unrestricted / verified / disallowed / verification-failed) + open API linker skipping disallowed accounts `BrokerVerifierLiveTests.cs` runs **real** probe เมื่อ cID creds + algo provided (skips cleanly otherwise)
- **E2E** — `E2ETests/BrokerAllowlistTests.cs`: restricted deployment rejects manual add ผ่าน real UI และ แสดง "couldn't verify" notification (no account row added)

## Nodes UI visibility

nodes เป็น infrastructure ที่สุด tenants ไม่เคย manage โดยมือ — cTrader CLI agents [self-register และ heartbeat](../operations/node-discovery.md) ดังนั้น white-label deployment สามารถ hide manual controls หรือ nodes surface ทั้งหมด และ still run healthy cluster ผ่าน auto-discovery สอง config-only branding keys govern นี้:

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

Environment-variable form: `App__Branding__NodesUi=Hidden`, `App__Branding__RestrictNodesToOwner=true`

**`NodesUi` — three modes:**

- **`Full` (default)** — stock product: node list บวก manual **New Node** และ **Delete** controls `POST`/`DELETE /api/nodes` work
- **`Monitor`** — read-only surface: list และ live stats stay แต่ manual add และ delete removed nodes เพียง ever appear ผ่าน auto-discovery `POST`/`DELETE /api/nodes` return **404**
- **`Hidden`** — nodes nav link และ page หายไป ทั้งหมด และ page route redirects เป็น dashboard; manual add/delete API off cluster เป็น auto-discovery เพียง

**`RestrictNodesToOwner`** floors ใคร อาจ see และ manage nodes default `false` keeps standard **admin-or-above** staff surface (`AdminOrAbove`); ตั้ง `true` เพื่อ ทำให้มัน **owner-only** (`Owner`) either way **normal users ไม่เคย see nodes** — นี้ เพียง เลือก ระหว่าง owner-only และ wider staff surface

node **auto-discovery unaffected โดย ทั้งสอง keys**: anonymous `POST /api/nodes/register` self-register + heartbeat endpoint always works ดังนั้น `Hidden`/`Monitor` deployment still grows cluster automatically

**Model:**

- `Core.Nodes.NodesUiMode` — `Full` / `Monitor` / `Hidden`
- `Core.Nodes.NodesUiAccess` — single source truth composing mode + owner-restriction: `IsPageVisible`, `AllowsManualManagement`, `RequiredPolicy(restrictToOwner)` nav (`NavMenu.razor`) page (`Pages/Nodes.razor`) และ endpoints (`NodeEndpoints`) ทั้งหมด read มัน ดังนั้น UI และ API ไม่เคย disagree
- `Core.Options.BrandingOptions.NodesUi` / `.RestrictNodesToOwner` — bound จาก `App:Branding`

## Nodes UI visibility — tests

- **Unit** — `UnitTests/Nodes/NodesUiAccessTests.cs`: page-visibility manual-management และ required-policy resolution ข้ามบน ทุก ๆ mode + default branding
- **Integration** — `IntegrationTests/NodeUiGatingTests.cs`: บน real HTTP + Postgres — `Full` allows manual add `Monitor`/`Hidden` 404 add และ delete และ `RestrictNodesToOwner` forbids admin ขณะ owner still reads list
- **E2E** — `E2ETests/NodesUiTests.cs` (default `Full`: nav link + page + new node button render) และ `E2ETests/NodesHiddenTests.cs` (`Hidden`: nav link หายไป `/nodes` redirects)

## Design tokens (CSS variables)

branding ด้วย reaches app's **own** stylesheet + custom components ไม่ just mudblazor `Web.Branding.BrandingCss.BuildRootVariables(BrandingOptions)` emits branded palette เป็น CSS custom properties บน `:root` (`--app-primary` `--app-primary-hover` `--app-surface` `--app-appbar` `--app-success`/`--app-error`/`--app-warning`/`--app-info` …) injected ใน `App.razor` right หลัง `site.css` `site.css` และ ทุก ๆ component read `var(--app-*)` — **ไม่มี hard-coded colours** — ดังนั้น reseller's palette ไหล ทุกที่ (login hero bottom nav help tips offline page) สำหรับ free neutral surface tones default ใน `site.css :root`; `CustomCss` (injected last) สามารถ override any token ดู [ui-guidelines.md](../ui-guidelines.md) §2

## Branded PWA

installable app branded ด้วย — manifest endpoint (`/manifest.webmanifest`) built จาก `BrandingOptions` (`ProductName` → `name`/`short_name`, `Description`, `AppBarColor`/`BackgroundColor` → theme/background) ดู [pwa.md](pwa.md)

## Tests

- **Unit** — `UnitTests/Branding/HexColorTests.cs`: valid/invalid hex validation
- **Integration** — `IntegrationTests/ThemeBuildTests.cs`: colours map เป็น palette invalid colour throws; `IntegrationTests/BrandingHttpTests.cs`: custom `ProductName`/description/theme-colour render ใน served page `<head>` (WebApplicationFactory + Postgres) defaults keep stock name
- **E2E** — `E2ETests/BrandingTests.cs`: branded product name renders ใน app bar ใน real browser
