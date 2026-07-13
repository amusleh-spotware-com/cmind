---
description: "Preprodajalec prevetri aplikacijo — ime izdelka, logotip, favicon, barve, custom CSS — prek namestitvene konfiguracije, brez spremembe kode. Vsaka vrednost blagovne znamke…"
---

# White-label blagovna znamka

Preprodajalec prevetri aplikacijo — ime izdelka, logotip, favicon, barve, custom CSS — prek namestitvene konfiguracije, brez spremembe kode. Vsaka vrednost blagovne znamke **privzeto na stock identity**: nenastavljena namestitev izgleda enako kot prej; preprodajalec povozi samo kar potrebuje.

## Model

- `Core.Options.BrandingOptions` — vezano iz `App:Branding`. Nizovno (rob konfiguracije); vsaka barva validirana ko je tema zgrajena.
- `Core.Branding.HexColor` — vrednostni objekt za CSS hex barvo (`#RGB` / `#RRGGBB`), nespremenljiv, samo-validirajoč.
  Neveljavna barva vrže `DomainException` (`domain.branding.color_invalid`) ko je tema zgrajena — narobe konfigurirana namestitev hitro propade ob startu, ne upodobi pokvarjeno paleto.
- `Web.Components.Theme.Build(BrandingOptions)` — proizvedi MudBlazor temo iz blagovne znamke. Samo branded vnose palete prihajajo iz konfiguracije; tipografija, postavitev, nevtralni tonski ostanejo fiksni, tako da izdelek ohranja koherentno zunanjost čez preprodajalce.
- `Web.Branding.IBrandingThemeProvider` — singleton, zgradi temo enkrat, obnovi ob spremembi možnosti.
  Injiciran od `MainLayout`/`EmptyLayout` za `MudThemeProvider`, od app bara za ime izdelka/logotip. `App.razor` bere `IOptionsMonitor<AppOptions>` naravnost za page `<head>` (naslov, opis, favicon, theme-colour, custom CSS).

## Konfiguracija

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

Oblika spremenljivke okolja: `App__Branding__ProductName=AcmeFX`, `App__Branding__PrimaryColor=%232D7FF9`.

| Ključ | Učinek | Privzeto |
|-------|--------|----------|
| `ProductName` | Besedilo app bara + `<title>` strani | `cMind` |
| `LogoUrl` | Slika logotipa app bara; ko je prazen, kaže besedilo imena izdelka | *(prazen)* |
| `FaviconUrl` | `<link rel="icon">` | `favicon.svg` |
| `Description` | `<meta name="description">` | stock opis |
| `PrimaryColor` / `SecondaryColor` | poudarek, ikona risalnika, gumbi | `#26C281` / `#1FB97A` |
| `AppBarColor` / `BackgroundColor` / `SurfaceColor` | chrome + površine; `AppBarColor` poganja `<meta theme-color>` + PWA manifest `theme_color`, `BackgroundColor` manifest `background_color` | temna paleta |
| `SuccessColor` / `ErrorColor` / `WarningColor` / `InfoColor` | status barve | stock |
| `CustomCss` | vbrizgan `<style>` v `<head>` (zaupano ob namestitvi) | *(prazen)* |
| `ShowSiteLink` | prikaži "Powered by cMind" povezavo na nadzorni plošči | `true` |
| `RequireMfa` | zahtevaj od vsakega uporabnika naj nastavi dvofaktorsko avtentikacijo pred uporabo aplikacije | `false` |
| `NodesUi` | koliko Nodes površine ladja: `Full` (seznam + ročno dodaj/izbriši), `Monitor` (samo za branje, brez dodajanja/brisanja), `Hidden` (brez nav, brez strani, brez ročnega API) | `Full` |
| `RestrictNodesToOwner` | ko `true`, samo lastnik sme videti/upravljati vozlišča; sicer ves admin-ali-nad uporabniški sloj. Navadni uporabniki nikoli ne vidijo vozlišč | `false` |

Sredstva referecirana z `LogoUrl`/`FaviconUrl` servirana iz Web app `wwwroot` (npr. mount `wwwroot/branding/` mapa) ali katerakoli absolutna URL.

`App:Branding` validirano ob zagonu (`BrandingOptionsValidator`, teče prek `ValidateOnStart`): vsaka barva mora biti veljaven hex, `CustomCss` ne sme vsebovati `<`/`>` (ne more uiti iz `<style>` oznake). Narobe konfigurirana namestitev ne zažene se z jasnim sporočilom, ne upodobi pokvarjene strani.

## Povezava Powered-by

Nadzorna plošča upodobi majhno **"Powered by cMind"** povezavo ki kaže na dokumentacijsko stran projekta. Nadzorovano z `App:Branding:ShowSiteLink` in je **`true` privzeto** — nenastavljena namestitev jo prikaže. Preprodajalec ki poganja popolnoma white-label instanco nastavi `App__Branding__ShowSiteLink=false` da jo v celoti odstrani.

