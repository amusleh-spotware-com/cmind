---
description: "Viszontelado atnevezi az alkalmazast - termeknev, logo, favicon, szinek, egyedi CSS - telepitesi konfiguran keresztul, kod valtoztatas nelkul. Minden branding ertek alapertelmezes szerint stock identitasra all."
---

# White-label branding

Viszontelado atnevezi az alkalmazast - termeknev, logo, favicon, szinek, egyedi CSS - telepitesi konfiguran keresztul, kod valtoztatas nelkul. Minden branding ertek **alapertelmezes szerint stock identitasra all**: konfigurálatlan telepites ugy nez ki, mint eddig; a viszontelado csak azt biralja felul, amid szukseges.

## Modell

- `Core.Options.BrandingOptions` - kotve `App:Branding`-bol. String-alapu (config edge); minden szin validalva van, amikor a tema epitul.
- `Core.Branding.HexColor` - value object CSS hex szinhez (`#RGB` / `#RRGGBB`), immutable, on-validalo. Ervenytelen szin `DomainException`-t dob (`domain.branding.color_invalid`), amikor a tema epitul - hibasan konfiguralt telepites gyorsan meghiusul az inditasnal, nem roppant palettat renderel.
- `Web.Components.Theme.Build(BrandingOptions)` - MudBlazor tema elohozasa a brandingbol. Csak a branded paletta bejegyzesek jonnek a konfigbol; tipografia, elrendezes, semleges felszin tonusok fixek maradnak, igy a termek koherens megjelenest tart a viszonteladokon at.
- `Web.Branding.IBrandingThemeProvider` - singleton, tema epitul egyszer, ujraepul az opciok valtozasara. Beinjektalva a `MainLayout`/`EmptyLayout` altal a `MudThemeProvider`-hez, az alkalmazassavhoz a termeknev/logoert. `App.razor` olvas `IOptionsMonitor<AppOptions>`-et direct a page `<head>`-hez (cim, leiras, favicon, theme-colour, egyedi CSS).

## Konfiguracio

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "Description": "AcmeFX - masolasi kereskedes es strategi automatizalas.",
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

Kornyezeti valtozo forma: `App__Branding__ProductName=AcmeFX`, `App__Branding__PrimaryColor=%232D7FF9`.

| Kulcs | Hatas | Alapertelmezes |
|-----|---------|---------|
| `ProductName` | Alkalmazassav szoveg + page `<title>` | `cMind` |
| `LogoUrl` | Alkalmazassav logo kep; ha ures, a termeknev szoveg jelenik meg | *(ures)* |
| `FaviconUrl` | `<link rel="icon">` | `favicon.svg` |
| `Description` | `<meta name="description">` | stock leiras |
| `PrimaryColor` / `SecondaryColor` | akcentus, drawer ikon, gombok | `#26C281` / `#1FB97A` |
| `AppBarColor` / `BackgroundColor` / `SurfaceColor` | chrom + felszinek; `AppBarColor` hajtja a `<meta theme-color>`-t + PWA manifest `theme_color`, `BackgroundColor` a manifest `background_color`-t | sotet paletta |
| `SuccessColor` / `ErrorColor` / `WarningColor` / `InfoColor` | allapot szinek | stock |
| `CustomCss` | befecskezett `<style>` a `<head>`-ben (telepitesi-megbizott) | *(ures)* |
| `ShowSiteLink` | megmutatja a "Powered by cMind" hitelkent linket a muszerfalon | `true` |
| `RequireMfa` | megkoveteli minden felhasznalotol a ket-faktoros hitelesites beallitasat, mielott az alkalmazast hasznalna | `false` |
| `NodesUi` | mennyit lathat a Nodes feluletbol: `Full` (lista + kezdi hozzaadas/torles), `Monitor` ( csak-olvasas lista, nincs hozzaadas/torles), `Hidden` (nincs nav, nincs page, nincs kezdi API) | `Full` |
| `RestrictNodesToOwner` | ha `true`, csak a tulajdonos lathatja/kezelheti a csomopontokat; kulonben a teljes admin-vagy-felett alkalmazotti felulet. Normal felhasznalok soha nem latjak a csomopontokat egyik esetben sem | `false` |

