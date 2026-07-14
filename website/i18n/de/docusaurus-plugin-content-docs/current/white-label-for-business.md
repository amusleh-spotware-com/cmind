---
slug: /white-label-for-business
title: White-Label für Business
description: Versende cMind als dein eigenes gebrandetes Produkt – für Prop Firms, Broker und Copy-Trading-Unternehmen. Rebrand jede Oberfläche über Config, keine Code-Änderungen.
sidebar_position: 4
---

# White-Label cMind für dein Business 🏢

Betreibst du eine Prop Firm, einen Broker Desk oder einen Copy-Trading-Service? cMind wurde von Tag eins gebaut, um **als dein eigenes Produkt weiterverkauft zu werden**. Jede Oberfläche – der Name, das Logo, das Favicon, die Farben, sogar die installierbare Phone-App – beugt sich deiner Marke. Deine Kunden sehen *dein* Unternehmen. Keine Code-Änderungen, kein Fork, nur Config.

:::tip[TL;DR]
Zeige `App:Branding` auf deinen Namen, Farben und Logo. Neustart. Fertig. Vollständige technische Referenz lebt in der [White-Label-Feature-Doc](./features/white-label.md).
:::

## Was du rebrandesn kannst

| Oberfläche | Was ändert sich |
|---|---|
| **Produktname** | App-Bar-Text + Browser-Tab-Titel |
| **Logo & Favicon** | Deine Marken überall, einschließlich des Browser-Tabs |
| **Farben** | Volle Palette – Primary, Surfaces, Status-Farben – fließen durch die ganze UI *und* das App-eigene CSS über Design-Tokens |
| **Installierbare App (PWA)** | Der Add-to-Home-Screen-Name, Icon und Splash verwenden deine Marke |
| **Meta / SEO** | Beschreibung und Support-URL sind deine |
| **Custom CSS** | Injiziere dein eigenes Polish für die letzten 5% |

Alles standardisiert auf die Stock cMind-Identität, daher überschreibst du nur, was dir wichtig ist.

## Das 60-Sekunden-Rebrand

Setze diese auf deine Bereitstellung (JSON-Config oder Umgebungsvariablen):

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "LogoUrl": "/branding/acme-logo.svg",
      "FaviconUrl": "/branding/acme.ico",
      "PrimaryColor": "#2D7FF9",
      "SecondaryColor": "#1E63C8",
      "ShowSiteLink": false
    }
  }
}
```

Umgebungsvariablen-Form: `App__Branding__ProductName=AcmeFX`. Farben werden beim Start validiert – ein schlechter Hex-Wert schlägt den Boot mit einer klaren Nachricht fehl, statt eine gebrochene Seite zu rendern. Schön und laut, genau wenn du es möchtest.

## Der "Powered by cMind" Link

**Standardmäßig** zeigt das Dashboard einen kleinen, geschmackvollen **"Powered by cMind"**-Link, der Besucher auf diese Website zurückweist. Es ist standardmäßig an, weil wir stolz auf das Projekt sind und es anderen Tradern hilft, es zu finden – aber es ist **deine Entscheidung**.

- **Behalte es** (Standard): einen subtilen Credit-Link auf dem Dashboard. Kostet dich nichts, hilft dem Projekt.
- **Verstecke es**: setze `App__Branding__ShowSiteLink=false` und es verschwindet komplett – perfekt für eine vollständig weiß-gelabelt Bereitstellung, wo das Produkt unmissverständlich *dein* ist.

Siehe die [White-Label-Feature-Doc](./features/white-label.md#powered-by-link) für genau wo es rendert.

## Multi-Tenant, Pro-Kunden-Branding

Weil Branding nur Bereitstellungs-Config ist, kann jede Tenant-Bereitstellung ihre eigene Identität tragen. Führe eine separate Instanz pro Kunde aus, oder fahre Branding von deiner eigenen Control Plane – die App liest es aus `IOptionsMonitor`, daher kann es sogar das Theme live neubauen, wenn Optionen sich ändern.

Koppelt mit:

- **[Feature-Toggles](./features/feature-toggles.md)** – entscheide, welche Fähigkeiten jeder Tenant sieht.
- **[Prop-Firm-Regeln](./features/prop-firm.md)** – setze deine Challenge-Regeln mit Live-Eigenkapital-Tracking durch.
- **[Performance Fees](./features/copy-performance-fees.md)** + **[Provider Marketplace](./features/copy-provider-marketplace.md)** – monetarisiere Copy Trading.
- **[Compliance](./features/compliance.md)** – behalte die Audit-Spur, die dein Regulator fragen wird.

## Assets & Hosting

Werfe dein Logo/Favicon in den Web-App-Ordner `wwwroot/branding/` (oder zeige `LogoUrl`/`FaviconUrl` auf jede absolute URL). Bereitstellung wie es passt – [Docker](./deployment/local.md), [Kubernetes](./deployment/kubernetes.md), [Azure](./deployment/cloud-azure.md) oder [AWS](./deployment/cloud-aws.md).

Bereit, es dein zu machen? Beginne mit der [technischen White-Label-Referenz →](./features/white-label.md)
