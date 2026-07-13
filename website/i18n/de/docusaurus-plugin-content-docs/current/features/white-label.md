---
description: "Reseller-Rebrand-App – Produktname, Logo, Favicon, Farben, Custom CSS – über Bereitstellungs-Config, keine Code-Änderung. Jeder Branding-Wert defaultt auf Stock-Identität..."
---

# White-Label-Branding

Reseller-Rebrand-App – Produktname, Logo, Favicon, Farben, Custom CSS – über Bereitstellungs-Config, keine Code-Änderung. Jeder Branding-Wert **defaultt auf Stock-Identität**: nicht konfigurierte Bereitstellung sieht gleich aus wie vorher; Reseller überschreibt nur, was nötig ist.

## Model

- `Core.Options.BrandingOptions` – gebunden von `App:Branding`. String-basiert (Config-Kante); jede Farbe validiert, wenn Theme gebaut.
- `Core.Branding.HexColor` – Value Object für CSS hex-Farbe (`#RGB` / `#RRGGBB`), unveränderbar, selbst-validierend. Ungültige Farbe wirft `DomainException` (`domain.branding.color_invalid`), wenn Theme gebaut – misconfigurierte Bereitstellung fail schnell beim Start, nicht render kaputte Palette.
- `Web.Components.Theme.Build(BrandingOptions)` – produzieren MudBlazor-Theme von Branding. Nur gebrandete Palette-Einträge kommen von Config; Typografie, Layout, neutrale Surface-Töne bleiben fest, daher Produkt behält kohärentes Aussehen über Reseller.
- `Web.Branding.IBrandingThemeProvider` – Singleton, baue Theme einmal, neubaue auf Optionen-Änderung. Injiziert durch `MainLayout`/`EmptyLayout` für `MudThemeProvider`, durch App-Bar für Produktname/Logo. `App.razor` liest `IOptionsMonitor<AppOptions>` direkt für Seiten `<head>` (Titel, Beschreibung, Favicon, Theme-Farbe, Custom CSS).

## Konfiguration

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "Description": "AcmeFX – Copy Trading und Strategy Automation.",
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

Umgebungsvariablen-Form: `App__Branding__ProductName=AcmeFX`, `App__Branding__PrimaryColor=%232D7FF9`.

| Schlüssel | Effekt | Standard |
|-----|--------|---------|
| `ProductName` | App-Bar-Text + Seiten `<title>` | `cMind` |
| `LogoUrl` | App-Bar-Logo-Bild; wenn leer, zeige Produktname-Text | *(Leer)* |
| `FaviconUrl` | `<link rel="icon">` | `favicon.svg` |
| `Description` | `<meta name="description">` | Stock-Beschreibung |
| `PrimaryColor` / `SecondaryColor` | Akzent, Schublade Icon, Buttons | `#26C281` / `#1FB97A` |
| `AppBarColor` / `BackgroundColor` / `SurfaceColor` | Chrome + Surfaces; `AppBarColor` fahren `<meta theme-color>` + PWA Manifest `theme_color`, `BackgroundColor` das Manifest `background_color` | Dark-Palette |
| `SuccessColor` / `ErrorColor` / `WarningColor` / `InfoColor` | Status-Farben | Stock |
| `CustomCss` | Injiziert `<style>` in `<head>` (Bereitstellung-vertraut) | *(Leer)* |
| `ShowSiteLink` | zeige den "Powered by cMind" Credit-Link auf dem Dashboard | `true` |
| `RequireMfa` | verlange von jedem Benutzer, Two-Factor-Authentication vor App-Nutzung zu setzen | `false` |
| `NodesUi` | wie viel der Nodes-Oberfläche versendet: `Full` (List + Manual Add/Delete), `Monitor` (Read-Only List, kein Add/Delete), `Hidden` (kein Nav, kein Page, keine Manual API) | `Full` |
| `RestrictNodesToOwner` | wenn `true`, nur der Owner kann Nodes sehen/verwalten; sonst die ganze Admin-or-Above Staff-Oberfläche kann. Normal Users sehen Nodes nie je Fall | `false` |

