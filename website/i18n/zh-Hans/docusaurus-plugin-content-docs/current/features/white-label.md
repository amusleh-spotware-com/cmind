---
description: "Reseller rebrand app — product name, logo, favicon, colours, custom CSS — via deployment config, no code change. Every branding value defaults to stock…"
---

# White-label branding

Reseller rebrand app — product name, logo, favicon, colours, custom CSS — via deployment config, no code change. Every branding value **defaults to stock identity**: unconfigured deployment look same as before; reseller override only what need.

## Model

- `Core.Options.BrandingOptions` — bound from `App:Branding`. String-based (config edge); each colour validated when theme built.
- `Core.Branding.HexColor` — value object for CSS hex colour (`#RGB` / `#RRGGBB`), immutable, self-validating.
  Invalid colour throws `DomainException` (`domain.branding.color_invalid`) when theme built — misconfigured deployment fail fast at startup, not render broken palette.
- `Web.Components.Theme.Build(BrandingOptions)` — produce MudBlazor theme from branding. Only branded palette entries come from config; typography, layout, neutral surface tones stay fixed so product keep coherent look across resellers.
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
| `AppBarColor` / `BackgroundColor` / `SurfaceColor` | chrome + surfaces; `AppBarColor` drives `<meta theme-color>` + PWA manifest `theme_color`, `BackgroundColor` the manifest `background_color` | dark palette |
| `SuccessColor` / `ErrorColor` / `WarningColor` / `InfoColor` | status colours | stock |
| `CustomCss` | injected `<style>` in `<head>` (deployment-trusted) | *(empty)* |
| `ShowSiteLink` | show the "Powered by cMind" credit link on the dashboard | `true` |
| `RequireMfa` | require every user to set up two-factor authentication before using the app | `false` |
| `NodesUi` | how much of the Nodes surface ships: `Full` (list + manual add/delete), `Monitor` (read-only list, no add/delete), `Hidden` (no nav, no page, no manual API) | `Full` |
| `RestrictNodesToOwner` | when `true`, only the owner may see/manage nodes; otherwise the whole admin-or-above staff surface can. Normal users never see nodes either way | `false` |

Assets referenced by `LogoUrl`/`FaviconUrl` served from Web app `wwwroot` (e.g. mount `wwwroot/branding/` folder) or any absolute URL.

`App:Branding` validated at startup (`BrandingOptionsValidator`, run via `ValidateOnStart`): every colour must be valid hex, `CustomCss` must not contain `<`/`>` (cannot break out of `<style>` tag). Misconfigured deployment fail to boot with clear message, not render broken page.

## Powered-by link

The dashboard renders a small **"Powered by cMind"** credit link that points to the project's
documentation site. It is controlled by `App:Branding:ShowSiteLink` and is **`true` by default** — an
unconfigured deployment shows it. A reseller running a fully white-labeled instance sets
`App__Branding__ShowSiteLink=false` to remove it entirely.

The link is emitted by the dashboard component and reads the flag through `IBrandingThemeProvider` /
`BrandingOptions`, so toggling it is a config-only change (no rebuild). See
[White-label for business](../white-label-for-business.md#the-powered-by-cmind-link) for the
business-facing summary.

## Broker allowlist

A white-label deployment can restrict which brokers' trading accounts its users may add — so a broker
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

- **Empty list (default) ⇒ unrestricted.** Every broker is allowed and **no verification runs** — a
  stock deployment is completely unchanged.
- **Non-empty ⇒ restricted.** cMind checks every account a user tries to add against the list
  (case-insensitive):
  - **Open API (OAuth) link** — the broker name is reported authoritatively by the cTrader Open API, so
    a disallowed account is simply **skipped** (allowed accounts in the same grant still link); the
    authorization page tells the user which brokers were skipped.
  - **Manual cID (username / password)** — the user-typed broker is **not** trusted. cMind **verifies**
    the account's real broker by running the shipped broker-probe cBot through the cTrader CLI (reading
    `Account.BrokerName`) and persists that verified name. A disallowed broker is rejected with a
    notification; a verification failure (bad credentials, no node, timeout) is surfaced too, and the
    account is not added.

**Model:**

- `Core.Options.AccountsOptions` — bound from `App:Accounts` (`AllowedBrokers`, `BrokerProbeTimeout`,
  `BrokerProbeAlgoPath`).
- `Core.Accounts.BrokerName` — value object (trimmed, case-insensitive equality).
- `Core.Accounts.BrokerAllowlist` — `IsRestricted` / `Allows(broker)`; empty = allow all. Enforced as an
  invariant inside `CTraderIdAccount.AddTradingAccount` / `LinkOpenApiAccount`
  (`domain.account.broker_not_allowed`).
- `Core.Accounts.IBrokerVerifier` → `Web.Accounts.BrokerVerifier` — runs the probe container on the web
  host (which has the Docker socket), tails logs, and parses the broker via
  `Core.Accounts.BrokerProbeOutput`. Only invoked when the allowlist is restricted.

**Broker-probe cBot:** a prebuilt `broker-probe.algo` ships with the Web app (`src/Web/BrokerProbe/`,
copied to the output as `broker-probe/broker-probe.algo`), so the default
`App:Accounts:BrokerProbeAlgoPath` resolves out of the box — a relative path is resolved against the app
base directory, an absolute path is used as given. The source lives in `tools/broker-probe/`. When the
algo is absent, manual-cID verification fails closed — accounts under a restricted allowlist can still be
linked via the Open API path, which needs no probe.

