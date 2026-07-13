---
description: "Reseller rebrand app — product name, logo, favicon, colours, custom CSS — poprzez deployment config, brak code change. Każda branding wartość domyślnie do stock…"
---

# White-label branding

Reseller rebrand app — product name, logo, favicon, colours, custom CSS — poprzez deployment config,
brak code change. Każda branding wartość **domyślnie do stock identity**: unconfigured deployment
wygląda tak samo jak zanim; reseller override tylko co potrzebuje.

## Model

- `Core.Options.BrandingOptions` — bound z `App:Branding`. String-based (config edge); każdy colour
  validated gdy theme built.
- `Core.Branding.HexColor` — value object dla CSS hex colour (`#RGB` / `#RRGGBB`), immutable,
  self-validating. Invalid colour throws `DomainException` (`domain.branding.color_invalid`) gdy
  theme built — misconfigured deployment fail fast na startup, nie render broken palette.
- `Web.Components.Theme.Build(BrandingOptions)` — produce MudBlazor theme z branding. Tylko branded
  palette entries pochodzą z config; typography, layout, neutral surface tones stay fixed tak
  product keep coherent look across resellers.
- `Web.Branding.IBrandingThemeProvider` — singleton, build theme raz, rebuild na options change.
  Injected przez `MainLayout`/`EmptyLayout` dla `MudThemeProvider`, przez app bar dla product
  name/logo. `App.razor` read `IOptionsMonitor<AppOptions>` direct dla page `<head>` (title,
  description, favicon, theme-colour, custom CSS).

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

Environment-variable forma: `App__Branding__ProductName=AcmeFX`, `App__Branding__PrimaryColor=%232D7FF9`.

| Key | Effect | Domyślnie |
|-----|--------|---------|
| `ProductName` | App-bar text + page `<title>` | `cMind` |
| `LogoUrl` | App-bar logo image; gdy empty, product name text shows | *(empty)* |
| `FaviconUrl` | `<link rel="icon">` | `favicon.svg` |
| `Description` | `<meta name="description">` | stock description |
| `PrimaryColor` / `SecondaryColor` | accent, drawer icon, buttons | `#26C281` / `#1FB97A` |
| `AppBarColor` / `BackgroundColor` / `SurfaceColor` | chrome + surfaces; `AppBarColor` drives `<meta theme-color>` + PWA manifest `theme_color`, `BackgroundColor` manifest `background_color` | dark palette |
| `SuccessColor` / `ErrorColor` / `WarningColor` / `InfoColor` | status colours | stock |
| `CustomCss` | injected `<style>` w `<head>` (deployment-trusted) | *(empty)* |
| `ShowSiteLink` | show "Powered by cMind" credit link na dashboard | `true` |
| `RequireMfa` | require każdy user setup two-factor authentication zanim używania app | `false` |
| `NodesUi` | ile Nodes surface wysyła: `Full` (list + manual add/delete), `Monitor` (read-only list, brak add/delete), `Hidden` (brak nav, brak page, brak manual API) | `Full` |
| `RestrictNodesToOwner` | gdy `true`, tylko owner mogą widzieć/zarządzać nodes; w innym wypadku cały admin-or-above staff surface mogą. Normalni users nigdy nie widzą nodes w żadnym wypadku | `false` |

Assets referenced przez `LogoUrl`/`FaviconUrl` served z Web app `wwwroot` (np. mount `wwwroot/branding/`
folder) albo każdy absolute URL.

`App:Branding` validated na startup (`BrandingOptionsValidator`, run poprzez `ValidateOnStart`):
każdy colour musi być valid hex, `CustomCss` musi nie zawierać `<`/`>` (nie może break out z `<style>`
tag). Misconfigured deployment fail do boot z clear message, nie render broken strona.

## Powered-by link

Dashboard renderuje mały **"Powered by cMind"** credit link który wskazuje na project's
documentation site. Jest to controlled przez `App:Branding:ShowSiteLink` i jest **`true` domyślnie** —
unconfigured deployment pokazuje to. Reseller uruchamiając fully white-labeled instancję ustawia
`App__Branding__ShowSiteLink=false` aby usunąć to całkowicie.

