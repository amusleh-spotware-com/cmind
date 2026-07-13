---
description: "Reseller пребрендирање апликације — назив производа, лого, favicon, боје, custom CSS — преко deployment конфигурације, без промене кода. Свака branding вредност подразумева stock..."
---

# White-label брендирање

Reseller пребрендирање апликације — назив производа, лого, favicon, боје, custom CSS — преко deployment конфигурације, без промене кода. Свака branding вредност **подразумева stock identity**: неконфигурисан deployment изгледа исто kao и пре; reseller превазилази само оно што треба.

## Модел

- `Core.Options.BrandingOptions` — везан из `App:Branding`. Стринг-базиран (config edge); свака боја валидирана када се тема гради.
- `Core.Branding.HexColor` — value object за CSS hex боју (`#RGB` / `#RRGGBB`), immutable, self-валидирајући.
  Неважећа боја баца `DomainException` (`domain.branding.color_invalid`) када се тема гради — погрешно конфигурисан deployment не успе fast при покретању, не рендерује покварену палетау.
- `Web.Components.Theme.Build(BrandingOptions)` — производи MudBlazor тему из branding-а. Само брендиране ставке палете долазе из config-а; типографиjа, layout, неутрални површински тонови остају фиксни тако да производ задржава кохерентан изглед преко reseller-а.
- `Web.Branding.IBrandingThemeProvider` — singleton, гради тему једном, ребилдује на промене опција.
  Убризгава се од стране `MainLayout`/`EmptyLayout` за `MudThemeProvider`, од стране app bar-а за назив производа/лого. `App.razor` чита `IOptionsMonitor<AppOptions>` директно за page `<head>` (title, description, favicon, theme-colour, custom CSS).

## Конфигурација

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

Форма променljиве окружења: `App__Branding__ProductName=AcmeFX`, `App__Branding__PrimaryColor=%232D7FF9`.

| Кључ | Ефекат | Подразумевано |
|-----|--------|---|
| `ProductName` | App-bar текст + page `<title>` | `cMind` |
| `LogoUrl` | App-bar лого слика; када je празно, приказуje се текст назива производа | *(empty)* |
| `FaviconUrl` | `<link rel="icon">` | `favicon.svg` |
| `Description` | `<meta name="description">` | stock description |
| `PrimaryColor` / `SecondaryColor` | accent, икона фиоца, дугмад | `#26C281` / `#1FB97A` |
| `AppBarColor` / `BackgroundColor` / `SurfaceColor` | chrome + површине; `AppBarColor` управља `<meta theme-color>` + PWA manifest `theme_color`, `BackgroundColor` manifest `background_color` | тамна палета |
| `SuccessColor` / `ErrorColor` / `WarningColor` / `InfoColor` | status боје | stock |
| `CustomCss` | убризгнути `<style>` у `<head>` (deployment-trusted) | *(empty)* |
| `ShowSiteLink` | прикажи "Powered by cMind" credit линк на командној табли | `true` |
| `RequireMfa` | захтевај од сваког корисника да подеси two-factor authentication пре коришћења апликације | `false` |
| `NodesUi` | колико од Nodes површине се испоручује: `Full` (листа + ручно add/delete), `Monitor` (read-only листа, без add/delete), `Hidden` (без nav, без странице, без ручног API-ja) | `Full` |
| `RestrictNodesToOwner` | када je `true`, само власник може да види/управља чворовима; у супротном цела admin-or-above staff површина може. Нормални корисници никада не виде чворове ни у једном случају | `false` |

Asset-ови референцирани од `LogoUrl`/`FaviconUrl` се сервирају из Web апликације `wwwroot` (нпр. mount `wwwroot/branding/` директоријум) или било koji апсолутни URL.

`App:Branding` валидира се при покретању (`BrandingOptionsValidator`, покренут преко `ValidateOnStart`): свака боја мора бити важећи hex, `CustomCss` не сме садржати `<`/`>` (не може да изађе из `<style>` тага). Погрешно конфигурисан deployment не успева да се покрене са јасном поруком, не рендерује покварену страницу.

## Powered-by линк

Командна tabla рендерује мали **"Powered by cMind"** credit линк koji показује на документациони сајт пројекта.
Контролисан je ca `App:Branding:ShowSiteLink` и je **`true` по подразумевању** — неконфигурисан
deployment га приказуje. Reseller koji покреће потпуно white-labeled инстанцу поставља
`App__Branding__ShowSiteLink=false` да га уклони потпуно.

