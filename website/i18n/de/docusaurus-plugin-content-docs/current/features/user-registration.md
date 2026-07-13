---
description: "Sichere, White-Label-Gated-Self-Service-Benutzer-Registrierung — eine On-App-Sign-Up-Seite und eine Server-zu-Server-Provisionierungs-API, mit konfigurierbaren Benutzer-Attributen, Admin-Genehmigung oder Email-Verifizierung-Gating und Anti-Missbrauch-Schutzmaßnahmen. Standardmäßig deaktiviert."
---

# Benutzer-Registrierung

Standardmäßig **fügt der Owner/Admin Benutzer manuell hinzu** (Benutzer-Seite → *Neuer Benutzer*). Für White-Label-Bereitstellungen, die Benutzer in großer Zahl onboarden müssen — oder die App mit einem anderen Service integrieren — versendet cMind auch einen **sicheren, Self-Service-Registrierungs**-Pfad. Es ist **standardmäßig deaktiviert**: eine Stock-Bereitstellung ist unverändert und die Seite und die API geben beide 404 zurück, bis eine Bereitstellung sich anmeldet.

Es gibt zwei Einstiegspunkte, die einen Domänen-Fluss teilen:

1. **On-App-Seite** (`/register`) — eine gebrandete, mobil-erste Sign-Up-Seite in der gleichen Shell wie `/login`.
2. **Provisionierungs-API** (`POST /api/provision`) — ein Server-zu-Server-Endpunkt für einen integrierenden Service, um Konten zu erstellen, authentifiziert durch ein Pro-Bereitstellungs-Provisionierungs-Geheimnis.

## Was wird aufgezeichnet — Datenminimierung

cMind ist Trading **Werkzeugen**: es baut/läuft/backtests cBots und spiegelt Trades über die *eigenen* cTrader-Open-API-Anmeldedaten jedes Benutzers. Es **eröffnet keine Trading-Konten oder Haft-Client-Geld**, sodass KYC/AML-Identitäts-Verifizierung die Verpflichtung des **Brokers** ist, nicht dieser Plattform. Das Registrierungs-Formular zeichnet daher **nur eine Email standardmäßig** — das Minimum, das nötig ist, um den Service bereitzustellen (GDPR Art. 5(1)(c) Datenminimierung; rechtliche Basis = Vertrag). cMind versendet bewusst **keine** nationalen-ID / Geburtsdatum / Adress-Felder.

Jedes andere Attribut ist **Optional pro Bereitstellung** via `App:Registration:Attributes`, jedes unabhängig `Off` / `Optional` / `Required`:

| Attribut | Notizen |
|---|---|
| `FullName`, `DisplayName`, `Company` | Free-Text, Länge-begrenzt. |
| `Country` | ISO 3166-1 Alpha-2, validiert gegen einen festen Code-Satz. |
| `Phone` | E.164-Format (`+14155552671`). |
| `Locale` | BCP-47-Form (`en-US`), normalisiert. |
| `MarketingOptIn` | Separate, **Unchecked**-Checkbox — nie mit der obligatorischen Zustimmung gebündelt (CAN-SPAM). |
| `AgeConfirmation` | Eine Checkbox nur; **keine** Geburtsdatum wird gespeichert. |

Attribute leben in dem `UserProfile`-Wert-Objekt, das vom `AppUser`-Aggregat besessen wird, validiert bei der Konstruktion. **GDPR-Löschung** (`AppUser.Anonymize()`) schrubbt das Profil und alle Verifizierungs-Tokens.

**Zustimmung.** Wenn `RequireTermsAcceptance` an ist, muss der Benutzer die veröffentlichten rechtlichen Dokumente akzeptieren (Bedingungen, Datenschutz, Risikooffenbarung). Die Akzeptanz wird durch das existierende `ConsentRecord`-Aggregat aufgezeichnet — Versions-gestempelt, Zeit-gestempelt, mit ursprünglicher IP — der gleiche Store, der an anderen Orten für MiFID/ESMA-Klasse Aufzeichnung verwendet wird.

## Gating-Modi

Ein Self-Registered-Konto kann sich nicht anmelden, bis es sein Tor passiert (`App:Registration:Mode`):

