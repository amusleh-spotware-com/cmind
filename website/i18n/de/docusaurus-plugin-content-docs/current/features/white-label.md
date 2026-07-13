---
description: "Wiederverkäufer-Branden der App – Produktname, Logo, Favicon, Farben, Custom CSS – über Deployment-Config, ohne Code-Änderung. Jeder Branding-Wert defaultet auf den Standard-Identität…"
---

# White-Label-Branding

Wiederverkäufer brarden die App – Produktname, Logo, Favicon, Farben, Custom CSS – über Deployment-Config,
ohne Code-Änderung. Jeder Branding-Wert **defaultet auf die Standard-Identität**: eine unkonfigurierte
Bereitstellung sieht gleich aus wie vorher; der Wiederverkäufer überschreibt nur, was er braucht.

## Modell

- `Core.Options.BrandingOptions` — gebunden von `App:Branding`. String-basiert (Config-Edge); jede
  Farbe wird beim Theme-Build validiert.
- `Core.Branding.HexColor` — Value Object für CSS-Hex-Farben (`#RGB` / `#RRGGBB`), immutable,
  selbst-validierend. Ungültige Farbe wirft `DomainException` (`domain.branding.color_invalid`) beim
  Theme-Build – Fehlkonfigurierte Bereitstellung schlägt beim Startup schnell fehl, statt eine
  kaputte Palette zu rendern.
- `Web.Components.Theme.Build(BrandingOptions)` — erzeugt MudBlazor-Theme aus Branding. Nur die
  gebrandeten Palette-Einträge kommen aus der Config; Typografie, Layout, neutrale Oberflächentöne
  bleiben fix, damit das Produkt über Wiederverkäufer hinweg ein kohärentes Aussehen behält.
- `Web.Branding.IBrandingThemeProvider` — Singleton, baut Theme einmal, rebuild bei Options-Änderung.
  Injiziert von `MainLayout`/`EmptyLayout` für `MudThemeProvider`, von der App-Bar für
  Produktname/Logo. `App.razor` liest `IOptionsMonitor<AppOptions>` direkt für `<head>` der Seite
  (Title, Description, Favicon, Theme-Colour, Custom CSS).

## Konfiguration

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "Description": "AcmeFX — Copy-Trading und Strategieautomatisierung.",
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

Umgebungsvariablen-Form: `App__Branding__ProductName=AcmeFX`,
`App__Branding__PrimaryColor=%232D7FF9`.

| Schlüssel | Effekt | Standard |
|---------|--------|---------|
| `ProductName` | App-Bar-Text + Seiten-<title> | `cMind` |
| `LogoUrl` | App-Bar-Logo-Bild; wenn leer, zeigt Produktnamens-Text | *(leer)* |
| `FaviconUrl` | `<link rel="icon">` | `favicon.svg` |
| `Description` | `<meta name="description">` | Standard-Beschreibung |
| `PrimaryColor` / `SecondaryColor` | Akzent, Schubladen-Icon, Buttons | `#26C281` / `#1FB97A` |
| `AppBarColor` / `BackgroundColor` / `SurfaceColor` | Chrome + Oberflächen; `AppBarColor` treibt `<meta theme-color>` + PWA-Manifest `theme_color`, `BackgroundColor` das Manifest `background_color` | dunkle Palette |
| `SuccessColor` / `ErrorColor` / `WarningColor` / `InfoColor` | Statusfarben | Standard |
| `CustomCss` | injiziertes `<style>` in `<head>` (Deployment-vertraut) | *(leer)* |
| `ShowSiteLink` | Den „Powered by cMind"-Credit-Link auf dem Dashboard anzeigen | `true` |
| `RequireMfa` | Jedem Benutzer.require, Zwei-Faktor-Authentifizierung einzurichten, bevor er die App nutzt | `false` |
| `NodesUi` | Wie viel von der Nodes-Oberfläche ausgeliefert wird: `Full` (Liste + manuelles Hinzufügen/Löschen), `Monitor` (Read-Only-Liste, kein Hinzufügen/Löschen), `Hidden` (keine Nav, keine Seite, keine manuelle API) | `Full` |
| `RestrictNodesToOwner` | Wenn `true`, darf nur der Eigentümer Nodes sehen/verwalten; sonst die gesamte Admin-oder-mehr-Staff-Oberfläche. Normale Benutzer sehen Nodes in beiden Fällen nie | `false` |

