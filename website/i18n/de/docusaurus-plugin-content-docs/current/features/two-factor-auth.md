---
description: "Optionale TOTP-Zwei-Faktor-Authentifizierung mit Authenticator-App-Einschreibung, einmaliger Backup-Codes und ein White-Label-Switch um es für alle Benutzer obligatorisch zu machen."
---

# Zwei-Faktor-Authentifizierung (2FA)

Konten können mit **Zeit-basiertes One-Time-Password (TOTP)** Zwei-Faktor-Authentifizierung zusätzlich zum Passwort geschützt werden. Es ist **Optional** aus dem Benutzer's Profil standardmäßig, und eine White-Label-Bereitstellung kann es **obligatorisch** für alle machen. Jede RFC 6238 Authenticator-App funktioniert — Google Authenticator, Microsoft Authenticator, Authy, Aegis, FreeOTP — weil die Implementierung Standard ist (SHA-1, 6 Ziffern, 30-Sekunden-Schritt); keine proprietäre Server-Komponente ist beteiligt.

## Wie es funktioniert

- **Domäne.** MFA lebt auf dem `AppUser`-Aggregat (Access-Kontext). Ein Benutzer wird durch Intention-Reveal-Methoden eingeschrieben — `BeginMfaEnrollment`, `ConfirmMfaEnrollment`, `ConsumeBackupCode`, `RegenerateBackupCodes`, `DisableMfa` — sodass die Invarianten (ein Geheimnis muss bestätigt werden, bevor es aktiviert wird; ein Backup-Code ist einmaliger Gebrauch) an einem Ort durchgesetzt werden.
- **TOTP.** Generierung und Verifizierung sitzen hinter der Core `ITotpAuthenticator`-Schnittstelle, in der Infrastruktur mit der **Otp.NET**-Bibliothek implementiert. Verifizierung toleriert ±1 Zeit-Schritt Uhr-Skew.
- **Geheimnis in Ruhe.** Das Authenticator-Geheimnis wird **verschlüsselt** via `ISecretProtector` (`EncryptionPurposes.MfaSecret`) gespeichert — nie in Klartext.
- **Backup-Codes.** Zehn einmaliger Recovery-Codes werden bei Einschreibung ausgestellt, **einmal** angezeigt und nur als SHA-256-Hashes (`MfaBackupCodes`) gespeichert. Jeder funktioniert genau einmal; ein ausgegeben Code wird danach abgelehnt.

## Aktivieren (Profil)

Auf der **Konto**-Seite (`/account`) zeigt der Abschnitt *Zwei-Faktor-Authentifizierung* den aktuellen Status:

1. **Enable Two-Factor** öffnet einen MudBlazor-Dialog mit einem **QR-Code** (Server-seitig als SVG via `Net.Codecrete.QrCodeGenerator` gerendert) plus dem manuellen Setup-Schlüssel.
2. Scannen Sie ihn, geben Sie den 6-stelligen Code ein, um zu bestätigen — dies verifiziert das Pending-Geheimnis, bevor es aktiviert wird.
3. Der Dialog zeigt dann die **Backup-Codes**; speichern Sie sie. 2FA ist jetzt ein.

Der gleiche Abschnitt lässt einen eingeschriebenen Benutzer **Backup-Codes regenerieren** oder **2FA ausschalten** — beide benötigen das Konto-Passwort, um zu bestätigen.

## Anmelden mit 2FA

Das Login ist ein **Two-Step**-Fluss, sobald 2FA aktiviert ist:

1. **Passwort-Schritt** (`POST /api/auth/login`). Bei Erfolg wird der Auth-Cookie **nicht** noch ausgestellt; stattdessen wird ein kurzfristiger (5 Minuten), verschlüsselter *Pending*-Cookie gesetzt und der Benutzer wird an `/login/2fa` gesendet.
2. **Challenge-Schritt** (`POST /api/auth/login/verify-2fa`). Der Benutzer gibt einen TOTP-Code **oder** einen unbenutzten Backup-Code ein. Bei Erfolg wird der Pending-Cookie gelöscht und der echte Auth-Cookie wird ausgestellt.

Fehlgeschlagene Second-Factor-Versuche zählen zum existierenden Konto-**Lockout** (`AuthLockout`), und die Auth-Endpunkte werden Rate-Limited.

## Obligatorische 2FA für eine White-Label-Bereitstellung

Ein regulierter Wiederverkäufer kann 2FA für **jedes** Konto benötigen:

```jsonc
// appsettings / Umgebung
"App": { "Branding": { "RequireMfa": true } }   // App__Branding__RequireMfa=true
```

Wenn `RequireMfa` an ist und ein Benutzer ohne 2FA anmeldet, meldet der Passwort-Schritt `mfaSetupRequired` und `MfaEnforcementMiddleware` leitet ihre Seiten-Navigationen auf `/account` um, bis sie die Einschreibung beenden. Es nimmt standardmäßig `false` an, sodass eine nicht konfigurierte Bereitstellung 2FA optional hält. Siehe [White-Label](white-label.md).

## Endpunkte

| Methode & Route | Zweck |
| --- | --- |
| `POST /api/auth/login` | Passwort-Schritt; gibt `mfaRequired` (Challenge) oder meldet an |
| `POST /api/auth/login/verify-2fa` | Second-Factor-Schritt (TOTP oder Backup-Code) |
| `GET /api/auth/mfa/status` | `MfaEnabled`, Pending, verbleibender Backup-Code-Zähler |
| `POST /api/auth/mfa/setup` | Beginnen Sie die Einschreibung — geben Sie Geheimnis, `otpauth://` URI, QR SVG zurück |
| `POST /api/auth/mfa/confirm` | Bestätigen Sie einen Code, aktivieren Sie, geben Sie Backup-Codes zurück |
| `POST /api/auth/mfa/disable` | Schalten Sie aus (Passwort-bestätigt) |
| `POST /api/auth/mfa/backup-codes/regenerate` | Geben Sie einen neuen Satz (Passwort-bestätigt) aus |

## Tests

- **Unit** — `UnitTests/Access/OtpNetTotpAuthenticatorTests.cs` (RFC 6238 Vektoren), `AppUserMfaTests.cs` (Einschreibung/Übergang/Einmaliger Gebrauch Invarianten), `MfaBackupCodesTests.cs`.
- **Integration** — `IntegrationTests/MfaPersistenceTests.cs` (Einschreibung → Bestätigung → Verbrauch, Cascade-Löschung) und `MfaFlowTests.cs` (vollständiger HTTP Two-Step-Login mit TOTP + Backup-Code und das obligatorische Einschreibungs-Tor).
- **E2E** — `E2ETests/MfaFlowTests.cs`: Aktivieren Sie aus dem Profil (QR + Bestätigung + Backup-Codes) und schließen Sie eine herausgefordert Anmeldung ab, auf Desktop- und Mobil-Ansichtsfenstern.
