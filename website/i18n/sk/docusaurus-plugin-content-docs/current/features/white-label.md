---
description: "Reseller preznačte app — názov produktu, logo, favicon, farby, vlastný CSS — cez nasadenie config, bez zmeny kódu. Každá branding hodnota štandardne na zásoby…"
---

# White-label branding

Reseller preznačte app — názov produktu, logo, favicon, farby, vlastný CSS — cez nasadenie config, bez zmeny kódu. Každá branding hodnota **štandardne na zásoby identita**: nekonfigurované nasadenie vyzerať rovnako ako predtým; reseller override iba čo potreba.

## Model

- `Core.Options.BrandingOptions` — viazané z `App:Branding`. String-based (config hrana); každá farba validovaná keď je téma postavená.
- `Core.Branding.HexColor` — objekt hodnota na CSS hex farba (`#RGB` / `#RRGGBB`), nemenný, samoosád validovanie.
  Neplatná farba vyvolá `DomainException` (`domain.branding.color_invalid`) keď je téma postavená — misconfigured nasadení zlyhať rýchlo pri spustení, nie vykresliť zlomený palette.
- `Web.Components.Theme.Build(BrandingOptions)` — produkovať MudBlazor téma z branding. Iba branding palette položky pochádzajú z config; typografia, rozloženie, neutrálne povrchové tóny zostávajú fixovaný takže produktu udržať koherentný vzhľad cez resellery.
- `Web.Branding.IBrandingThemeProvider` — singleton, postaviť téma raz, obnov na zmeny možností.
  Injektovaný podľa `MainLayout`/`EmptyLayout` pre `MudThemeProvider`, podľa app bar pre názov produktu/logo. `App.razor` čítať `IOptionsMonitor<AppOptions>` priamo na stránku `<head>` (názov, popis, favicon, téma-farba, vlastný CSS).

## Konfigurácia

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

Formulár环境変数: `App__Branding__ProductName=AcmeFX`, `App__Branding__PrimaryColor=%232D7FF9`.

| Kľúč | Efekt | Štandardne |
|-----|--------|---------|
| `ProductName` | App-bar text + stránka `<title>` | `cMind` |
| `LogoUrl` | App-bar logo obrázok; keď prázdne, názov produktu text ukazuje | *(prázdne)* |
| `FaviconUrl` | `<link rel="icon">` | `favicon.svg` |
| `Description` | `<meta name="description">` | stock popis |
| `PrimaryColor` / `SecondaryColor` | accent, drawer ikona, tlačidlá | `#26C281` / `#1FB97A` |
| `AppBarColor` / `BackgroundColor` / `SurfaceColor` | chrome + povrchy; `AppBarColor` pohány `<meta theme-color>` + PWA manifest `theme_color`, `BackgroundColor` manifest `background_color` | tmavý palette |
| `SuccessColor` / `ErrorColor` / `WarningColor` / `InfoColor` | status farby | stock |
| `CustomCss` | injektovaný `<style>` v `<head>` (deployment-trusted) | *(prázdne)* |
| `ShowSiteLink` | ukazovať "Powered by cMind" krediť link na dashboard | `true` |
| `RequireMfa` | vyžadovať každého používateľa na nastaviť two-factor authentication pred používaním app | `false` |
| `NodesUi` | ako veľa z Nodes povrchu lodí: `Full` (zoznam + manuálne pridať/odstrániť), `Monitor` (read-only zoznam, bez pridať/odstrániť), `Hidden` (žiadny nav, žádna stránka, žádny manuálny API) | `Full` |
| `RestrictNodesToOwner` | keď `true`, iba vlastník môže vidieť/spravovať uzly; inak celý admin-or-above personál povrch môžu. Normálni používatelia nikdy nevidia uzly ani tak | `false` |

Aktíva odkazované podľa `LogoUrl`/`FaviconUrl` podávané z Web app `wwwroot` (napr. montáž `wwwroot/branding/` priečinok) alebo žiadny absolútny URL.

`App:Branding` validovaný pri spustení (`BrandingOptionsValidator`, spustiť cez `ValidateOnStart`): každá farba musí byť platný hex, `CustomCss` nesmie obsahovať `<`/`>` (nemôže zlomiť z `<style>` tag). Misconfigured nasadení zlyhať na spustenie s jasným správou, nie vykresliť zlomený stránka.

## Powered-by link

Dashboard vykresluje malú **"Powered by cMind"** krediť link, ktorý ukazuje na projekt
dokumentácia miesta. Je riadený podľa `App:Branding:ShowSiteLink` a je **`true` štandardne** — a
nekonfigurované nasadení ukazuje to. A reseller spúšťajúce plne white-labeled inštancia nastav
`App__Branding__ShowSiteLink=false` na úplné odstránenie to.