Assets, referenziert durch `LogoUrl`/`FaviconUrl`, werden aus dem Web-App-`wwwroot` bedient (z.B.
`wwwroot/branding/`-Ordner einhängen) oder jeder absoluten URL.

`App:Branding` wird beim Startup validiert (`BrandingOptionsValidator`, ausgeführt via `ValidateOnStart`):
Jede Farbe muss gültiges Hex sein, `CustomCss` darf kein `<`/`>` enthalten (kann nicht aus dem
`<style>`-Tag ausbrechen). Fehlkonfigurierte Bereitstellung bootet nicht mit klarer Meldung, statt
kaputte Seite zu rendern.

## Powered-by Link

Das Dashboard rendert einen kleinen **„Powered by cMind"**-Credit-Link, der auf die
Projektdokumentationsseite zeigt. Er wird durch `App:Branding:ShowSiteLink` gesteuert und ist
**`true` standardmäßig** — eine unkonfigurierte Bereitstellung zeigt ihn. Ein Wiederverkäufer,
der eine vollständig white-labelte Instanz betreibt, setzt `App__Branding__ShowSiteLink=false`, um
ihn vollständig zu entfernen.

Der Link wird von der Dashboard-Komponente emittiert und liest das Flag durch
`IBrandingThemeProvider` / `BrandingOptions`, sodass das Umschalten eine Config-Änderung ist (kein
Rebuild). Siehe [White-Label für Unternehmen](../white-label-for-business.md#the-powered-by-cmind-link)
für die geschäftliche Zusammenfassung.

## Broker-Allowlist

Eine White-Label-Bereitstellung kann einschränken, welche Broker-Trading-Konten ihre Benutzer
hinzufügen dürfen — sodass ein Broker, der cMind für seine eigenen Kunden betreibt, jemals nur sein
eigenes Buch bedient. Konfiguriert unter `App:Accounts`:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Pepperstone", "IC Markets"]
    }
  }
}
```

Umgebungsvariablen-Form: `App__Accounts__AllowedBrokers__0=Pepperstone`.

**Verhalten:**

- **Leere Liste (Standard) ⇒ uneingeschränkt.** Jeder Broker ist erlaubt und **keine Verifizierung
  läuft** — eine Stock-Bereitstellung ist vollständig unverändert.
- **Nicht-leer ⇒ eingeschränkt.** cMind prüft jedes Konto, das ein Benutzer hinzuzufügen versucht,
  gegen die Liste (case-insensitive):
  - **Open API (OAuth) Verknüpfung** — der Broker-Name wird autoritativ von der cTrader Open API
    gemeldet, also wird ein nicht erlaubtes Konto einfach **übersprungen** (erlaubte Konten in derselben
    Berechtigung werden noch verknüpft); die Autorisierungsseite teilt dem Benutzer mit, welche Broker
    übersprungen wurden.
  - **Manuelle cID (Benutzername / Passwort)** — der vom Benutzer eingegebene Broker wird **nicht**
    vertraut. cMind **verifiziert** den echten Broker des Kontos, indem es den mitgelieferten
    Broker-Probe-cBot durch die cTrader CLI ausführt (liest `Account.BrokerName`) und diesen
    verifizierten Namen persistiert. Ein nicht erlaubter Broker wird mit einer Benachrichtigung
    abgelehnt; ein Verifizierungsfehler (falsche Anmeldedaten, kein Node, Timeout) wird ebenfalls
    gemeldet, und das Konto wird nicht hinzugefügt.

**Modell:**

- `Core.Options.AccountsOptions` — gebunden von `App:Accounts` (`AllowedBrokers`,
  `BrokerProbeTimeout`, `BrokerProbeAlgoPath`).
- `Core.Accounts.BrokerName` — Value Object (getrimmt, Case-insensitive Equality).
- `Core.Accounts.BrokerAllowlist` — `IsRestricted` / `Allows(broker)`; leer = alle erlauben.
  Durchgesetzt als Invariante in `CTraderIdAccount.AddTradingAccount` /
  `LinkOpenApiAccount` (`domain.account.broker_not_allowed`).
- `Core.Accounts.IBrokerVerifier` → `Web.Accounts.BrokerVerifier` — führt den Probe-Container auf
  dem Web-Host aus (der den Docker-Socket hat), tailed Logs und parst den Broker via
  `Core.Accounts.BrokerProbeOutput`. Wird nur aufgerufen, wenn die Allowlist eingeschränkt ist.

**Broker-Probe-cBot:** ein vorgebauter `broker-probe.algo` wird mit der Web-App ausgeliefert
(`src/Web/BrokerProbe/`, kopiert als `broker-probe/broker-probe.algo` in den Output), sodass der
Standard-`App:Accounts:BrokerProbeAlgoPath` out-of-the-box aufgelöst wird — ein relativer Pfad wird
gegen das App-Basisverzeichnis aufgelöst, ein absoluter Pfad wie angegeben verwendet. Die Quelle
lebt in `tools/broker-probe/`. Wenn der Algo fehlt, schlägt die manuelle cID-Verifizierung
geschlossen fehl — Konten unter einer eingeschränkten Allowlist können immer noch über den Open-API-Pfad
verknüpft werden, der keine Probe benötigt.

## Broker-Allowlist — Tests

- **Unit** — `UnitTests/Accounts/`: `BrokerName`/`BrokerAllowlist` Value Objects, `BrokerProbeOutput`
  Parser und die `CTraderIdAccount`-Allowlist-Invariante.
- **Integration** — `IntegrationTests/BrokerAllowlistTests.cs`: manueller-cID-Endpunkt mit einem Fake-Verifier
  (uneingeschränkt / verifiziert / nicht erlaubt / Verifizierung-fehlgeschlagen) + Open-API-Linker
  überspringt nicht erlaubte Konten. `BrokerVerifierLiveTests.cs` führt die **echte** Probe aus, wenn
  cID-Credentials + der Algo bereitgestellt werden (überspringt sauber, sonst).
- **E2E** — `E2ETests/BrokerAllowlistTests.cs`: eine eingeschränkte Bereitstellung lehnt ein manuelles
  Hinzufügen durch die echte UI ab und zeigt die „couldn't verify"-Benachrichtigung (kein
  Kontoreih hinzugefügt).

## Nodes-UI-Sichtbarkeit

Nodes sind Infrastruktur, die die meisten Mieter nie von Hand verwalten — cTrader-CLI-Agenten
[registrieren sich selbst und senden Heartbeat](../operations/node-discovery.md), sodass eine
White-Label-Bereitstellung die manuellen Steuerungen oder die gesamte Nodes-Oberfläche verbergen und
trotzdem einen gesunden Cluster durch Auto-Discovery betreiben kann. Zwei Config-only Branding-Keys
steuern dies:

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

Umgebungsvariablen-Form: `App__Branding__NodesUi=Hidden`,
`App__Branding__RestrictNodesToOwner=true`.

**`NodesUi` — drei Modi:**

- **`Full` (Standard)** — das Standard-Produkt: die Node-Liste plus die manuellen **Neuer Node**- und
  **Löschen**-Steuerungen. `POST`/`DELETE /api/nodes` funktionieren.
- **`Monitor`** — eine Read-Only-Oberfläche: die Liste und Live-Stats bleiben, aber manuelles Hinzufügen
  und Löschen sind entfernt. Nodes erscheinen nur durch Auto-Discovery. `POST`/`DELETE /api/nodes`
  geben **404** zurück.
- **`Hidden`** — der Nodes-Nav-Link und die Seite sind vollständig weg und die Seitenroute leitet zum
  Dashboard weiter; die manuelle Hinzufügen/Löschen-API ist aus. Der Cluster ist Auto-Discovery nur.

**`RestrictNodesToOwner`** bestimmt, wer Nodes sehen und verwalten darf. Standard `false` behält die
normale **Admin-oder-mehr**-Staff-Oberfläche (`AdminOrAbove`); auf `true` setzen, um es
**nur-Eigentümer** (`Owner`) zu machen. In beiden Fällen sehen **normale Benutzer nie Nodes** — dies
wählt nur zwischen Nur-Eigentümer und der breiteren Staff-Oberfläche.

Node **Auto-Discovery ist von beiden Keys unberührt**: der anonyme `POST /api/nodes/register`
Self-Register + Heartbeat-Endpunkt funktioniert immer, sodass eine `Hidden`/`Monitor`-Bereitstellung
ihren Cluster trotzdem automatisch wachsen lässt.

**Modell:**

- `Core.Nodes.NodesUiMode` — `Full` / `Monitor` / `Hidden`.
- `Core.Nodes.NodesUiAccess` — die einzige Quelle der Wahrheit, die den Modus + Eigentümer-Beschränkung
  zusammensetzt: `IsPageVisible`, `AllowsManualManagement`,
  `RequiredPolicy(restrictToOwner)`. Nav (`NavMenu.razor`), die Seite (`Pages/Nodes.razor`) und die
  Endpunkte (`NodeEndpoints`) lesen alle, sodass UI und API nie disagree können.
- `Core.Options.BrandingOptions.NodesUi` / `.RestrictNodesToOwner` — gebunden von `App:Branding`.

## Nodes-UI-Sichtbarkeit — Tests

- **Unit** — `UnitTests/Nodes/NodesUiAccessTests.cs`: Seiten-Sichtbarkeit,
  manuelle-Verwaltung und erforderliche-Policy-Auflösung über jeden Modus + Default-Branding.
- **Integration** — `IntegrationTests/NodeUiGatingTests.cs`: über echtes HTTP + Postgres —
  `Full` erlaubt manuelles Hinzufügen, `Monitor`/`Hidden` 404 für Hinzufügen und Löschen, und
  `RestrictNodesToOwner` verbietet einem Admin, während der Eigentümer noch die Liste liest.
- **E2E** — `E2ETests/NodesUiTests.cs` (Standard `Full`: Nav-Link + Seite + Neuer-Node-Button
  rendern) und `E2ETests/NodesHiddenTests.cs` (`Hidden`: Nav-Link weg, `/nodes` leitet um).

## Design Tokens (CSS-Variablen)

Branding erreicht auch das **eigene** Stylesheet der App + Custom-Komponenten, nicht nur MudBlazor.
`Web.Branding.BrandingCss.BuildRootVariables(BrandingOptions)` emittiert die gebrandete Palette als CSS
Custom Properties auf `:root` (`--app-primary`, `--app-primary-hover`, `--app-surface`,
`--app-appbar`, `--app-success`/`--app-error`/`--app-warning`/`--app-info`, …), injiziert in
`App.razor` direkt nach `site.css`. `site.css` und jede Komponente lesen `var(--app-*)` —
**keine hardcodierten Farben** — sodass die Palette eines Wiederverkäufers überall (Login-Hero,
Bottom-Nav, Help-Tips, Offline-Seite) kostenlos durchfließt. Neutrale Oberflächentöne defaulten in
`site.css :root`; `CustomCss` (zuletzt injiziert) kann jedes Token überschreiben. Siehe
[ui-guidelines.md](../ui-guidelines.md) §2.

## Gebrandete PWA

Die installierbare App ist ebenfalls gebrandet — der Manifest-Endpunkt
(`/manifest.webmanifest`) wird aus `BrandingOptions` gebaut (`ProductName` → `name`/`short_name`,
`Description`, `AppBarColor`/`BackgroundColor` → theme/background). Siehe [pwa.md](pwa.md).

## Tests

- **Unit** — `UnitTests/Branding/HexColorTests.cs`: gültige/unfähige Hex-Validierung.
- **Integration** — `IntegrationTests/ThemeBuildTests.cs`: Farben mappen in Palette, ungültige Farbe
  wirft; `IntegrationTests/BrandingHttpTests.cs`: benutzerdefinierter `ProductName`/description/theme-colour
  rendern in bedientem Seiten-<head> (WebApplicationFactory + Postgres), Defaults behalten Standardnamen.
- **E2E** — `E2ETests/BrandingTests.cs`: gebrandeter Produktname rendert in App-Bar in echtem Browser.
