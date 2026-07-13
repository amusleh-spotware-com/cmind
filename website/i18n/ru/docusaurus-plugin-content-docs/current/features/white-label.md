---
description: "Reseller перебранд приложение — имя продукта, логотип, favicon, цвета, custom CSS — через deployment config, нет code change. Каждое branding значение defaults на stock…"
---

# White-label branding

Reseller перебранд приложение — имя продукта, логотип, favicon, цвета, custom CSS — через deployment config, нет code change. Каждое branding значение **defaults на stock identity**: unconfigured deployment выглядит то же как раньше; reseller override только что need.

## Model

- `Core.Options.BrandingOptions` — bound из `App:Branding`. String-based (config edge); каждый цвет валидирован когда theme built.
- `Core.Branding.HexColor` — value object для CSS hex цвета (`#RGB` / `#RRGGBB`), immutable, self-validating. Invalid цвет throws `DomainException` (`domain.branding.color_invalid`) когда theme built — misconfigured deployment fail fast на startup, не render broken palette.
- `Web.Components.Theme.Build(BrandingOptions)` — produce MudBlazor theme из branding. Только branded palette entries приходят из config; typography, layout, neutral surface tones stay fixed поэтому product keep coherent look поперек resellers.
- `Web.Branding.IBrandingThemeProvider` — singleton, build theme один раз, rebuild на options change. Injected by `MainLayout`/`EmptyLayout` для `MudThemeProvider`, by app bar для product name/logo. `App.razor` read `IOptionsMonitor<AppOptions>` direct для page `<head>` (title, description, favicon, theme-colour, custom CSS).

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

Environment-variable форма: `App__Branding__ProductName=AcmeFX`, `App__Branding__PrimaryColor=%232D7FF9`.

| Key | Effect | Default |
|-----|--------|---------|
| `ProductName` | App-bar текст + page `<title>` | `cMind` |
| `LogoUrl` | App-bar logo image; когда пусто, product name text shows | *(пусто)* |
| `FaviconUrl` | `<link rel="icon">` | `favicon.svg` |
| `Description` | `<meta name="description">` | stock description |
| `PrimaryColor` / `SecondaryColor` | accent, drawer icon, buttons | `#26C281` / `#1FB97A` |
| `AppBarColor` / `BackgroundColor` / `SurfaceColor` | chrome + surfaces; `AppBarColor` drives `<meta theme-color>` + PWA manifest `theme_color`, `BackgroundColor` manifest `background_color` | dark palette |
| `SuccessColor` / `ErrorColor` / `WarningColor` / `InfoColor` | status colors | stock |
| `CustomCss` | injected `<style>` в `<head>` (deployment-trusted) | *(пусто)* |
| `ShowSiteLink` | show "Powered by cMind" credit link на dashboard | `true` |
| `RequireMfa` | require каждого пользователя set up two-factor authentication перед using app | `false` |
| `NodesUi` | как много Nodes surface ships: `Full` (list + manual add/delete), `Monitor` (read-only list, нет add/delete), `Hidden` (нет nav, нет page, нет manual API) | `Full` |
| `RestrictNodesToOwner` | когда `true`, только owner может видеть/управлять узлами; иначе целая admin-or-above staff surface может. Normal users никогда не видят узлы either way | `false` |

Assets referenced by `LogoUrl`/`FaviconUrl` served из Web app `wwwroot` (например mount `wwwroot/branding/` folder) или любой absolute URL.

`App:Branding` валидирован на startup (`BrandingOptionsValidator`, run через `ValidateOnStart`): каждый цвет must быть valid hex, `CustomCss` must не содержать `<`/`>` (не может break out of `<style>` tag). Misconfigured deployment fail boot с clear message, не render broken page.

## Powered-by link

Dashboard renders маленький **"Powered by cMind"** credit link что points на project's documentation site. Это controlled by `App:Branding:ShowSiteLink` и есть **`true` по умолчанию** — unconfigured deployment shows это. Reseller работающий fully white-labeled instance sets `App__Branding__ShowSiteLink=false` для remove это entirely.

