---
description: "Reseller rebrand app — product name, logo, favicon, colours, custom CSS — via deployment config, no code change. Every branding value defaults to stock identity."
---

# White-label branding

Reseller rebrand aplikace — název produktu, logo, favicon, barvy, custom CSS — přes nasazovací konfiguraci, bez změny kódu. Každá branding hodnota **defaultuje na stock identitu**: nenakonfigurované nasazení vypadá stejně jako předtím; reseller přepíše jen to, co potřebuje.

## Model

- `Core.Options.BrandingOptions` — svázáno z `App:Branding`. Založeno na stringu (config okraj); každá barva validována při sestavení tématu.
- `Core.Branding.HexColor` — value object pro CSS hex barvu (`#RGB` / `#RRGGBB`), immutable, self-validating. Neplatná barva vyhodí `DomainException` (`domain.branding.color_invalid`) při sestavení tématu — špatně nakonfigurované nasazení failuje rychle při startu, nevyrenderuje broken palette.
- `Web.Components.Theme.Build(BrandingOptions)` — vytvoří MudBlazor téma z branding. Pouze branded palette položky přicházejí z config; typografie, layout, neutrální surface tóny zůstávají fixní, takže produkt si udržuje koherentní vzhled napříč resellery.
- `Web.Branding.IBrandingThemeProvider` — singleton, sestaví téma jednou, přestaví při změně options. Injektováno `MainLayout`/`EmptyLayout` pro `MudThemeProvider`, app barem pro název produktu/logo. `App.razor` čte `IOptionsMonitor<AppOptions>` direct pro page `<head>` (title, description, favicon, theme-colour, custom CSS).

## Konfigurace

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

Forma s environmentální proměnnou: `App__Branding__ProductName=AcmeFX`, `App__Branding__PrimaryColor=%232D7FF9`.

| Klíč | Efekt | Default |
|-----|--------|---------|
| `ProductName` | App-bar text + page `<title>` | `cMind` |
| `LogoUrl` | App-bar logo obrázek; když prázdné, zobrazí se textový název produktu | *(prázdné)* |
| `FaviconUrl` | `<link rel="icon">` | `favicon.svg` |
| `Description` | `<meta name="description">` | stock description |
| `PrimaryColor` / `SecondaryColor` | accent, drawer icon, tlačítka | `#26C281` / `#1FB97A` |
| `AppBarColor` / `BackgroundColor` / `SurfaceColor` | chrome + surfaces; `AppBarColor` řídí `<meta theme-color>` + PWA manifest `theme_color`, `BackgroundColor` manifest `background_color` | dark palette |
| `SuccessColor` / `ErrorColor` / `WarningColor` / `InfoColor` | status barvy | stock |
| `CustomCss` | injected `<style>` v `<head>` (deployment-trusted) | *(prázdné)* |
| `ShowSiteLink` | zobrazit "Powered by cMind" kreditní odkaz na dashboardu | `true` |
| `RequireMfa` | vyžadovat po každém uživateli nastavení dvoufaktorové autentifikace před použitím aplikace | `false` |
| `NodesUi` | kolik z Nodes surface se expeduje: `Full` (list + manual add/delete), `Monitor` (read-only list, žádné add/delete), `Hidden` (žádný nav, žádná page, žádný manual API) | `Full` |
| `RestrictNodesToOwner` | když `true`, pouze owner může vidět/spravovat uzly; jinak celý staff surface admin-or-above. Normální uživatelé nikdy nevidí uzly ani tak | `false` |

Asserty referenced přes `LogoUrl`/`FaviconUrl` servírovány z Web app `wwwroot` (např. mount `wwwroot/branding/` složka) nebo jakékoliv absolutní URL.

`App:Branding` validováno při startu (`BrandingOptionsValidator`, spuštěno přes `ValidateOnStart`): každá barva musí být validní hex, `CustomCss` nesmí obsahovat `<`/`>` (nemůže uniknout z `<style>` tagu). Špatně nakonfigurované nasazení failuje při bootu s jasnou zprávou, nerenderuje broken stránku.

## Odkaz Powered-by

Dashboard vykresluje malý kreditní odkaz **"Powered by cMind"**, který směřuje na dokumentační stránku projektu. Je řízen přes `App:Branding:ShowSiteLink` a je **`true` defaultně** — nenakonfigurované nasazení ho zobrazuje. Reseller provozující plně white-labeled instanci nastaví `App__Branding__ShowSiteLink=false` pro jeho úplné odstranění.

