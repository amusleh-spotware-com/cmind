---
description: "Versenden Sie eine cTrader-Open-API-Anwendung für jeden Benutzer (White-Label-Shared-Modus), die einzelne Umleitungs-URL zum Registrieren und Pro-Nachrichtentyp-Client-Rate-Limits."
---

# Gemeinsame Open-API-Anwendung & Rate-Limits

Standardmäßig registriert jeder Benutzer seine **eigene** cTrader-Open-API-Anwendung unter **Einstellungen → Open API**. Ein White-Label-Betreiber (typischerweise ein cTrader-Broker oder Wiederverkäufer) kann stattdessen **eine gemeinsame Open-API-Anwendung für alle Benutzer** versenden — niemand registriert sein eigenes; jeder autorisiert seine Konten durch die einzelne App des Betreibers.

## Zwei Möglichkeiten, die gemeinsame Anwendung bereitzustellen

Die gemeinsame Anwendung wird entweder aus Bereitstellungs-Config **oder** aus dem Owner-Einstellungs-UI bereitgestellt (der Owner-Set-Wert gewinnt). Stellen Sie es einmal bereit und der Shared-Modus schaltet sich für jeden ein.

### 1. Bereitstellungs-Config (beim Start gesät)

```jsonc
"App": {
  "OpenApi": {
    "PublicBaseUrl": "https://cmind.yourbroker.com",   // kanonische öffentliche URL dieser Bereitstellung
    "SharedApp": {
      "Enabled": true,
      "Name": "YourBroker Open API",
      "ClientId": "1234_abcd...",
      "ClientSecret": "…"                                // verschlüsselt in Ruhe; nie geloggt
    }
  }
}
```

Beim Start besät die App eine gemeinsame Anwendung, die vom Owner-Konto besessen wird (idempotent — sie überschreibt nie einen Owner-bearbeiteten Runtime-Wert, und Neuseeding ist ein No-Op).

### 2. Owner-Einstellungen (Runtime, kein Redeploy)

**Einstellungen → Open API** (nur Owner) zeigt eine **Deployment Shared Application**-Karte: Fügen Sie hinzu / bearbeiten Sie / löschen Sie die gemeinsame App, mit der Umleitungs-URL zur Anzeige für Copy-Paste. Änderungen treten für neue Autorisierungen sofort in Kraft.

## Die Umleitungs-URL (registrieren Sie dies in cTrader)

Jede cTrader-Open-API-Anwendung registriert **eine** Umleitungs-URL — der **gleiche einzelne Wert** für die gemeinsame App und für beliebige Pro-Benutzer-Apps:

```
{your deployment URL}/openapi/callback
```

zum Beispiel `https://cmind.yourbroker.com/openapi/callback`.

- Die App **zeigt den genauen Wert** auf der Open-API-Einstellungs-Seite (mit einem Copy-Button) — fügen Sie ihn in das cTrader-Partner-Portal ein, wenn Sie die Open-API-Anwendung erstellen.
- Es wird aus `App:OpenApi:PublicBaseUrl` zusammengestellt, sodass es hinter einem Reverse Proxy / CDN stabil bleibt; wenn das nicht gesetzt ist, fällt es auf den inbound Request-Host zurück.
- Das Einladung vs normales Benutzer-Erlebnis unterscheidet sich nur darin, wo der Benutzer **nach** dem Callback landet (seine Konten-Liste vs eine "Konten hinzugefügt"-Bestätigung) — die registrierte Umleitungs-URL ist unverändert.

## Was Benutzer im Shared-Modus sehen

Wenn eine gemeinsame Anwendung vorhanden ist:

- Benutzer bekommen **keine Möglichkeit**, ihre eigene Open-API-Anwendung zu registrieren — die Einstellungs-Seite zeigt **"Open API wird von Ihrem Provider verwaltet"** und einen **Konten autorisieren**-Button, der die gemeinsame App verwendet.
- Alle bereits existierenden persönlichen Anwendungen werden **entfernt**; ihre autorisierten Konten werden auf die gemeinsame App zurück zeigen und müssen **erneut autorisiert** werden (ihre alten Tokens wurden unter einer anderen Client-ID ausgestellt). Der Versuch, eine persönliche App zu erstellen, gibt einen "verwaltet von Ihrem Provider"-Fehler zurück.

## Client-Rate-Limits (pro Nachrichtentyp)

Der Client paced die Outbound-cTrader-Open-API-Nachrichten, sodass ein Burst nie einen Server-seitigen Rate-Limit-Block auslöst. Limits sind **pro Nachrichtentyp**, passend zur cTrader-Open-API-Dokumentation:

| Kategorie | Was es abdeckt | Standard |
|---|---|---|
| `General` | Trading + Read-Nachrichten (Orders, Symbole, Konto-Abfragen) | 45 Msg/s |
| `HistoricalData` | Trendbar / Tick-Daten-Anfragen (von cTrader gehärtet) | 5 Msg/s |

Eine Historical-Data-Anfrage zählt gegen **sowohl** ihren eigenen Bucket als auch den General-Bucket. Heartbeat- und Authentifizierungs-Nachrichten werden nie gepaced. Nachrichten warten in der Warteschlange an und ziehen mit der verfügbaren Rate ab — nichts wird gelöscht und die Reihenfolge bleibt erhalten.

Stimmen Sie sie ab, wenn Ihr Broker höhere **höhere** cTrader-Limits verhandelt hat, oder setzen Sie eine Kategorie auf **`0`**, um die Pacing ganz zu deaktivieren (unbegrenzt):

- **Config:** `App:OpenApi:RateLimits:General` / `App:OpenApi:RateLimits:HistoricalData` (Msgs/Sek).
- **Owner-Einstellungen:** die **Client-Rate-Limits**-Karte auf **Einstellungen → Open API** (Owner-Override gewinnt, wird auf neue Verbindungen / beim Reconnect angewendet).
