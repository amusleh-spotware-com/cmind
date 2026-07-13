---
id: white-label-owner-settings
title: White-Label-Optionen in den Owner-Einstellungen
sidebar_label: White-Label-Owner-Einstellungen
---

# White-Label-Optionen in den Owner-Einstellungen

Jede White-Label-Option, die eine Bereitstellung durch Konfiguration setzen kann (`appsettings`/env), ist
**auch zur Laufzeit durch den App-Owner einstellbar**, über **Settings → Deployment**, ohne Redeploy.
Ein Owner-Override **gewinnt über der Konfiguration**; Löschen davon stellt die bereitgestellte
(oder eingebaute Standard-) Option wieder her.

Dies bildet nach, wie eine White-Label-*Bereitstellung* das Produkt konfiguriert — dieselben Knöpfe,
derselbe Effekt — sodass ein Operator Branding, Gates und Policy live abstimmen und das Ergebnis sofort
sehen kann.

## Wo es lebt

- **UI:** der Owner-only **Deployment**-Abschnitt im Einstellungen-Dialog und die Deep-Link-Seite
  **`/settings/deployment`**. Optionen sind in **einen Tab pro Kategorie** gruppiert (Branding, Theme,
  Features, Registration, Accounts, Email, AI, Open API, Prop Firm), Mobile-First, mit einem
  Fenster-Dialog auf dem Desktop und einer Vollbild-Oberfläche auf Handys.
- **API:** `/api/whitelabel` (Owner-only, nie Feature-gated):
  - `GET /api/whitelabel` — jede Option mit ihrem effektiven Wert, Provenienz (`Config` / `Owner` /
    `Default`) und ob ein Override gesetzt ist. **Secrets werden maskiert** (Wert wird nie
    zurückgegeben).
  - `PUT /api/whitelabel/{key}` `{ "value": "…" }` — einen Override setzen (pro Optionstyp validiert).
    Ein leerer Wert auf einem **Secret** behält das bestehende Secret.
  - `DELETE /api/whitelabel/{key}` — einen Override löschen (zurück zur Config).
  - `POST /api/whitelabel/reset` — alle Overrides löschen (Bereitstellung auf reine Config zurücksetzen).

## Wie Overrides wirksam werden

Owner-Overrides werden als verschlüsselte (wo nötig) `AppSetting`-Zeilen gespeichert und über das
gebundene `AppOptions` durch einen dekorierten `IOptionsMonitor<AppOptions>` geschichtet. Da jeder
Consumer bereits Options durch diesen Monitor liest, gilt ein Override **live über die gesamte App** —
das Theme, der Seitentitel, das MFA-Gate, die KI-Provider-Gates, die Broker-Allowlist,
Registrierungsrichtlinie, E-Mail-Transporteinstellungen usw. werden beim nächsten Lesen aktualisiert
(das Theme/Branding rendert sofort). Wenn die Datenbank kurzzeitig nicht verfügbar ist, öffnet die
Schicht **fail-open** zur konfigurierten Basislinie, sodass ein Override-Lesen die App nie brechen kann.

**Feature-Flags** sind Teil derselben Oberfläche, werden aber durch den bestehenden Feature-Override-Store
(`IFeatureGate`) persistiert, sodass der Features-Tab und die eigenständigen Feature-Toggles nie
divergieren.

**Secrets** (SMTP-Passwort, CAPTCHA-Secret, Provisioning-Secret) werden im Ruhezustand verschlüsselt
(`ISecretProtector`, Zweck `whitelabel.secret`), sind Write-Only in der UI und werden nie von der API
zurückgegeben.

## Delegierte Optionen

Die **gemeinsam genutzte Open-API-Anwendungs**-Anmeldedaten und **pro-Nachrichtentyp-Rate-Limits**
werden im **Open API**-Einstellungsabschnitt verwaltet (siehe die Copy-Trading / Open-API-Docs). Sie
erscheinen im Deployment-Katalog als *delegierte* Einträge (Read-Only hier, mit einem Link), sodass
nichts dupliziert wird und die Sync-Garantie sie trotzdem als abgedeckt zählt.

## Immer synchron (erzwungen)

Das Hinzufügen einer neuen White-Label-Option zur Konfiguration **muss** sie in derselben Änderung in den
Owner-Einstellungen verfügbar machen. Dies wird durch `WhiteLabelCatalogParityTests` erzwungen: es
reflektiert über jede White-Label-Options-Record-Eigenschaft und lässt den Build fehlschlagen, es sei
denn, die Eigenschaft ist in `Core/WhiteLabel/WhiteLabelCatalog` registriert (oder explizit in
`IntentionallyExcluded` mit einem Grund aufgelistet). Siehe Mandat 10 in `CLAUDE.md`.

## Anmerkungen

- Das Aktivieren von SMTP auf einer Bereitstellung, die ohne konfiguriertes E-Mail **gestartet** ist,
  benötigt einen Neustart (der Sendertyp wird beim Startup gewählt); Host/Credentials eines bereits
  konfigurierten Senders werden live aktualisiert.
- Options-**Labels/Beschreibungen** sind technische Config-Knopf-Identifikatoren, die als Daten
  angezeigt werden; die Tab-Labels und die gesamte interaktive Chrome sind vollständig lokalisiert.