Assets, die von `LogoUrl`/`FaviconUrl` referenziert werden, werden von Web-App `wwwroot` serviert (z.B. mounten `wwwroot/branding/`-Ordner) oder jede absolute URL.

`App:Branding` validiert beim Start (`BrandingOptionsValidator`, laufen über `ValidateOnStart`): jede Farbe muss gültiges Hex sein, `CustomCss` darf nicht `<`/`>` enthalten (können nicht aus `<style>`-Tag brechen). Misconfigurierte Bereitstellung fail zu Boot mit klarer Nachricht, nicht render kaputte Seite.

## Powered-By-Link

Das Dashboard rendert einen kleinen **"Powered by cMind"**-Credit-Link, der auf die Dokumentations-Website des Projekts zeigt. Er wird kontrolliert durch `App:Branding:ShowSiteLink` und ist **`true` per Standard** – eine nicht konfigurierte Bereitstellung zeigt ihn. Ein Reseller, der ein vollständig White-Label-Instanz betreibt, setzt `App__Branding__ShowSiteLink=false`, um ihn komplett zu entfernen.

Der Link wird durch die Dashboard-Komponente emittiert und liest das Flag über `IBrandingThemeProvider` / `BrandingOptions`, daher ist das Umschalten eine Config-Only-Änderung (kein Neubauen). Siehe [White-Label für Business](../white-label-for-business.md#der-powered-by-cmind-link) für die Business-Facing-Zusammenfassung.

## Broker-Allowlist

Eine White-Label-Bereitstellung kann beschränken, welche Brokers' Handelskonten ihre Benutzer hinzufügen können – daher serviert ein Broker, der cMind für seine eigenen Kunden läuft, nur sein eigenes Buch. Konfiguriert unter `App:Accounts`:

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

- **Leere List (Standard) ⇒ Unbeschränkt.** Jeder Broker ist erlaubt und **keine Verifizierung läuft** – eine Stock-Bereitstellung ist komplett unverändert.
- **Nicht-Leer ⇒ Beschränkt.** cMind überprüft jedes Konto, das ein Benutzer hinzufügen versucht, gegen die List (Case-Insensitiv):
  - **Open API (OAuth) Link** – der Broker-Name wird autoritativ durch cTrader Open API berichtet, daher wird ein nicht erlaubtes Konto einfach **übersprungen** (erlaubte Konten im gleichen Grant verlinken immer noch); die Autorisierungs-Seite sagt dem Benutzer, welche Broker übersprungen wurden.
  - **Manual cID (Benutzername / Passwort)** – der Benutzer-getippte Broker wird **nicht** vertraut. cMind **verifiziert** das echte Broker-Konto durch Ausführung des versendet Broker-Probe-cBots durch cTrader CLI (Lesen von `Account.BrokerName`) und persistiert diesen verifizierten Namen. Ein nicht erlaubter Broker wird mit einer Benachrichtigung abgelehnt; ein Verifizierungs-Fehler (schlechte Anmeldedaten, kein Node, Timeout) wird auch surfaced und das Konto wird nicht hinzugefügt.

**Model:**

- `Core.Options.AccountsOptions` – gebunden von `App:Accounts` (`AllowedBrokers`, `BrokerProbeTimeout`, `BrokerProbeAlgoPath`).
- `Core.Accounts.BrokerName` – Value Object (getrimmt, Case-Insensitive-Gleichheit).
- `Core.Accounts.BrokerAllowlist` – `IsRestricted` / `Allows(broker)`; Leer = Alles erlauben. Durchgesetzt als Invariant innerhalb `CTraderIdAccount.AddTradingAccount` / `LinkOpenApiAccount` (`domain.account.broker_not_allowed`).
- `Core.Accounts.IBrokerVerifier` → `Web.Accounts.BrokerVerifier` – führt den Probe-Container auf dem Web-Host aus (der den Docker-Socket hat), tails Logs und parst den Broker über `Core.Accounts.BrokerProbeOutput`. Nur aufgerufen, wenn die Allowlist beschränkt ist.