Odkaz je emitován komponentou dashboardu a čte flag přes `IBrandingThemeProvider` / `BrandingOptions`, takže přepínání je změna pouze v konfiguraci (bez rebuildu). Viz [White-label pro firmy](../white-label-for-business.md#the-powered-by-cmind-link) pro business-facing shrnutí.

## Broker allowlist

White-label nasazení může omezit, kterým brokerům obchodní účty mohou uživatelé přidat — takže broker provozující cMind pro své vlastní klienty kdykoliv slouží pouze své vlastní knize. Nakonfigurováno pod `App:Accounts`:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Pepperstone", "IC Markets"]
    }
  }
}
```

Forma s environmentální proměnnou: `App__Accounts__AllowedBrokers__0=Pepperstone`.

**Chování:**

- **Prázdný seznam (default) ⇒ bez omezení.** Každý broker je povolen a **nebeží žádné ověřování** — stock nasazení je zcela nezměněno.
- **Ne-prázdný ⇒ omezený.** cMind kontroluje každý účet, který se uživatel pokusí přidat, proti seznamu (case-insensitive):
  - **Open API (OAuth) propojení** — jméno brokera je autoritativně reportováno cTrader Open API, takže nepovolený účet je jednoduše **přeskočen** (povolené účty ve stejném grantu se stále propojí); autorizační stránka uživateli říká, kteří brokeři byli přeskočeni.
  - **Manuální cID (username / password)** — uživatelem napsaný broker **není** důvěryhodný. cMind **ověřuje** skutečného brokera účtu spuštěním dodávaného broker-probe cBot přes cTrader CLI (čtením `Account.BrokerName`) a persistuje toto ověřené jméno. Nepovolený broker je odmítnut s notifikací; selhání ověření (špatné creds, žádný uzel, timeout) je také surfaceováno a účet není přidán.

**Model:**

- `Core.Options.AccountsOptions` — svázáno z `App:Accounts` (`AllowedBrokers`, `BrokerProbeTimeout`, `BrokerProbeAlgoPath`).
- `Core.Accounts.BrokerName` — value object (oříznutý, case-insensitive rovnost).
- `Core.Accounts.BrokerAllowlist` — `IsRestricted` / `Allows(broker)`; prázdný = povolit vše. Vynucováno jako invariant uvnitř `CTraderIdAccount.AddTradingAccount` / `LinkOpenApiAccount` (`domain.account.broker_not_allowed`).
- `Core.Accounts.IBrokerVerifier` → `Web.Accounts.BrokerVerifier` — spouští probe kontejner na web hostiteli (který má Docker socket), tailuje logy a parsuje brokera přes `Core.Accounts.BrokerProbeOutput`. Voláno pouze když je allowlist omezený.

**Broker-probe cBot:** předpřipravený `broker-probe.algo` se dodává s Web app (`src/Web/BrokerProbe/`, kopírován do výstupu jako `broker-probe/broker-probe.algo`), takže defaultní `App:Accounts:BrokerProbeAlgoPath` resolvuje out of the box — relativní cesta je resolvována proti app base adresáři, absolutní cesta je použita jak je. Zdroj žije v `tools/broker-probe/`. Když algo chybí, manuální-cID ověření failuje closed — účty pod omezeným allowlistem se stále mohou propojit přes Open API cestu, která žádný probe nepotřebuje.

## Broker allowlist — testy

- **Unit** — `UnitTests/Accounts/`: value objects `BrokerName`/`BrokerAllowlist`, parser `BrokerProbeOutput` a allowlist invariant `CTraderIdAccount`.
- **Integration** — `IntegrationTests/BrokerAllowlistTests.cs`: manual-cID endpoint s fake verifierem (neomezený / ověřený / nepovolený / selhání ověření) + Open API linker přeskakující nepovolené účty. `BrokerVerifierLiveTests.cs` spouští **skutečný** probe když jsou poskytnuty cID creds + algo (clean skip jinak).
- **E2E** — `E2ETests/BrokerAllowlistTests.cs`: omezené nasazení odmítá manuální přidání přes skutečné UI a zobrazuje notifikaci "couldn't verify" (žádný řádek účtu nepřidán).

## Nodes UI viditelnost

Uzly jsou infrastruktura, kterou většina tenantů nikdy nespravuje ručně — cTrader CLI agenty [se-registrují a heartbeatují](../operations/node-discovery.md), takže white-label nasazení může skrýt manuální ovládací prvky, nebo celý Nodes surface, a stále provozovat zdravý cluster přes auto-discovery. Dva config-only branding klíče to řídí:

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

Forma s environmentální proměnnou: `App__Branding__NodesUi=Hidden`, `App__Branding__RestrictNodesToOwner=true`.

**`NodesUi` — tři módy:**

- **`Full` (default)** — stock produkt: seznam uzlů plus manuální **Nový uzel** a **Smazat** ovládací prvky. `POST`/`DELETE /api/nodes` fungují.
- **`Monitor`** — read-only surface: seznam a live statistiky zůstávají, ale manuální přidání a smazání jsou odstraněny. Uzly se objevují pouze přes auto-discovery. `POST`/`DELETE /api/nodes` vrací **404**.
- **`Hidden`** — navigační odkaz Nodes a stránka jsou zcela pryč a route stránky přesměrovává na dashboard; manuální add/delete API je vypnuto. Cluster je pouze auto-discovery.

**`RestrictNodesToOwner`** určuje, kdo může vidět a spravovat uzly. Default `false` zachovává standardní staff surface **admin-or-above** (`AdminOrAbove`); nastavte `true` pro **owner-only** (`Owner`). Ať tak či onak **normální uživatelé nikdy nevidí uzly** — toto pouze vybírá mezi owner-only a širším staff surface.

**Auto-discovery uzlů je oběma klíči nedotčeno**: anonymní endpoint `POST /api/nodes/register` se-registrace + heartbeat vždy funguje, takže `Hidden`/`Monitor` nasazení stále automaticky rozšiřuje svůj cluster.

**Model:**

- `Core.Nodes.NodesUiMode` — `Full` / `Monitor` / `Hidden`.
- `Core.Nodes.NodesUiAccess` — jediný zdroj pravdy skládající mód + owner-restrikce: `IsPageVisible`, `AllowsManualManagement`, `RequiredPolicy(restrictToOwner)`. Nav (`NavMenu.razor`), stránka (`Pages/Nodes.razor`) a endpointy (`NodeEndpoints`) to všechno čtou, takže UI a API se nikdy nemohou rozcházet.
- `Core.Options.BrandingOptions.NodesUi` / `.RestrictNodesToOwner` — svázáno z `App:Branding`.

## Nodes UI viditelnost — testy

- **Unit** — `UnitTests/Nodes/NodesUiAccessTests.cs`: page-visibility, manuální management a required-policy resolution napříč každým módem + default branding.
- **Integration** — `IntegrationTests/NodeUiGatingTests.cs`: přes skutečné HTTP + Postgres — `Full` povoluje manuální přidání, `Monitor`/`Hidden` 404 add a delete a `RestrictNodesToOwner` zakazuje admina zatímco owner stále čte seznam.
- **E2E** — `E2ETests/NodesUiTests.cs` (default `Full`: nav link + page + New Node button render) a `E2ETests/NodesHiddenTests.cs` (`Hidden`: nav link pryč, `/nodes` přesměrovává).

## Design tokens (CSS proměnné)

Branding také dosahuje vlastního stylesheetu aplikace + vlastních komponent, nejen MudBlazor. `Web.Branding.BrandingCss.BuildRootVariables(BrandingOptions)` emituje branded paletu jako CSS custom properties na `:root` (`--app-primary`, `--app-primary-hover`, `--app-surface`, `--app-appbar`, `--app-success`/`--app-error`/`--app-warning`/`--app-info`, …), injektováno v `App.razor` hned po `site.css`. `site.css` a každá komponenta čtou `var(--app-*)` — **žádné hard-coded barvy** — takže reseller's palette...

## Branded PWA

Instalovatelná aplikace je také branded — manifest endpoint (`/manifest.webmanifest`) je sestaven z `BrandingOptions` (`ProductName` → `name`/`short_name`, `Description`, `AppBarColor`/`BackgroundColor` → theme/background). Viz [pwa.md](pwa.md).

## Testy

- **Unit** — `UnitTests/Branding/HexColorTests.cs`: valid/invalid hex validace.
- **Integration** — `IntegrationTests/ThemeBuildTests.cs`: barvy mapují do palety, neplatná barva vyhodí; `IntegrationTests/BrandingHttpTests.cs`: vlastní `ProductName`/description/theme-colour render v served page `<head>` (WebApplicationFactory + Postgres), defaulty drží stock název.
- **E2E** — `E2ETests/BrandingTests.cs`: branded název produktu render v app baru ve skutečném prohlížeči.