- **`AdminApproval`** (Standard) — das Konto wird in die Warteschlange gestellt; ein Owner/Admin genehmigt es auf der **Benutzer**-Seite (*Genehmigung ausstehend*-Abschnitt). Benötigt keine Mail-Infrastruktur.
- **`EmailVerification`** — ein einmaliger, ablaufen-Verifikations-Link wird ge-emailed; das Konto aktiviert, wenn der Link geöffnet wird. Benötigt einen Email-Transport (`App:Email`). **Wenn kein Transport konfiguriert ist, wird dieser Modus automatisch beim Startup auf `AdminApproval` herabgestuft**, sodass das Aktivieren der Registrierung nie stillschweigend bricht.
- **`Open`** — das Konto ist sofort aktiv (vertraut/Dev nur).

Self-Registered-Benutzer werden immer als **`User`** erstellt (oder `Viewer`, wenn konfiguriert) — die Domäne **weigert sich hart**, einen Owner/Admin durch Self-Registration zu prägen.

## Sicherheit & Anti-Missbrauch

- **Anti-Enumeration.** Eine Duplicate-Email ergibt die **gleiche** neutrale `202 Accepted` wie ein frischer Sign-Up und erstellt nichts — die App offenbart nie, ob eine Adresse bereits ein Konto hat.
- **Rate Limiting.** Die öffentlichen Endpunkte werden per IP gedrosselt (schwächer als der Auth-Limiter).
- **Passwort-Policy.** Mindestlänge durchgesetzt; Passwörter werden gehashed (Argon2 via `IPasswordHasher`); Verifizierungs-Tokens werden nur als SHA-256-Hashes gespeichert und sind einmaliger Gebrauch + ablaufen.
- **Email-Hygiene.** Optionale Zulassungs-Liste von Email-Domänen und eine Disposable-Provider-Block-Liste.
- **CAPTCHA (optional).** reCAPTCHA / hCaptcha / Turnstile via ihre gemeinsame Verify-Vertrag.
- **Login-Tor.** Ein Pending-Konto wird bei Login mit einer neutralen Antwort geweigert.

## Provisionierungs-API (Integration)

Mit `App:Registration:Api:Enabled` und einem `Secret`-Satz kann ein anderer Service Benutzer erstellen:

```
POST /api/provision
X-Provision-Secret: <the configured secret>
{ "email": "user@example.com", "password": "…", "role": 2 }
```

Das Geheimnis wird in konstanter Zeit verglichen. Bereitgestellte Konten werden **aktiv** erstellt (oder mit `MustChangePassword` eingeladen), je nach `Api.ActivateImmediately` / `Api.InviteMustChangePassword`.

## Aktivieren

Registrierung benötigt **sowohl** das Feature-Flag als auch den Master-Schalter:

```jsonc
"App": {
  "Features": { "Registration": true },
  "Registration": {
    "Enabled": true,
    "Mode": "AdminApproval",           // oder EmailVerification / Open
    "DefaultRole": "User",             // nie Owner/Admin
    "RequireTermsAcceptance": true,
    "AllowedEmailDomains": [],          // leer = any
    "BlockDisposableEmail": true,
    "Attributes": { "FullName": "Optional", "Country": "Off" },
    "Api": { "Enabled": false, "Secret": "" }
  }
}
```

Der `App:Email`-Abschnitt (SMTP `Host`, `Port`, `UseStartTls`, `Username`, `Password`, `FromAddress`, `FromName`) konfiguriert den Transport, der vom `EmailVerification`-Modus verwendet wird; lassen Sie `Host` ungesetzt, um ohne Mail zu laufen (der No-Op-Sender). Siehe [Feature-Toggles](./feature-toggles.md) und [White-Label](./white-label.md) wie Bereitstellungen Features einschalten und rebrand. Wenn Registrierung aktiviert ist, zeigt die Login-Seite einen **Konto erstellen**-Link.

## Getestet

Unit (Profil-Validierung, `SelfRegister`-Rolle-Guard, Aktivierungs-Übergänge, einmaliger Gebrauch-Tokens, Löschung), Integration (Disabled-by-Default 404, Genehmigungsfluss, Email-Verifizierung-Downgrade, Anti-Enumeration, Missbrauch-Wachen, erforderliche Attribute, Provisionierung + schlechtes Geheimnis) und E2E (Standard-Off-Login hat keinen Sign-Up-Link; die `/register`-Seite rendert ihren Brand-geschlossenen Zustand).