Линк емитује командна tabla компонента и чита заставицу преко `IBrandingThemeProvider` /
`BrandingOptions`, тако да пребацивање je config-only промена (без ребилда). Види
[White-label for business](../white-label-for-business.md#the-powered-by-cmind-link) за бизнис-оријентисани преглед.

## Листа дозвољених брокера

White-label deployment може ограничити koji брокерови трговачки налози његови корисници могу да додају — тако да брокер
који покреће cMind за своје сопствене клијенте увек служи само своју књигу. Конфигурисано под `App:Accounts`:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Pepperstone", "IC Markets"]
    }
  }
}
```

Форма променljиве окружења: `App__Accounts__AllowedBrokers__0=Pepperstone`.

**Понашање:**

- **Празна листа (подразумевано) ⇒ неограничено.** Сваки брокер је дозвољен и **не покреће се верификација** —
  stock deployment je потпуно непромењен.
- **Непразна ⇒ ограничено.** cMind проверава сваки налог koji корисник покушава да дода против листе
  (case-insensitive):
  - **Open API (OAuth) линк** — назив брокера се пријављује ауторитативно од стране cTrader Open API, тако да
    недозвољени налог се једноставно **прескаче** (дозвољени налози у истом grant-у се и даље повезују);
    ауторизациона страница говори кориснику koji брокери су прескочени.
  - **Мануелни cID (username / password)** — корисников куцани брокер **није** поуздан. cMind **верификује**
    стварног брокера налога покретањем испоручених broker-probe cBot-а кроз cTrader CLI (читање
    `Account.BrokerName`) и перзистује то верификовано име. Недозвољени брокер се одбија са
    обавештењем; неуспех верификације (лоши креденцијали, нема чвора, timeout) се такође површинује, и налог
    се не додаје.

**Модел:**

- `Core.Options.AccountsOptions` — везан из `App:Accounts` (`AllowedBrokers`, `BrokerProbeTimeout`,
  `BrokerProbeAlgoPath`).
- `Core.Accounts.BrokerName` — value object (trimmed, case-insensitive equality).
- `Core.Accounts.BrokerAllowlist` — `IsRestricted` / `Allows(broker)`; празно = дозволи све. Наметнуто kao
  инваријанта унутра `CTraderIdAccount.AddTradingAccount` / `LinkOpenApiAccount`
  (`domain.account.broker_not_allowed`).
- `Core.Accounts.IBrokerVerifier` → `Web.Accounts.BrokerVerifier` — покреће probe контејнер на web
  хост-у (који има Docker socket), tail-ује логове и парсира брокера преко
  `Core.Accounts.BrokerProbeOutput`.позива се само када је allowlist ограничен.

**Broker-probe cBot:** преизграђени `broker-probe.algo` испоручује се са Web апликацијом (`src/Web/BrokerProbe/`,
копиран у излаз kao `broker-probe/broker-probe.algo`), тако да подразумевани
`App:Accounts:BrokerProbeAlgoPath` ресолвира out of the box — релативна путања се разрешава према app
base директоријуму, апсолутна путања се користи kao дата. Извор живи у `tools/broker-probe/`. Када
алго не постоји, мануелна-cID верификација не успева closed — налози под ограниченим allowlist-ом се и даље могу
повезати преко Open API путање, којој не треба probe.

## Листа дозвољених брокера — тестови

- **Unit** — `UnitTests/Accounts/`: `BrokerName`/`BrokerAllowlist` value objects, `BrokerProbeOutput`
  parser, и `CTraderIdAccount` allowlist инваријанта.
- **Integration** — `IntegrationTests/BrokerAllowlistTests.cs`: мануелни-cID ендпоинт са лажним верификатором
  (неограничен / верификован / недозвољен / верификација-неуспела) + Open API линкер koji прескаче недозвољене
  налоге. `BrokerVerifierLiveTests.cs` покреће **правег** probe када су cID креденцијали + алго дати
  (чисто прескаче у супротном).
- **E2E** — `E2ETests/BrokerAllowlistTests.cs`: ограничен deployment одбија мануелно додавање кроз
  прави UI и приказује обавештење "couldn't verify" (није додат ред налога).

## Видљивост Nodes UI-ја

Чворови су инфраструктура коју већина закупаца никада не управља ручно — cTrader CLI агенти се
[аутоматски региструју и heartbeat-ују](../operations/node-discovery.md), тако да white-label deployment може сакрити
ручне контроле, или Nodes површину потпуно, и и даље покретати здрав cluster кроз ауто-откриће.
Два config-only branding кључa управљају овим:

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

Форма променljиве окружења: `App__Branding__NodesUi=Hidden`, `App__Branding__RestrictNodesToOwner=true`.

**`NodesUi` — три режима:**

- **`Full` (подразумевано)** — stock производ: листа чворова плус ручне **New Node** и **Delete**
  контроле. `POST`/`DELETE /api/nodes` раде.
- **`Monitor`** — read-only површина: листа и live статистика остају, али се уклањају ручно додавање и брисање.
  Чворови се појављују само кроз аутоматско откриће. `POST`/`DELETE /api/nodes` враћају **404**.
- **`Hidden`** — Nodes nav линк и страница су потпуно нестали и рута странице преусмерава на командну
  таблу; ручни add/delete API je искljučen. Cluster je само аутоматско откриће.

**`RestrictNodesToOwner`** одређује ко може да види и управља чворовима. Подразумевано `false` чува стандардну
**admin-or-above** staff површину (`AdminOrAbove`); постави `true` да буде **само власник** (`Owner`). У оба
случаја **нормални корисници никада не виде чворове** — ово само бира између власника само и шире staff површине.

Аутоматско откриће чворова **није погођено оба кључa**: анонимни `POST /api/nodes/register` self-register
+ heartbeat ендпоинт увек ради, тако да `Hidden`/`Monitor` deployment и даље расте свој cluster
аутоматски.

**Модел:**

- `Core.Nodes.NodesUiMode` — `Full` / `Monitor` / `Hidden`.
- `Core.Nodes.NodesUiAccess` — јединствени извор истине koji саставља режим + owner-restriction:
  `IsPageVisible`, `AllowsManualManagement`, `RequiredPolicy(restrictToOwner)`. Nav
  (`NavMenu.razor`), страница (`Pages/Nodes.razor`) и ендпоинти (`NodeEndpoints`) сви га читају тако да
  UI и API никада не могу да се не слажу.
- `Core.Options.BrandingOptions.NodesUi` / `.RestrictNodesToOwner` — везани из `App:Branding`.

## Видљивост Nodes UI-ја — тестови

- **Unit** — `UnitTests/Nodes/NodesUiAccessTests.cs`: page-visibility, manual-management и
  required-policy резолуција преко сваког режима + подразумевани branding.
- **Integration** — `IntegrationTests/NodeUiGatingTests.cs`: преко правог HTTP + Postgres — `Full` дозвољава
  ручно додавање, `Monitor`/`Hidden` 404 add и delete, и `RestrictNodesToOwner` забрањује админа док
  власник и даље чита листу.
- **E2E** — `E2ETests/NodesUiTests.cs` (подразумевано `Full`: nav линк + страница + New Node дугме рендерују) и
  `E2ETests/NodesHiddenTests.cs` (`Hidden`: nav линк нестаје, `/nodes` преусмерава).

## Design tokens (CSS променljиве)

Брендирање такође досеже до **сопственог** stylesheet-а апликације + прилагођених компонената, не само MudBlazor. `Web.Branding.BrandingCss.BuildRootVariables(BrandingOptions)` емитује брендирану палетау kao CSS custom properties на `:root` (`--app-primary`, `--app-primary-hover`, `--app-surface`, `--app-appbar`, `--app-success`/`--app-error`/`--app-warning`/`--app-info`, …), убризгано у `App.razor` одмах након `site.css`. `site.css` и свака компонента чита `var(--app-*)` — **без хард-кодираних боја** — тако да reseller-ова палета протиче свуда (login hero, bottom nav, help tips, offline страница) бесплатно. Неутрални површински тонови подразумевају у `site.css :root`; `CustomCss` (убризгнут последњи) може да превазиђе било koji токен. Види [ui-guidelines.md](../ui-guidelines.md) §2.

## Брендирани PWA

Инсталабилна апликација je takođe брендирана — manifest ендпоинт (`/manifest.webmanifest`) je изграђен из `BrandingOptions` (`ProductName` → `name`/`short_name`, `Description`, `AppBarColor`/`BackgroundColor` → theme/background). Види [pwa.md](pwa.md).

## Тестови

- **Unit** — `UnitTests/Branding/HexColorTests.cs`: валидација важећег/неважећег hex-а.
- **Integration** — `IntegrationTests/ThemeBuildTests.cs`: боје мапиране у палетау, неважећа боја баца;
  `IntegrationTests/BrandingHttpTests.cs`: прилагођени `ProductName`/description/theme-colour рендерују се у сервираном page `<head>` (WebApplicationFactory + Postgres), подразумевани чувају stock назив.
- **E2E** — `E2ETests/BrandingTests.cs`: брендиран назив производа рендерује се у app bar у правом прегледачу.
