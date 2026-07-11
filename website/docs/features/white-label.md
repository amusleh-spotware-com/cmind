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

## Design tokens (CSS variables)

Branding also reaches the app's **own** stylesheet + custom components, not just MudBlazor. `Web.Branding.BrandingCss.BuildRootVariables(BrandingOptions)` emits the branded palette as CSS custom properties on `:root` (`--app-primary`, `--app-primary-hover`, `--app-surface`, `--app-appbar`, `--app-success`/`--app-error`/`--app-warning`/`--app-info`, …), injected in `App.razor` right after `site.css`. `site.css` and every component read `var(--app-*)` — **no hard-coded colours** — so a reseller's palette flows everywhere (login hero, bottom nav, help tips, offline page) for free. Neutral surface tones default in `site.css :root`; `CustomCss` (injected last) can override any token. See [ui-guidelines.md](../ui-guidelines.md) §2.

## Branded PWA

The installable app is branded too — the manifest endpoint (`/manifest.webmanifest`) is built from `BrandingOptions` (`ProductName` → `name`/`short_name`, `Description`, `AppBarColor`/`BackgroundColor` → theme/background). See [pwa.md](pwa.md).

## Tests

- **Unit** — `UnitTests/Branding/HexColorTests.cs`: valid/invalid hex validation.
- **Integration** — `IntegrationTests/ThemeBuildTests.cs`: colours map into palette, invalid colour throws;
  `IntegrationTests/BrandingHttpTests.cs`: custom `ProductName`/description/theme-colour render in served page `<head>` (WebApplicationFactory + Postgres), defaults keep stock name.
- **E2E** — `E2ETests/BrandingTests.cs`: branded product name renders in app bar in real browser.