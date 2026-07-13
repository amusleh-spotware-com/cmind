---
description: "Reseller rebrand app — product name, logo, favicon, colours, custom CSS — via deployment config, no code change. Every branding value defaults to stock…"
---

# White-label branding

Reseller rebrand app — product name, logo, favicon, colours, custom CSS — via deployment config, no code change. Mọi branding value **defaults to stock identity**: unconfigured deployment look same as before; reseller override only what need.

## Model

- `Core.Options.BrandingOptions` — bound from `App:Branding`. String-based (config edge); each colour validated when theme built.
- `Core.Branding.HexColor` — value object for CSS hex colour (`#RGB` / `#RRGGBB`), immutable, self-validating.
  Invalid colour throws `DomainException` (`domain.branding.color_invalid`) when theme built — misconfigured deployment fail fast at startup, not render broken palette.
- `Web.Components.Theme.Build(BrandingOptions)` — produce MudBlazor theme from branding. Only branded palette entries come from config; typography, layout, neutral surface tones stay fixed vì vậy product keep coherent look across resellers.
- `Web.Branding.IBrandingThemeProvider` — singleton, build theme once, rebuild on options change.
  Injected by `MainLayout`/`EmptyLayout` for `MudThemeProvider`, by app bar for product name/logo. `App.razor` read `IOptionsMonitor<AppOptions>` direct for page `<head>` (title, description, favicon, theme-colour, custom CSS).

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
| `LogoUrl` | App-bar logo image; when empty, product name text shows | *(empty)* |
| `FaviconUrl` | `<link rel="icon">` | `favicon.svg` |
| `Description` | `<meta name="description">` | stock description |
| `PrimaryColor` / `SecondaryColor` | accent, drawer icon, buttons | `#26C281` / `#1FB97A` |
| `AppBarColor` / `BackgroundColor` / `SurfaceColor` | chrome + surfaces; `AppBarColor` drives `<meta theme-color>` + PWA manifest `theme_color`, `BackgroundColor` manifest `background_color` | dark palette |
| `SuccessColor` / `ErrorColor` / `WarningColor` / `InfoColor` | status colours | stock |
| `CustomCss` | injected `<style>` in `<head>` (deployment-trusted) | *(empty)* |
| `ShowSiteLink` | show "Powered by cMind" credit link on dashboard | `true` |
| `RequireMfa` | require every user to set up two-factor authentication before using app | `false` |
| `NodesUi` | how much of Nodes surface ships: `Full` (list + manual add/delete), `Monitor` (read-only list, no add/delete), `Hidden` (no nav, no page, no manual API) | `Full` |
| `RestrictNodesToOwner` | when `true`, only owner may see/manage nodes; otherwise whole admin-or-above staff surface can. Normal users never see nodes either way | `false` |

Assets referenced by `LogoUrl`/`FaviconUrl` served from Web app `wwwroot` (ví dụ mount `wwwroot/branding/` folder) or any absolute URL.

`App:Branding` validated at startup (`BrandingOptionsValidator`, run via `ValidateOnStart`): every colour must be valid hex, `CustomCss` must not contain `<`/`>` (cannot break out of `<style>` tag). Misconfigured deployment fail to boot với clear message, not render broken page.

## Powered-by link

Dashboard renders small **"Powered by cMind"** credit link trỏ đến project's
documentation site. Nó controlled by `App:Branding:ShowSiteLink` và là **`true` by default** —
unconfigured deployment shows it. A reseller running fully white-labeled instance sets
`App__Branding__ShowSiteLink=false` để remove it entirely.

Link emitted by dashboard component và reads flag through `IBrandingThemeProvider` /
`BrandingOptions`, vì vậy toggling it là config-only change (no rebuild). Xem
[White-label for business](../white-label-for-business.md#the-powered-by-cmind-link) cho
business-facing summary.

## Broker allowlist

A white-label deployment có thể restrict which brokers' trading accounts its users may add — vì vậy a broker
running cMind for its own clients only ever serves its own book. Configured under `App:Accounts`:

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

- **Empty list (default) ⇒ unrestricted.** Every broker allowed và **no verification runs** —
  stock deployment completely unchanged.
- **Non-empty ⇒ restricted.** cMind checks every account a user tries to add against list
  (case-insensitive):
  - **Open API (OAuth) link** — broker name reported authoritatively by cTrader Open API, vì vậy
    a disallowed account đơn giản **skipped** (allowed accounts in same grant still link);
    authorization page tells user which brokers were skipped.
  - **Manual cID (username / password)** — user-typed broker **not** trusted. cMind **verifies**
    account's real broker by running shipped broker-probe cBot through cTrader CLI (reading
    `Account.BrokerName`) và persists that verified name. A disallowed broker rejected với a
    notification; a verification failure (bad credentials, no node, timeout) surfaced too, và account
    not added.

**Model:**

- `Core.Options.AccountsOptions` — bound from `App:Accounts` (`AllowedBrokers`, `BrokerProbeTimeout`,
  `BrokerProbeAlgoPath`).
- `Core.Accounts.BrokerName` — value object (trimmed, case-insensitive equality).
- `Core.Accounts.BrokerAllowlist` — `IsRestricted` / `Allows(broker)`; empty = allow all. Enforced as
  invariant inside `CTraderIdAccount.AddTradingAccount` / `LinkOpenApiAccount`
  (`domain.account.broker_not_allowed`).
- `Core.Accounts.IBrokerVerifier` → `Web.Accounts.BrokerVerifier` — runs probe container on web
  host (which has Docker socket), tails logs, và parses broker via
  `Core.Accounts.BrokerProbeOutput`. Only invoked when allowlist restricted.