A `LogoUrl`/`FaviconUrl` altal hivatkozott asset-ek a Web alkalmazas `wwwroot`-bol vannak kiszolgalva (pl. mount `wwwroot/branding/` mappa) vagy barmely abszolut URL-bol.

`App:Branding` validalva az inditasnal (`BrandingOptionsValidator`, futtatva `ValidateOnStart`-on): minden szinnek ervényes hexnek kell lennie, a `CustomCss` nem tartalmazhat `<`/`>` (nem törhet ki a `<style>` tag-bol). Hibasan konfiguralt telepites nem bootol, tiszta uzenettel, nem törött oldalt renderel.

## Powered-by link

A muszerfal egy kicsi **"Powered by cMind"** hitelkent linket renderel, amely a projekt dokumentacios oldalara mutat. Az `App:Branding:ShowSiteLink` vezerli, es **`true` az alapertelmezes** - egy konfigurálatlan telepites megjeleniti. Egy viszontelado, aki teljesen white-labelezett instance-t futtat, beallitja az `App__Branding__ShowSiteLink=false`-t, hogy teljesen eltavolitasa.

A linket a muszerfal komponens bocsátja ki es az `IBrandingThemeProvider` / `BrandingOptions` altal olvassa a flaget, igy az atkapcsolasa csak konfiguracios valtoztatas (nincs ujraepites). Laskad [White-label uzleti felhasznaloknak](../white-label-for-business.md#the-powered-by-cmind-link) az uzleti-osszefoglaloert.

## Broker engedelyezesi lista

Egy white-label telepites korlatozhatja, mely broker kereskedesi szamlait a felhasznalok hozzaadhatjak - igy egy broker, aki a cMind-et a sajat kliensei szamara futtatja, csak a sajat konyvet szolgalja. Konfiguralva az `App:Accounts` alatt:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Pepperstone", "IC Markets"]
    }
  }
}
```

Kornyezeti valtozo forma: `App__Accounts__AllowedBrokers__0=Pepperstone`.

**Viselkedes:**

- **Ures lista (alapertelmezes) => korlatlan.** Minden broker engedelyezve es **nincs ellenorzes fut** - egy stock telepites teljesen valtozatlan.
- **Nem ures => korlatozott.** A cMind ellenorzi minden szamlat, amit egy felhasznalo probal hozzaadni, a lista ellen (case-insensitive):
  - **Open API (OAuth) link** - a broker nevet a cTrader Open API authoritativan jelenti, igy egy nem engedelyezett szamla egyszeruen **ki van hagyva** (az engedelyezett szamlak ugyanabban a grant-ban meg mind linkelodnek); az engedelyezesi oldal megmondja a felhasznalonak, mely brokerok lettek kihagyva.
  - **Kezik cID (username / jelszo)** - a felhasznalo altal gepelt broker nem megbizhato. A cMind **ellenoriz** a szamla valodi brokerjet a szallitott broker-probe cBot cTrader CLI-n keresztuli futtatasaval (`Account.BrokerName` olvasasa) es perzisztalja azt a verified nevet. Egy nem engedelyezett broker elutasitva, ertesitessel; egy ellenorzesi hiba (rossz creds, nincs csomopont, timeout) szinten felszine-re hozva, es a szamla nem ad hozza.

**Modell:**

- `Core.Options.AccountsOptions` - kotve `App:Accounts`-bol (`AllowedBrokers`, `BrokerProbeTimeout`, `BrokerProbeAlgoPath`).
- `Core.Accounts.BrokerName` - value object (trimmed, case-insensitive equality).
- `Core.Accounts.BrokerAllowlist` - `IsRestricted` / `Allows(broker)`; ures = mindent enged. Kiertekelve mint invariant a `CTraderIdAccount.AddTradingAccount` / `LinkOpenApiAccount` mogott (`domain.account.broker_not_allowed`).
- `Core.Accounts.IBrokerVerifier` => `Web.Accounts.BrokerVerifier` - futtatja a probe konténert a web gazdagen (ami rendelkezik a Docker socket-tel), tail-eli a logokat, es parse-olja a brokert a `Core.Accounts.BrokerProbeOutput` reven. Csak akkor hivodik, amikor az engedelyezesi lista korlatozott.

**Broker-probe cBot:** egy elorepitett `broker-probe.algo` szallitva a Web alkalmazassal (`src/Web/BrokerProbe/`, masolva a kimenetbe mint `broker-probe/broker-probe.algo`), igy az alapertelmezett `App:Accounts:BrokerProbeAlgoPath` dobozabol kiolvashato - egy relativ eleresi ut az alkalmazas alkonyvtarahoz viszonyitva oldodik fel, egy abszolut eleresi ut az adottkent hasznalt. Amikor az algo hianyzik, a kezi-cID ellenorzes fail-closed - a korlatozott engedelyezesi lista alatti szamlak tovabbra is linkedhetok az Open API utvonalon keresztul, amelynek nincs szuksege probe-ra.

## Broker engedelyezesi lista - tesztek

- **Unit** - `UnitTests/Accounts/`: `BrokerName`/`BrokerAllowlist` value objektumok, `BrokerProbeOutput` parser, es a `CTraderIdAccount` engedelyezesi lista invariánsa.
- **Integracio** - `IntegrationTests/BrokerAllowlistTests.cs`: kezi-cID vegpont hamis ellenorrel (korlatlan / ellenorzott / nem engedelyezett / ellenorzes sikertelen) + Open API linker, ami atugorja a nem engedelyezett szamlakat. `BrokerVerifierLiveTests.cs` a **valo** probe-ot futtatja, amikor cID creds + az algo megadott (tiszta atugaras kulonben).
- **E2E** - `E2ETests/BrokerAllowlistTests.cs`: egy korlatozott telepites elutasitja a kezi hozzaadast a valodi UI-n at es megmutatja a "nem sikerult ellenorizni" ertesitest (nincs szamla sor hozzaadva).

## Nodes UI lathatosag

A csomopontok olyan infrastruktura, amelyet a legtobb bermelo soha nem kezel kezzel - a cTrader CLI ugynokok [önregisztraljak magukat es szivveressel](../operations/node-discovery.md), igy egy white-label telepites elrejtheti a kezi vezerloket, vagy a Csomopontok feluletet teljesen, es meg mindig egeszseges klasztert mukdt a automatikus felfedezesen keresztul. Ket konfiguracios-only branding kulcs szabalyozza ezt:

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

Kornyezeti valtozo forma: `App__Branding__NodesUi=Hidden`, `App__Branding__RestrictNodesToOwner=true`.

**`NodesUi` - harom mod:**

- **`Full` (alapertelmezes)** - a stock termek: a csomopont lista plusz a kezi **Uj csomopont** es **Torles** vezerlok. `POST`/`DELETE /api/nodes` mukodik.
- **`Monitor`** - csak-olvasasi felulet: a lista es az elo statisztika marad, de a kezi hozzaadas es torles el van tavolitva. A csomopontok csak automatikus felfedezesen keresztul jelennek meg. `POST`/`DELETE /api/nodes` **404**-et ad vissza.
- **`Hidden`** - a Csomopontok nav link es oldal teljesen eltavolitva es az oldal utvonal atiranyit a muszerfalra; a kezi hozzaadas/torles API ki van kapcsolva. A klaszter csak automatikus felfedezes.

**`RestrictNodesToOwner`** padlozza, ki lathatja es kezelheti a csomopontokat. `false` alapertelmezes megtartja a standard **admin-vagy-felett** alkalmazotti feluletet (`AdminOrAbove`); `true`-ra allitas **csak tulajdonosra** korlatozza (`Owner`). Barkepp esetben **a normal felhasznalok soha nem latjak a csomopontokat** - ez csak a tulajonos-kizarolagos es a szelesebb alkalmazotti felulet kozotti valasztast vegzi.

A csomopont **automatikus felfedezes nem erintett egyik kulcs altal sem**: az anonim `POST /api/nodes/register` onregisztracio + szivveres vegpont mindig mukodik, igy egy `Hidden`/`Monitor` telepites meg mindig automatikusan noveli a klasztert.

**Modell:**

- `Core.Nodes.NodesUiMode` - `Full` / `Monitor` / `Hidden`.
- `Core.Nodes.NodesUiAccess` - az egyetlen forrasa az igazsagnak, amely a modot + tulajonos-korlabtast komponalja: `IsPageVisible`, `AllowsManualManagement`, `RequiredPolicy(restrictToOwner)`. Nav (`NavMenu.razor`), az oldal (`Pages/Nodes.razor`) es a vegpontok (`NodeEndpoints`) mind ebbdl olvassak, igy az UI es az API soha nem értékelhet egyet.
- `Core.Options.BrandingOptions.NodesUi` / `.RestrictNodesToOwner` - kotve `App:Branding`-bol.

## Nodes UI lathatosag - tesztek

- **Unit** - `UnitTests/Nodes/NodesUiAccessTests.cs`: oldal-lathatosag, kezi-kezelés es szukseges-policy feloldas minden mod + alap branding felett.
- **Integracio** - `IntegrationTests/NodeUiGatingTests.cs`: valodi HTTP + Postgres felett - `Full` engedelyezi a kezi hozzaadast, `Monitor`/`Hidden` 404 add es delete, es `RestrictNodesToOwner` tilt egy admint, mikozben a tulajonos meg olvassa a listat.
- **E2E** - `E2ETests/NodesUiTests.cs` (alapertelmezes `Full`: nav link + oldal + Uj csomopont gomb renderel) es `E2ETests/NodesHiddenTests.cs` (`Hidden`: nav link eltavolitva, `/nodes` atiranyit).

## Dizajn tokenek (CSS valtozok)

A branding az alkalmazas **sajat** stiluslapjat is eleri + egyedi komponenseket, nem csak a MudBlazor. `Web.Branding.BrandingCss.BuildRootVariables(BrandingOptions)` kibocsátja a branded palettat CSS egyedi tulajdonságokként a `:root`-on (`--app-primary`, `--app-primary-hover`, `--app-surface`, `--app-appbar`, `--app-success`/`--app-error`/`--app-warning`/`--app-info`, ...), befecskezve az `App.razor`-be közvetlen a `site.css` után. A `site.css` es minden komponens a `var(--app-*)`-t olvassa - **nincs keménykódolt szín** - igy a viszontelado palettája mindenhol ingyen folyik (bejelentkezesi hero, bottom nav, help tippek, offline oldal). Semleges felszín tónusok alapertelmezes szerint a `site.css :root`-ban vannak; a `CustomCss` (legutoljara befecskezve) bármely token-t felulírhat. Lásd [ui-guidelines.md](../ui-guidelines.md) 2. szakasz.

## Branded PWA

A telepitheto alkalmazás is branded - a manifest vegpont (`/manifest.webmanifest`) a `BrandingOptions`-ból van epleitve (`ProductName` → `name`/`short_name`, `Description`, `AppBarColor`/`BackgroundColor` → theme/background). Lásd [pwa.md](pwa.md).

## Tesztek

- **Unit** - `UnitTests/Branding/HexColorTests.cs`: érvényes/érvénytelen hex validáció.
- **Integracio** - `IntegrationTests/ThemeBuildTests.cs`: színek map-pelődnek a palettába, érvénytelen szín dob; `IntegrationTests/BrandingHttpTests.cs`: egyedi `ProductName`/leírás/theme-colour renderel a kiszolgált oldal `<head>`-ben (WebApplicationFactory + Postgres), alapertelmezoek megtartjak a stock nevet.
- **E2E** - `E2ETests/BrandingTests.cs`: branded terméknév renderel az alkalmazássávon a valódi böngészőben.