Link emitted by dashboard component и reads flag через `IBrandingThemeProvider` / `BrandingOptions`, поэтому toggling это — config-only change (нет rebuild). See [White-label for business](../white-label-for-business.md#the-powered-by-cmind-link) для business-facing summary.

## Broker allowlist

White-label deployment может restrict какие brokers' trading accounts его users могут add — поэтому broker running cMind для его собственных clients только ever serves его own book. Configured under `App:Accounts`:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Pepperstone", "IC Markets"]
    }
  }
}
```

Environment-variable форма: `App__Accounts__AllowedBrokers__0=Pepperstone`.

**Behaviour:**

- **Empty list (default) ⇒ unrestricted.** Каждый broker allowed и **нет verification runs** — stock deployment completely unchanged.
- **Non-empty ⇒ restricted.** cMind checks каждый account пользователь tries add против list (case-insensitive):
  - **Open API (OAuth) link** — broker name reported authoritatively by cTrader Open API, поэтому disallowed account просто **skipped** (allowed accounts в том же grant все еще link); authorization page tells пользователю какие brokers were skipped.
  - **Manual cID (username / password)** — user-typed broker — **не** trusted. cMind **verifies** account's real broker by running shipped broker-probe cBot через cTrader CLI (reading `Account.BrokerName`) и persists что verified name. Disallowed broker rejected с notification; verification failure (bad credentials, нет node, timeout) surfaced тоже, и account не added.

**Model:**

- `Core.Options.AccountsOptions` — bound из `App:Accounts` (`AllowedBrokers`, `BrokerProbeTimeout`, `BrokerProbeAlgoPath`).
- `Core.Accounts.BrokerName` — value object (trimmed, case-insensitive equality).
- `Core.Accounts.BrokerAllowlist` — `IsRestricted` / `Allows(broker)`; empty = allow все. Enforced как invariant внутри `CTraderIdAccount.AddTradingAccount` / `LinkOpenApiAccount` (`domain.account.broker_not_allowed`).
- `Core.Accounts.IBrokerVerifier` → `Web.Accounts.BrokerVerifier` — runs probe container на web host (который has Docker socket), tails logs, и parses broker через `Core.Accounts.BrokerProbeOutput`. Только invoked когда allowlist restricted.

**Broker-probe cBot:** prebuilt `broker-probe.algo` ships с Web app (`src/Web/BrokerProbe/`, copied на output как `broker-probe/broker-probe.algo`), поэтому default `App:Accounts:BrokerProbeAlgoPath` resolves out of box — relative path resolved against app base directory, absolute path used как given. Source lives в `tools/broker-probe/`. Когда algo absent, manual-cID verification fails closed — accounts under restricted allowlist все еще можно linked через Open API path, что needs нет probe.

## Broker allowlist — tests

- **Unit** — `UnitTests/Accounts/`: `BrokerName`/`BrokerAllowlist` value objects, `BrokerProbeOutput` parser, и `CTraderIdAccount` allowlist invariant.
- **Integration** — `IntegrationTests/BrokerAllowlistTests.cs`: manual-cID endpoint с fake verifier (unrestricted / verified / disallowed / verification-failed) + Open API linker skipping disallowed accounts. `BrokerVerifierLiveTests.cs` runs **real** probe когда cID creds + algo provided (skips cleanly иначе).
- **E2E** — `E2ETests/BrokerAllowlistTests.cs`: restricted deployment rejects manual add через real UI и shows "couldn't verify" notification (нет account row added).

## Nodes UI visibility

Nodes — infrastructure большинство tenants никогда не manage by hand — cTrader CLI agents [self-register и heartbeat](../operations/node-discovery.md), поэтому white-label deployment может hide manual controls, или Nodes surface entirely, и все еще run healthy cluster через auto-discovery. Два config-only branding keys govern это:

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

Environment-variable форма: `App__Branding__NodesUi=Hidden`, `App__Branding__RestrictNodesToOwner=true`.

**`NodesUi` — три modes:**

- **`Full` (default)** — stock product: node list плюс manual **New Node** и **Delete** controls. `POST`/`DELETE /api/nodes` work.
- **`Monitor`** — read-only surface: list и live stats stay, но manual add и delete removed. Nodes только ever appear через auto-discovery. `POST`/`DELETE /api/nodes` return **404**.
- **`Hidden`** — Nodes nav link и page gone entirely и page route redirects на dashboard; manual add/delete API off. Cluster — auto-discovery only.

**`RestrictNodesToOwner`** floors кто может видеть и manage узлы. Default `false` keeps standard **admin-or-above** staff surface (`AdminOrAbove`); set `true` для make это **owner-only** (`Owner`). Either way **normal users никогда не видят узлы** — это только chooses between owner-only и wider staff surface.

Node **auto-discovery unaffected обоими keys**: anonymous `POST /api/nodes/register` self-register + heartbeat endpoint всегда works, поэтому `Hidden`/`Monitor` deployment все еще grows его cluster automatically.

**Model:**

- `Core.Nodes.NodesUiMode` — `Full` / `Monitor` / `Hidden`.
- `Core.Nodes.NodesUiAccess` — single source of truth composing mode + owner-restriction: `IsPageVisible`, `AllowsManualManagement`, `RequiredPolicy(restrictToOwner)`. Nav (`NavMenu.razor`), page (`Pages/Nodes.razor`) и endpoints (`NodeEndpoints`) все read это поэтому UI и API никогда не disagree.
- `Core.Options.BrandingOptions.NodesUi` / `.RestrictNodesToOwner` — bound из `App:Branding`.

## Nodes UI visibility — tests

- **Unit** — `UnitTests/Nodes/NodesUiAccessTests.cs`: page-visibility, manual-management и required-policy resolution поперек каждого mode + default branding.
- **Integration** — `IntegrationTests/NodeUiGatingTests.cs`: через real HTTP + Postgres — `Full` allows manual add, `Monitor`/`Hidden` 404 add и delete, и `RestrictNodesToOwner` forbids admin пока owner все еще reads list.
- **E2E** — `E2ETests/NodesUiTests.cs` (default `Full`: nav link + page + New Node button render) и `E2ETests/NodesHiddenTests.cs` (`Hidden`: nav link gone, `/nodes` redirects).

## Design tokens (CSS variables)

Branding также reaches app's **собственный** stylesheet + custom components, не просто MudBlazor. `Web.Branding.BrandingCss.BuildRootVariables(BrandingOptions)` emits branded palette как CSS custom properties на `:root` (`--app-primary`, `--app-primary-hover`, `--app-surface`, `--app-appbar`, `--app-success`/`--app-error`/`--app-warning`/`--app-info`, …), injected в `App.razor` right после `site.css`. `site.css` и каждый component read `var(--app-*)` — **нет hard-coded colors** — поэтому reseller's palette flows везде (login hero, bottom nav, help tips, offline page) даром. Neutral surface tones default в `site.css :root`; `CustomCss` (injected last) может override любой token. See [ui-guidelines.md](../ui-guidelines.md) §2.

## Branded PWA

Installable app branded тоже — manifest endpoint (`/manifest.webmanifest`) built из `BrandingOptions` (`ProductName` → `name`/`short_name`, `Description`, `AppBarColor`/`BackgroundColor` → theme/background). See [pwa.md](pwa.md).

## Tests

- **Unit** — `UnitTests/Branding/HexColorTests.cs`: valid/invalid hex validation.
- **Integration** — `IntegrationTests/ThemeBuildTests.cs`: colors map into palette, invalid color throws; `IntegrationTests/BrandingHttpTests.cs`: custom `ProductName`/description/theme-colour render в served page `<head>` (WebApplicationFactory + Postgres), defaults keep stock name.
- **E2E** — `E2ETests/BrandingTests.cs`: branded product name renders в app bar в real browser.