Link je emitovaný komponentou dashboard a čítať vlajku cez `IBrandingThemeProvider` /
`BrandingOptions`, takže toggle to je config-only zmena (bez rebuild). Pozrite si
[White-label pre business](../white-label-for-business.md#the-powered-by-cmind-link) pre
business-facing zhrnutie.

## Broker allowlist

A white-label nasadení môžu obmedziť, ktorých brokerov obchodné účty svojich používateľov môžu pridať — takže a broker
spúšťajúc cMind pre svojich vlastných klientov iba kedy slúži svoj vlastný knihu. Nakonfigurovaný pod `App:Accounts`:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Pepperstone", "IC Markets"]
    }
  }
}
```

Formulár环境変数: `App__Accounts__AllowedBrokers__0=Pepperstone`.

**Správanie:**

- **Prázdny zoznam (štandardne) ⇒ neobmedzené.** Každý broker je povolený a **žádne overenie beží** — a
  stock nasadení je úplne nezmenené.
- **Non-empty ⇒ obmedzené.** cMind kontroly každý účet a používateľ pokúša pridať voči zoznam
  (case-insensitive):
  - **Open API (OAuth) link** — broker názov je hlásené autoritatívne podľa cTrader Open API, takže a
    zakázaný účet je jednoducho **preskočené** (povolené účty v rovnakom grant stále link); autorizácia
    stránka povie používateľ, ktorí brokery boli preskočené.
  - **Manuálne cID (username / heslo)** — používateľ-typed broker je **nie** dôveryhodný. cMind **overuje**
    účet reálny broker spustením dodaného broker-probe cBot cez cTrader CLI (čítanie
    `Account.BrokerName`) a trvalé, že overené meno. A zakázaný broker je odoprevá s a
    notifikácia; overenie zlyhanie (zlé poverenie, žádny uzol, timeout) je povrch tiež a a
    účet nie je pridaný.

**Model:**

- `Core.Options.AccountsOptions` — viazané z `App:Accounts` (`AllowedBrokers`, `BrokerProbeTimeout`,
  `BrokerProbeAlgoPath`).
- `Core.Accounts.BrokerName` — objekt hodnota (trimmed, case-insensitive rovnosť).
- `Core.Accounts.BrokerAllowlist` — `IsRestricted` / `Allows(broker)`; prázdne = povoliť všetci. Vynútiť ako a
  invariant vnútri `CTraderIdAccount.AddTradingAccount` / `LinkOpenApiAccount`
  (`domain.account.broker_not_allowed`).
- `Core.Accounts.IBrokerVerifier` → `Web.Accounts.BrokerVerifier` — beží sonda kontajner na web
  hostiteľ (ktorý má Docker socket), chvosty logy a parsuje broker cez
  `Core.Accounts.BrokerProbeOutput`. Iba vyvolané keď je allowlist obmedzený.

**Broker-probe cBot:** a prebuilt `broker-probe.algo` dodáva s Web app (`src/Web/BrokerProbe/`,
skopírované na výstup ako `broker-probe/broker-probe.algo`), takže štandardne
`App:Accounts:BrokerProbeAlgoPath` riešoný z krabice — a relatívna cesta je riešené voči app
základný adresár, absolútna cesta sa používa tak ako je daný. Zdroj žije v `tools/broker-probe/`. Keď algo
chýba, manuálny-cID overenie zlyhá zatvorený — účty pod obmedzený allowlist stále môžu byť
linkovať cez Open API cestu, ktorá potreby žádny sonda.

## Broker allowlist — testy

- **Jednotka** — `UnitTests/Accounts/`: `BrokerName`/`BrokerAllowlist` objekty hodnota, `BrokerProbeOutput`
  parser a `CTraderIdAccount` allowlist invariant.
- **Integrácia** — `IntegrationTests/BrokerAllowlistTests.cs`: manuálny-cID koncový bod s falošný verifier
  (neobmedzené / overené / zakázaný / verification-failed) + Open API linker preskakujúce zakázaný
  účty. `BrokerVerifierLiveTests.cs` beží **reálny** sonda keď cID poverenia + algo sú poskytnúť
  (preskakuje čisto inak).
- **E2E** — `E2ETests/BrokerAllowlistTests.cs`: a obmedzený nasadení odoprevá manuálny pridať cez
  reálny UI a ukazuje "nemôžu verifiť" notifikácia (žádny účet riadok pridaný).

## Nodes UI viditeľnosť

Nodes sú infraštruktúra väčšina nájomcov nikdy spravovať manuálne — cTrader CLI agenti
[self-register a pulzuje](../operations/node-discovery.md), takže a white-label nasadení môžu skrý
manuálne ovládače alebo Nodes povrchu úplne a stále spustenia zdravý klaster cez auto-objavovanie.
Dva config-only branding kľúče vládneme to:

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

Formulár环境変数: `App__Branding__NodesUi=Hidden`, `App__Branding__RestrictNodesToOwner=true`.

**`NodesUi` — tri režimy:**

- **`Full` (štandardne)** — stock produktu: node zoznam plus manuálny **Nový Node** a **Odstrániť**
  ovládače. `POST`/`DELETE /api/nodes` práca.
- **`Monitor`** — a read-only povrch: zoznam a live štatistika zostávajú, ale manuálny pridať a odstráni sú
  odstránené. Nodes iba kedy sa objavujú cez auto-objavovanie. `POST`/`DELETE /api/nodes` vrátenie **404**.
- **`Hidden`** — Nodes nav link a stránka sú úplne spálené a stránka trasa presmeruje na
  dashboard; manuálny pridať/odstrániť API je vypnutý. Klaster je auto-objavovanie iba.

**`RestrictNodesToOwner`** podlahy kto môže vidieť a spravovať uzly. Štandardne `false` drží štandardne
**admin-or-above** personál povrch (`AdminOrAbove`); nastaviť `true` na robiť to **vlastník-iba** (`Owner`). Obidve
spôsob **normálni používatelia nikdy nevidia uzly** — to iba vyberá vlastník-iba a širšiu personál povrch.

Node **auto-objavovanie je nepovídatelné obidvoma kľúče**: anonymný `POST /api/nodes/register` self-register
+ pulzuje koncový bod vždy funguje, takže a `Hidden`/`Monitor` nasadení stále rastie Its klaster
automaticky.

**Model:**

- `Core.Nodes.NodesUiMode` — `Full` / `Monitor` / `Hidden`.
- `Core.Nodes.NodesUiAccess` — jediný zdroj pravdy kompónujúce režim + vlastník-omedzenie:
  `IsPageVisible`, `AllowsManualManagement`, `RequiredPolicy(restrictToOwner)`. Nav
  (`NavMenu.razor`), stránka (`Pages/Nodes.razor`) a koncové body (`NodeEndpoints`) všetci čítajú to takže
  UI a API nikdy nemôže nesúhlas.
- `Core.Options.BrandingOptions.NodesUi` / `.RestrictNodesToOwner` — viazané z `App:Branding`.

## Nodes UI viditeľnosť — testy

- **Jednotka** — `UnitTests/Nodes/NodesUiAccessTests.cs`: stránka-viditeľnosť, manuálny-spravovanie a
  požadované-politika rozlíšenie cez každý režim + štandardne branding.
- **Integrácia** — `IntegrationTests/NodeUiGatingTests.cs`: cez reálny HTTP + Postgres — `Full` umožňuje a
  manuálny pridať, `Monitor`/`Hidden` 404 pridať a odstráni a `RestrictNodesToOwner` zakazuje admin zatiaľ
  vlastník stále čítajú zoznam.
- **E2E** — `E2ETests/NodesUiTests.cs` (štandardne `Full`: nav link + stránka + Nový Node tlačidlo vykresliť) a
  `E2ETests/NodesHiddenTests.cs` (`Hidden`: nav link spálené, `/nodes` presmeruje).

## Design tokeny (CSS premenné)

Branding tiež dosahuje app **vlastný** stylesheet + vlastné komponenty nie iba MudBlazor. `Web.Branding.BrandingCss.BuildRootVariables(BrandingOptions)` emituje branded palette ako CSS vlastné vlastnosti na `:root` (`--app-primary`, `--app-primary-hover`, `--app-surface`, `--app-appbar`, `--app-success`/`--app-error`/`--app-warning`/`--app-info`, …), injektovaný v `App.razor` priamo po `site.css`. `site.css` a každá komponent čítajú `var(--app-*)` — **žádny hard-coded farby** — takže a reseller palette tokov všade (prihlásenie hero, dno nav, pomocný tipy, offline stránka) zadarmo. Neutrálne povrchové tóny štandardne v `site.css :root`; `CustomCss` (injektovaný posledný) môžu prepísať žádny token. Pozrite si [ui-guidelines.md](../ui-guidelines.md) §2.

## Branded PWA

Inštalovateľný app je tiež označený — manifest koncový bod (`/manifest.webmanifest`) je postavený z `BrandingOptions` (`ProductName` → `name`/`short_name`, `Description`, `AppBarColor`/`BackgroundColor` → téma/background). Pozrite si [pwa.md](pwa.md).

## Testy

- **Jednotka** — `UnitTests/Branding/HexColorTests.cs`: platný/neplatný hex validácia.
- **Integrácia** — `IntegrationTests/ThemeBuildTests.cs`: farby mapa do palette, neplatná farba vyvolá;
  `IntegrationTests/BrandingHttpTests.cs`: vlastný `ProductName`/description/theme-colour vykresliť v podávané stránka `<head>` (WebApplicationFactory + Postgres), štandardne drží stock názov.
- **E2E** — `E2ETests/BrandingTests.cs`: branded názov produktu vykresluje v app bar v reálny prehliadač.
