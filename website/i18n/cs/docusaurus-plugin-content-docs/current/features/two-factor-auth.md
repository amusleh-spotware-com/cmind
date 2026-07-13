---
description: "Volitelné TOTP two-factor ověřování s authenticator-app enrollmentem, single-use backup kódy, a white-label switch aby se to stalo povinným pro všechny uživatele."
---

# Two-factor ověřování (2FA)

Účty mohou být chráněny s **time-based one-time password (TOTP)** two-factor ověřováním na top hesla. Je to **opt-in** z uživatelova profilu výchozím, a white-label nasazení to může učinit **povinným** pro všechny. Jakýkoliv RFC 6238 authenticator app pracuje — Google Authenticator, Microsoft Authenticator, Authy, Aegis, FreeOTP — protože implementace je standardní (SHA-1, 6 číslic, 30-sekundový step); žádný proprietární server komponenta není zahrnut.

## Jak to pracuje

- **Doména.** MFA žije na `AppUser` agregátu (Access context). Uživatel je zapsán skrz intention-revealing metody — `BeginMfaEnrollment`, `ConfirmMfaEnrollment`, `ConsumeBackupCode`, `RegenerateBackupCodes`, `DisableMfa` — takže invarianty (tajemství musí být potvrzeno předtím než se aktivuje; backup kód je single-use) jsou vynuceny na jednom místě.
- **TOTP.** Generování a ověřování sedí za Core `ITotpAuthenticator` rozhraním, implementováno v Infrastructure s **Otp.NET** knihovnou. Ověřování toleruje ±1 time-step clock skew.
- **Tajemství v klidu.** Authenticator tajemství je uloženo **encrypted** přes `ISecretProtector` (`EncryptionPurposes.MfaSecret`) — nikdy v plaintext.
- **Backup kódy.** Deset single-use recovery kódů je vydáno při zápisu, zobrazeno **jednou**, a uloženo pouze jako SHA-256 hashe (`MfaBackupCodes`). Každý pracuje přesně jednou; utracený kód je odmítnut poté.

## Povolení (profil)

Na **Account** stránce (`/account`) *Two-factor ověřování* sekce ukazuje aktuální status:

1. **Enable two-factor** otevře MudBlazor dialog s **QR code** (renderován server-side jako SVG přes `Net.Codecrete.QrCodeGenerator`) plus manual setup klíč.
2. Scannujte jej, vložte 6-číslicový kód k potvrzení — toto ověřuje pending tajemství předtím než se aktivuje.
3. Dialog pak ukazuje **backup kódy**; uložte je. 2FA je nyní on.

Stejná sekce umožňuje zapsaným uživatelům **regenerate backup kódy** nebo **turn off** 2FA — obojí vyžaduje account heslo k potvrzení.

## Přihlášení s 2FA

Přihlášení je **dvou-step** tok jakmile je 2FA povoleno:

1. **Password step** (`POST /api/auth/login`). Na úspěch auth cookie je **ne** vydáno ještě; místo toho short-lived (5-minut), encrypted *pending* cookie je nastaven a uživatel je poslán do `/login/2fa`.
2. **Challenge step** (`POST /api/auth/login/verify-2fa`). Uživatel zadá TOTP kód **nebo** jakýkoliv nepoužitý backup kód. Na úspěch pending cookie je vymazáno a reálný auth cookie je vydán.

Selhavší second-factor pokusy počítají do existujícího účtu **lockout** (`AuthLockout`), a auth endpointy jsou rate-limited.

## Povinné 2FA pro white-label nasazení

Regulovaný prodejce může vyžadovat 2FA pro **každý** účet:

```jsonc
// appsettings / environment
"App": { "Branding": { "RequireMfa": true } }   // App__Branding__RequireMfa=true
```

Když `RequireMfa` je on a uživatel bez 2FA se přihlásí, password step hlásí `mfaSetupRequired` a `MfaEnforcementMiddleware` přesměruje jejich page navigace na `/account` dokud nezavršit zápis. Výchozí je `false`, takže unconfigured nasazení udržuje 2FA volitelné. Viz [White-label](white-label.md).

## Endpointy

| Metoda & trasa | Účel |
| --- | --- |
| `POST /api/auth/login` | Password step; vrátí `mfaRequired` (challenge) nebo podepíše |
| `POST /api/auth/login/verify-2fa` | Second-factor step (TOTP nebo backup kód) |
| `GET /api/auth/mfa/status` | `MfaEnabled`, pending, zbývající backup-code počet |
| `POST /api/auth/mfa/setup` | Begin zápis — vrátí tajemství, `otpauth://` URI, QR SVG |
| `POST /api/auth/mfa/confirm` | Potvrdit kód, aktivovat, vrátit backup kódy |
| `POST /api/auth/mfa/disable` | Vypnout (password-confirmed) |
| `POST /api/auth/mfa/backup-codes/regenerate` | Vydejte čerstvou sadu (password-confirmed) |

## Testy

- **Unit** — `UnitTests/Access/OtpNetTotpAuthenticatorTests.cs` (RFC 6238 vektory), `AppUserMfaTests.cs` (zápis/transition/single-use invarianty), `MfaBackupCodesTests.cs`.
- **Integration** — `IntegrationTests/MfaPersistenceTests.cs` (zápis → potvrdit → spotřebit, cascade delete) a `MfaFlowTests.cs` (plný HTTP dvou-step login s TOTP + backup kód, a mandatory-enrollment gate).
- **E2E** — `E2ETests/MfaFlowTests.cs`: umožnit z profilu (QR + potvrdit + backup kódy) a dokončit challenged sign-in, na desktop a mobile viewports.