## Broker allowlist — tests

- **Unit** — `UnitTests/Accounts/`: `BrokerName`/`BrokerAllowlist` value objects, `BrokerProbeOutput`
  parser, and the `CTraderIdAccount` allowlist invariant.
- **Integration** — `IntegrationTests/BrokerAllowlistTests.cs`: manual-cID endpoint with a fake verifier
  (unrestricted / verified / disallowed / verification-failed) + Open API linker skipping disallowed
  accounts. `BrokerVerifierLiveTests.cs` runs the **real** probe when cID creds + the algo are provided
  (skips cleanly otherwise).
- **E2E** — `E2ETests/BrokerAllowlistTests.cs`: a restricted deployment rejects a manual add through the
  real UI and shows the "couldn't verify" notification (no account row added).

## Nodes UI visibility

Nodes are infrastructure most tenants never manage by hand — cTrader CLI agents
[self-register and heartbeat](../operations/node-discovery.md), so a white-label deployment can hide the
manual controls, or the Nodes surface entirely, and still run a healthy cluster through auto-discovery.
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

- **`Full` (default)** — the stock product: the node list plus the manual **New Node** and **Delete**
  controls. `POST`/`DELETE /api/nodes` work.
- **`Monitor`** — a read-only surface: the list and live stats stay, but manual add and delete are
  removed. Nodes only ever appear through auto-discovery. `POST`/`DELETE /api/nodes` return **404**.
- **`Hidden`** — the Nodes nav link and page are gone entirely and the page route redirects to the
  dashboard; the manual add/delete API is off. The cluster is auto-discovery only.

**`RestrictNodesToOwner`** floors who may see and manage nodes. Default `false` keeps the standard
**admin-or-above** staff surface (`AdminOrAbove`); set `true` to make it **owner-only** (`Owner`). Either
way **normal users never see nodes** — this only chooses between owner-only and the wider staff surface.

Node **auto-discovery is unaffected by both keys**: the anonymous `POST /api/nodes/register` self-register
+ heartbeat endpoint always works, so a `Hidden`/`Monitor` deployment still grows its cluster
automatically.

**Model:**

- `Core.Nodes.NodesUiMode` — `Full` / `Monitor` / `Hidden`.
- `Core.Nodes.NodesUiAccess` — the single source of truth composing the mode + owner-restriction:
  `IsPageVisible`, `AllowsManualManagement`, `RequiredPolicy(restrictToOwner)`. Nav
  (`NavMenu.razor`), the page (`Pages/Nodes.razor`) and the endpoints (`NodeEndpoints`) all read it so
  the UI and API can never disagree.
- `Core.Options.BrandingOptions.NodesUi` / `.RestrictNodesToOwner` — bound from `App:Branding`.

## Nodes UI visibility — tests

- **Unit** — `UnitTests/Nodes/NodesUiAccessTests.cs`: page-visibility, manual-management and
  required-policy resolution across every mode + default branding.
- **Integration** — `IntegrationTests/NodeUiGatingTests.cs`: over real HTTP + Postgres — `Full` allows a
  manual add, `Monitor`/`Hidden` 404 add and delete, and `RestrictNodesToOwner` forbids an admin while the
  owner still reads the list.
- **E2E** — `E2ETests/NodesUiTests.cs` (default `Full`: nav link + page + New Node button render) and
  `E2ETests/NodesHiddenTests.cs` (`Hidden`: nav link gone, `/nodes` redirects).

## Design tokens (CSS variables)

Branding also reaches the app's **own** stylesheet + custom components, not just MudBlazor. `Web.Branding.BrandingCss.BuildRootVariables(BrandingOptions)` emits the branded palette as CSS custom properties on `:root` (`--app-primary`, `--app-primary-hover`, `--app-surface`, `--app-appbar`, `--app-success`/`--app-error`/`--app-warning`/`--app-info`, …), injected in `App.razor` right after `site.css`. `site.css` and every component read `var(--app-*)` — **no hard-coded colours** — so a reseller's palette flows everywhere (login hero, bottom nav, help tips, offline page) for free. Neutral surface tones default in `site.css :root`; `CustomCss` (injected last) can override any token. See [ui-guidelines.md](../ui-guidelines.md) §2.

## Branded PWA

The installable app is branded too — the manifest endpoint (`/manifest.webmanifest`) is built from `BrandingOptions` (`ProductName` → `name`/`short_name`, `Description`, `AppBarColor`/`BackgroundColor` → theme/background). See [pwa.md](pwa.md).

## Tests

- **Unit** — `UnitTests/Branding/HexColorTests.cs`: valid/invalid hex validation.
- **Integration** — `IntegrationTests/ThemeBuildTests.cs`: colours map into palette, invalid colour throws;
  `IntegrationTests/BrandingHttpTests.cs`: custom `ProductName`/description/theme-colour render in served page `<head>` (WebApplicationFactory + Postgres), defaults keep stock name.
- **E2E** — `E2ETests/BrandingTests.cs`: branded product name renders in app bar in real browser.
<!-- [ZH-HANS] Translation needed -->
