# White-label branding

A reseller can rebrand the app — product name, logo, favicon, colours, custom CSS — entirely through
deployment configuration, no code changes. Every branding value **defaults to the stock identity**, so an
unconfigured deployment looks exactly as before; a reseller overrides only what it needs.

## Model

- `Core.Options.BrandingOptions` — bound from `App:Branding`. String-based (config edge); each colour is
  validated when the theme is built.
- `Core.Branding.HexColor` — value object for a CSS hex colour (`#RGB` / `#RRGGBB`), immutable, self-validating.
  An invalid colour throws a `DomainException` (`domain.branding.color_invalid`) when the theme is built, so
  a misconfigured deployment fails fast at startup rather than rendering a broken palette.
- `Web.Components.Theme.Build(BrandingOptions)` — produces the MudBlazor theme from branding. Only the branded
  palette entries come from configuration; typography, layout and neutral surface tones stay fixed so the
  product keeps a coherent look across resellers.
- `Web.Branding.IBrandingThemeProvider` — singleton that builds the theme once and rebuilds on options change.
  Injected by `MainLayout`/`EmptyLayout` for the `MudThemeProvider` and by the app bar for the product
  name/logo. `App.razor` reads `IOptionsMonitor<AppOptions>` directly for the page `<head>` (title, description,
  favicon, theme-colour, custom CSS).

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
| `LogoUrl` | App-bar logo image; when empty, the product name text shows | *(empty)* |
| `FaviconUrl` | `<link rel="icon">` | `favicon.svg` |
| `Description` | `<meta name="description">` | stock description |
| `PrimaryColor` / `SecondaryColor` | accent, drawer icon, buttons | `#26C281` / `#1FB97A` |
| `AppBarColor` / `BackgroundColor` / `SurfaceColor` | chrome + surfaces + `<meta theme-color>` | dark palette |
| `SuccessColor` / `ErrorColor` / `WarningColor` / `InfoColor` | status colours | stock |
| `CustomCss` | injected `<style>` in `<head>` (deployment-trusted) | *(empty)* |

Assets referenced by `LogoUrl`/`FaviconUrl` are served from the Web app's `wwwroot` (e.g. mount a
`wwwroot/branding/` folder) or any absolute URL.

`App:Branding` is validated at startup (`BrandingOptionsValidator`, run via `ValidateOnStart`): every colour
must be valid hex and `CustomCss` must not contain `<`/`>` (so it cannot break out of the `<style>` tag). A
misconfigured deployment fails to boot with a clear message rather than rendering a broken page.

## Tests

- **Unit** — `UnitTests/Branding/HexColorTests.cs`: valid/invalid hex validation.
- **Integration** — `IntegrationTests/ThemeBuildTests.cs`: colours map into the palette, invalid colour throws;
  `IntegrationTests/BrandingHttpTests.cs`: custom `ProductName`/description/theme-colour render in the served
  page `<head>` (WebApplicationFactory + Postgres), defaults keep the stock name.
- **E2E** — `E2ETests/BrandingTests.cs`: the branded product name renders in the app bar in a real browser.