**Broker-probe cBot:** a prebuilt `broker-probe.algo` ships with Web app (`src/Web/BrokerProbe/`,
copied to output as `broker-probe/broker-probe.algo`), vì vậy default
`App:Accounts:BrokerProbeAlgoPath` resolves out of the box — a relative path resolved against app
base directory, an absolute path used as given. When algo absent, manual-cID verification fails closed — accounts under a restricted allowlist can still be
linked via Open API path, which needs no probe.

## Broker allowlist — tests

- **Unit** — `UnitTests/Accounts/`: `BrokerName`/`BrokerAllowlist` value objects, `BrokerProbeOutput`
  parser, và `CTraderIdAccount` allowlist invariant.
- **Integration** — `IntegrationTests/BrokerAllowlistTests.cs`: manual-cID endpoint với fake verifier
  (unrestricted / verified / disallowed / verification-failed) + Open API linker skipping disallowed
  accounts. `BrokerVerifierLiveTests.cs` runs **real** probe when cID creds + algo provided
  (skips cleanly otherwise).
- **E2E** — `E2ETests/BrokerAllowlistTests.cs`: restricted deployment rejects manual add through
  real UI và shows "couldn't verify" notification (no account row added).

## Nodes UI visibility

Nodes là infrastructure most tenants never manage by hand — cTrader CLI agents
[self-register và heartbeat](../operations/node-discovery.md), vì vậy a white-label deployment có thể hide
manual controls, hoặc Nodes surface entirely, và still run a healthy cluster through auto-discovery.
Two config-only branding keys govern this:

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

**`NodesUi` — three modes:**

- **`Full` (default)** — stock product: node list plus manual **New Node** và **Delete**
  controls. `POST`/`DELETE /api/nodes` work.
- **`Monitor`** — read-only surface: list và live stats stay, nhưng manual add và delete are
  removed. Nodes only ever appear through auto-discovery. `POST`/`DELETE /api/nodes` return **404**.
- **`Hidden`** — Nodes nav link và page gone entirely và page route redirects to
  dashboard; manual add/delete API off. Cluster auto-discovery only.

**`RestrictNodesToOwner`** floors who may see và manage nodes. Default `false` keeps standard
**admin-or-above** staff surface (`AdminOrAbove`); set `true` để make it **owner-only** (`Owner`). Either
way **normal users never see nodes** — điều này chỉ chooses between owner-only và wider staff surface.

Node **auto-discovery unaffected by both keys**: anonymous `POST /api/nodes/register` self-register
+ heartbeat endpoint always works, vì vậy `Hidden`/`Monitor` deployment still grows its cluster
automatically.

**Model:**

- `Core.Nodes.NodesUiMode` — `Full` / `Monitor` / `Hidden`.
- `Core.Nodes.NodesUiAccess` — single source of truth composing mode + owner-restriction:
  `IsPageVisible`, `AllowsManualManagement`, `RequiredPolicy(restrictToOwner)`. Nav
  (`NavMenu.razor`), page (`Pages/Nodes.razor`) và endpoints (`NodeEndpoints`) all read it vì vậy
  UI và API never disagree.
- `Core.Options.BrandingOptions.NodesUi` / `.RestrictNodesToOwner` — bound from `App:Branding`.

## Nodes UI visibility — tests

- **Unit** — `UnitTests/Nodes/NodesUiAccessTests.cs`: page-visibility, manual-management và
  required-policy resolution across every mode + default branding.
- **Integration** — `IntegrationTests/NodeUiGatingTests.cs`: over real HTTP + Postgres — `Full` allows a
  manual add, `Monitor`/`Hidden` 404 add và delete, và `RestrictNodesToOwner` forbids an admin while owner
  still reads list.
- **E2E** — `E2ETests/NodesUiTests.cs` (default `Full`: nav link + page + New Node button render) và
  `E2ETests/NodesHiddenTests.cs` (`Hidden`: nav link gone, `/nodes` redirects).

## Design tokens (CSS variables)

Branding also reaches app's **own** stylesheet + custom components, not just MudBlazor. `Web.Branding.BrandingCss.BuildRootVariables(BrandingOptions)` emits branded palette as CSS custom properties on `:root` (`--app-primary`, `--app-primary-hover`, `--app-surface`, `--app-appbar`, `--app-success`/`--app-error`/`--app-warning`/`--app-info`, …), injected in `App.razor` right after `site.css`. `site.css` và mỗi component read `var(--app-*)` — **no hard-coded colours** — vì vậy a reseller's palette flows everywhere (login hero, bottom nav, help tips, offline page) for free. Neutral surface tones default in `site.css :root`; `CustomCss` (injected last) có thể override any token. Xem [ui-guidelines.md](../ui-guidelines.md) §2.

## Branded PWA

Installable app also branded — manifest endpoint (`/manifest.webmanifest`) built from `BrandingOptions` (`ProductName` → `name`/`short_name`, `Description`, `AppBarColor`/`BackgroundColor` → theme/background). Xem [pwa.md](pwa.md).

## Tests

- **Unit** — `UnitTests/Branding/HexColorTests.cs`: valid/invalid hex validation.
- **Integration** — `IntegrationTests/ThemeBuildTests.cs`: colours map into palette, invalid colour throws;
  `IntegrationTests/BrandingHttpTests.cs`: custom `ProductName`/description/theme-colour render in served page `<head>` (WebApplicationFactory + Postgres), defaults keep stock name.
- **E2E** — `E2ETests/BrandingTests.cs`: branded product name renders in app bar in real browser.