Link jest emitted przez dashboard component i czyta flag poprzez `IBrandingThemeProvider` /
`BrandingOptions`, więc toggling to jest config-only zmiana (brak rebuild). Zobacz
[White-label dla business](../white-label-for-business.md#the-powered-by-cmind-link) dla
business-facing summary.

## Broker allowlist

White-label deployment może restrict które brokery' trading accounts jego users mogą dodać — więc
broker uruchamiający cMind dla jego własnych clients tylko ever serves jego własną book. Configured
under `App:Accounts`:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Pepperstone", "IC Markets"]
    }
  }
}
```

Environment-variable forma: `App__Accounts__AllowedBrokers__0=Pepperstone`.

**Zachowanie:**

- **Empty list (domyślnie) ⇒ unrestricted.** Każdy broker jest allowed i **brak verification runs** —
  stock deployment jest kompletnie bez zmian.
- **Non-empty ⇒ restricted.** cMind checks każdy account użytkownik próbuje dodać przeciwko list
  (case-insensitive):
  - **Open API (OAuth) link** — broker name jest reported authoritatively przez cTrader Open API, więc
    disallowed account jest po prostu **skipped** (allowed accounts w ten sam grant ciągle link);
    authorization strona mówi użytkownikowi które brokery były skipped.
  - **Manual cID (username / password)** — user-typed broker jest **nie** trusted. cMind **verifies**
    account'a rzeczywisty broker poprzez running shipped broker-probe cBot poprzez cTrader CLI (reading
    `Account.BrokerName`) i persists tę zweryfikowaną name. Disallowed broker jest rejected z
    notification; verification failure (bad credentials, brak node, timeout) jest surfaced też, i
    account nie jest added.

**Model:**

- `Core.Options.AccountsOptions` — bound z `App:Accounts` (`AllowedBrokers`, `BrokerProbeTimeout`,
  `BrokerProbeAlgoPath`).
- `Core.Accounts.BrokerName` — value object (trimmed, case-insensitive equality).
- `Core.Accounts.BrokerAllowlist` — `IsRestricted` / `Allows(broker)`; empty = allow all. Enforced
  jako invariant inside `CTraderIdAccount.AddTradingAccount` / `LinkOpenApiAccount`
  (`domain.account.broker_not_allowed`).
- `Core.Accounts.IBrokerVerifier` → `Web.Accounts.BrokerVerifier` — runs probe container na web
  host (który ma Docker socket), tails logs, i parses broker poprzez
  `Core.Accounts.BrokerProbeOutput`. Tylko invoked gdy allowlist jest restricted.

**Broker-probe cBot:** prebuilt `broker-probe.algo` wysyła z Web app (`src/Web/BrokerProbe/`,
copied do output jako `broker-probe/broker-probe.algo`), więc default
`App:Accounts:BrokerProbeAlgoPath` resolves out of the box — relative path jest resolved przeciwko
app base directory, absolute path jest używany jako given. Source żyje w `tools/broker-probe/`.
Gdy algo jest absent, manual-cID verification fails closed — accounts under restricted allowlist
mogą ciągle być linked poprzez Open API path, który potrzebuje brak probe.

## Broker allowlist — tests

- **Unit** — `UnitTests/Accounts/`: `BrokerName`/`BrokerAllowlist` value objects, `BrokerProbeOutput`
  parser, i `CTraderIdAccount` allowlist invariant.
- **Integration** — `IntegrationTests/BrokerAllowlistTests.cs`: manual-cID endpoint z fake verifier
  (unrestricted / verified / disallowed / verification-failed) + Open API linker skipping disallowed
  accounts. `BrokerVerifierLiveTests.cs` runs **real** probe gdy cID creds + algo są provided
  (skips cleanly w innym wypadku).
- **E2E** — `E2ETests/BrokerAllowlistTests.cs`: restricted deployment rejects manual add poprzez
  real UI i shows "couldn't verify" notification (brak account row added).

## Nodes UI visibility

Nodes są infrastructure większość tenants nigdy nie zarządzać ręcznie — cTrader CLI agents
[self-register i heartbeat](../operations/node-discovery.md), więc white-label deployment może
hide manual controls, albo Nodes surface całkowicie, i ciągle uruchamiać healthy cluster poprzez
auto-discovery. Dwa config-only branding keys kierują to:

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

Environment-variable forma: `App__Branding__NodesUi=Hidden`, `App__Branding__RestrictNodesToOwner=true`.

**`NodesUi` — trzy modes:**

- **`Full` (domyślnie)** — stock product: node list plus manual **New Node** i **Delete**
  controls. `POST`/`DELETE /api/nodes` work.
- **`Monitor`** — read-only surface: list i live stats stay, ale manual add i delete są
  removed. Nodes tylko ever pojawić się poprzez auto-discovery. `POST`/`DELETE /api/nodes`
  zwracają **404**.
- **`Hidden`** — Nodes nav link i strona są gone całkowicie i strona route redirects do
  dashboard; manual add/delete API jest off. Cluster jest auto-discovery tylko.

**`RestrictNodesToOwner`** floors kto mogą widzieć i zarządzać nodes. Default `false` keeps standard
**admin-or-above** staff surface (`AdminOrAbove`); ustawić `true` aby make to **owner-only** (`Owner`).
W każdym wypadku **normalni users nigdy nie widzą nodes** — to tylko wybiera między owner-only i
wider staff surface.

Node **auto-discovery jest unaffected przez obu keys**: anonymous `POST /api/nodes/register` self-register
+ heartbeat endpoint zawsze works, więc `Hidden`/`Monitor` deployment ciągle grows cluster
automatycznie.

**Model:**

- `Core.Nodes.NodesUiMode` — `Full` / `Monitor` / `Hidden`.
- `Core.Nodes.NodesUiAccess` — single source of truth composing mode + owner-restriction:
  `IsPageVisible`, `AllowsManualManagement`, `RequiredPolicy(restrictToOwner)`. Nav
  (`NavMenu.razor`), strona (`Pages/Nodes.razor`) i endpoints (`NodeEndpoints`) wszystkie czytają
  to więc UI i API nigdy nie mogą się różnić.
- `Core.Options.BrandingOptions.NodesUi` / `.RestrictNodesToOwner` — bound z `App:Branding`.

## Nodes UI visibility — tests

- **Unit** — `UnitTests/Nodes/NodesUiAccessTests.cs`: page-visibility, manual-management i
  required-policy resolution across każdy mode + domyślny branding.
- **Integration** — `IntegrationTests/NodeUiGatingTests.cs`: nad real HTTP + Postgres — `Full`
  pozwala manual add, `Monitor`/`Hidden` 404 add i delete, i `RestrictNodesToOwner` forbids admin
  podczas owner ciągle czyta list.
- **E2E** — `E2ETests/NodesUiTests.cs` (domyślnie `Full`: nav link + strona + New Node button
  render) i `E2ETests/NodesHiddenTests.cs` (`Hidden`: nav link gone, `/nodes` redirects).

## Design tokens (CSS variables)

Branding również reach app'a **własna** stylesheet + custom components, nie tylko MudBlazor.
`Web.Branding.BrandingCss.BuildRootVariables(BrandingOptions)` emits branded palette jako CSS
custom properties na `:root` (`--app-primary`, `--app-primary-hover`, `--app-surface`, `--app-appbar`,
`--app-success`/`--app-error`/`--app-warning`/`--app-info`, …), injected w `App.razor` right po
`site.css`. `site.css` i każdy component czytają `var(--app-*)` — **brak hard-coded colours** —
więc reseller palette flows wszędzie (login hero, bottom nav, help tips, offline strona) dla
wolne. Neutral surface tones default w `site.css :root`; `CustomCss` (injected last) może override
każdy token. Zobacz [ui-guidelines.md](../ui-guidelines.md) §2.

## Branded PWA

Installable app jest branded też — manifest endpoint (`/manifest.webmanifest`) jest built z
`BrandingOptions` (`ProductName` → `name`/`short_name`, `Description`, `AppBarColor`/`BackgroundColor`
→ theme/background). Zobacz [pwa.md](pwa.md).

## Tests

- **Unit** — `UnitTests/Branding/HexColorTests.cs`: valid/invalid hex validation.
- **Integration** — `IntegrationTests/ThemeBuildTests.cs`: colours mapują do palette, invalid colour
  throws; `IntegrationTests/BrandingHttpTests.cs`: custom `ProductName`/description/theme-colour
  render w served strona `<head>` (WebApplicationFactory + Postgres), domyślnie keep stock name.
- **E2E** — `E2ETests/BrandingTests.cs`: branded product name renderuje w app bar w real browser.