Povezava oddana od nadzorne plošče in bere zastavico prek `IBrandingThemeProvider` /
`BrandingOptions`, torej preklapljanje je sprememba samo konfiguracije (brez prevzgoje). Glej
[White-label za podjetja](../white-label-for-business.md#the-powered-by-cmind-link) za poslovni povzetek.

## Belo listino dovoljenih brokerjev

White-label namestitev lahko omeji katere brokerjeve trgovalne račune njeni uporabniki lahko dodajo — torej broker
ki poganja cMind za lastne stranke vedno streže samo svoji knjigi. Konfigurirano pod `App:Accounts`:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Pepperstone", "IC Markets"]
    }
  }
}
```

Oblika spremenljivke okolja: `App__Accounts__AllowedBrokers__0=Pepperstone`.

**Vedenje:**

- **Prazen seznam (privzeto) ⇒ neomejeno.** Vsak broker dovoljen in **ne teče verifikacija** — stock
  namestitev je popolnoma nespremenjena.
- **Neprazen ⇒ omejeno.** cMind preveri vsak račun ki ga uporabnik poskusi dodati proti seznamu
  (neobčutljivo na velikost črk):
  - **Open API (OAuth) povezava** — ime brokera je avtoritativno poročano od cTrader Open API, torej
    nedovoljen račun je preprosto **preskočen** (dovoljeni računi v isti pooblastitvi še vedno povezani);
    avtorizacijska stran pove uporabniku katere brokerje je preskočil.
  - **Ročni cID (uporabniško ime / geslo)** — uporabnik vtipkan broker **ni zaupan**. cMind **preveri**
    resničnega brokera računa z zagonom priloženega broker-probe cBot prek cTrader CLI (beremo
    `Account.BrokerName`) in vztraja to verificirano ime. Nedovoljen broker zavrnjen z
    obvestilom; napaka verifikacije (slaba poverilnica, nobeno vozlišče, timeout) prav tako površinska, in račun
    ni dodan.

**Model:**

- `Core.Options.AccountsOptions` — vezano iz `App:Accounts` (`AllowedBrokers`, `BrokerProbeTimeout`,
  `BrokerProbeAlgoPath`).
- `Core.Accounts.BrokerName` — vrednostni objekt (obrezan, neobčutljiv na velikost črk).
- `Core.Accounts.BrokerAllowlist` — `IsRestricted` / `Allows(broker)`; prazen = dovoli vse. Uveljavljen kot
  invariant znotraj `CTraderIdAccount.AddTradingAccount` / `LinkOpenApiAccount`
  (`domain.account.broker_not_allowed`).
- `Core.Accounts.IBrokerVerifier` → `Web.Accounts.BrokerVerifier` — zažene prob container na spletnem
  gostitelju (ki ima Docker socket), sledi dnevnikom in razčleni brokera prek
  `Core.Accounts.BrokerProbeOutput`. Poklicano samo ko je bela lista omejena.

**Broker-probe cBot:** vnaprej zgrajen `broker-probe.algo` ladij s Web aplikacijo (`src/Web/BrokerProbe/`,
kopirano v izhod kot `broker-probe/broker-probe.algo`), torej privzeto
`App:Accounts:BrokerProbeAlgoPath` reši iz škatle — relativna pot je razrešena glede na app
base directory, absolutna pot je uporabljena kot podana. Ko je algo odsoten, ročna-cID verifikacija
propade zaprto — računi pod omejeno belo listo lahko še vedno povezani prek Open API poti, ki ne potrebuje probe.

## Bela lista broker — testi

- **Enote** — `UnitTests/Accounts/`: `BrokerName`/`BrokerAllowlist` vrednostni objekti, `BrokerProbeOutput`
  parser, in `CTraderIdAccount` allowlist invariant.
- **Integracija** — `IntegrationTests/BrokerAllowlistTests.cs`: ročna-cID končna točka s ponaredkom verifier
  (neomejeno / verificirano / nedovoljeno / verifikacija-failed) + Open API linker preskoči nedovoljene
  račune. `BrokerVerifierLiveTests.cs` zažene **resničnega** probe ko so na voljo cID poverilnice + algo
  (preskoči gladko drugače).
- **E2E** — `E2ETests/BrokerAllowlistTests.cs`: omejena namestitev zavrne ročno dodajanje prek
  pravega UI in prikaže "ni bilo mogoče verificirati" obvestilo (brez vrstice računa dodane).

## Vidljivost Nodes uporabniškega vmesnika

Vozlišča so infrastruktura ki je večina najemnikov nikoli ne upravlja ročno — cTrader CLI agenti
[avto-registrirajo in heartbeat](../operations/node-discovery.md), torej white-label namestitev lahko skrije
ročne kontrole ali celotno Nodes površino, in še vedno poganja zdrav gručo skozi avto-odkritje.
Dve konfiguracijski ključi blagovne znamke nadzorujeta to:

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

Oblika spremenljivke okolja: `App__Branding__NodesUi=Hidden`, `App__Branding__RestrictNodesToOwner=true`.

**`NodesUi` — trije načini:**

- **`Full` (privzeto)** — stock izdelek: seznam vozlišč plus ročne **Novo vozlišče** in **Izbriši**
  kontrole. `POST`/`DELETE /api/nodes` delujeta.
- **`Monitor`** — površina samo za branje: seznam in žive statistike ostaneta, vendar sta ročno dodajanje in brisanje
  odstranjena. `POST`/`DELETE /api/nodes` vrneta **404**.
- **`Hidden`** — Nodes nav povezava in stran sta v celoti odstranjeni in ruta strani preusmeri na
  nadzorno ploščo; ročno dodajanje/brisanje API je off. Gruča je avto-odkritje samo.

**`RestrictNodesToOwner`** določa kdo lahko vidi in upravlja vozlišča. Privzeto `false` ohranja standardni
**admin-ali-nad** uporabniški sloj (`AdminOrAbove`); nastavi `true` da bo **samo-lastnik** (`Owner`). V vsakem
primeru **navadni uporabniki nikoli ne vidijo vozlišč** — to samo izbira med samo-lastnikom in širšim uporabniškim slojem.

Avto-odkritje vozlišč **ni prizadeto s katerimkoli ključem**: anonimna `POST /api/nodes/register` končna točka za
samo-registracijo + heartbeat vedno deluje, torej `Hidden`/`Monitor` namestitev še vedno raste svojo gručo
avtomatsko.

**Model:**

- `Core.Nodes.NodesUiMode` — `Full` / `Monitor` / `Hidden`.
- `Core.Nodes.NodesUiAccess` — en sam vir resnice ki združuje način + omejitev lastnika:
  `IsPageVisible`, `AllowsManualManagement`, `RequiredPolicy(restrictToOwner)`. Nav
  (`NavMenu.razor`), stran (`Pages/Nodes.razor`) in končne točke (`NodeEndpoints`) vse berejo, torej
  UI in API se nikoli ne razideta.
- `Core.Options.BrandingOptions.NodesUi` / `.RestrictNodesToOwner` — vezano iz `App:Branding`.

## Vidljivost Nodes UI — testi

- **Enote** — `UnitTests/Nodes/NodesUiAccessTests.cs`: vidljivost strani, ročno upravljanje in
  potrebna politika resolucije čez vsak način + privzeto blagovno znamko.
- **Integracija** — `IntegrationTests/NodeUiGatingTests.cs`: čez resničen HTTP + Postgres — `Full` dovoli
  ročno dodajanje, `Monitor`/`Hidden` 404 dodaj in izbriši, in `RestrictNodesToOwner` prepove adminu medtem ko
  lastnik še vedno bere seznam.
- **E2E** — `E2ETests/NodesUiTests.cs` (privzeto `Full`: nav povezava + stran + gumb Novo vozlišče upodobita) in
  `E2ETests/NodesHiddenTests.cs` (`Hidden`: nav povezava izginila, `/nodes` preusmeri).

## Oblikovalski žetoni (CSS spremenljivke)

Blagovna znamka prav tako doseže **lasten** stil aplikacije + custom komponente, ne samo MudBlazor. `Web.Branding.BrandingCss.BuildRootVariables(BrandingOptions)` oddaja branded paleto kot CSS lastnine na `:root` (`--app-primary`, `--app-primary-hover`, `--app-surface`, `--app-appbar`, `--app-success`/`--app-error`/`--app-warning`/`--app-info`, …), vbrizgano v `App.razor` takoj za `site.css`. `site.css` in vsaka komponenta bere `var(--app-*)` — **brez trdo kodiranih barv** — torej preprodajalčeva paleta teče povsod (login hero, spodnja navigacija, help tips, offline stran) zastonj. Nevtralni tonski privzeti v `site.css :root`; `CustomCss` (vbrizgan zadnji) lahko povozi katerikoli žeton. Glej [ui-guidelines.md](../ui-guidelines.md) §2.

## Branded PWA

Nameščljiva aplikacija je prav tako blagovno znamčena — manifest končna točka (`/manifest.webmanifest`) je zgrajena iz `BrandingOptions` (`ProductName` → `name`/`short_name`, `Description`, `AppBarColor`/`BackgroundColor` → theme/background). Glej [pwa.md](pwa.md).

## Testi

- **Enote** — `UnitTests/Branding/HexColorTests.cs`: valid/invalid hex validacija.
- **Integracija** — `IntegrationTests/ThemeBuildTests.cs`: barve preslikajo v paleto, neveljavna barva vrže;
  `IntegrationTests/BrandingHttpTests.cs`: custom `ProductName`/opis/theme-colour upodobljen v servirani strani `<head>` (WebApplicationFactory + Postgres), privzeti ohranjajo stock ime.
- **E2E** — `E2ETests/BrandingTests.cs`: blagovno znamčeno ime izdelka upodobljeno v app baru v resničnem brskalniku.
