---
description: "Voliteľné TOTP two-factor authentication s authenticator-app enrollment, single-use backup codes a white-label switch, aby sa stalomandatory pre všetkých používateľov."
---

# Two-factor authentication (2FA)

Účty môžu byť chránené s **time-based one-time password (TOTP)** two-factor authentication na vrchu
hesla. Je to **opt-in** z user profilu podľa defaults, a white-label deployment môže urobiť
to **mandatory** pre všetkých. Akákoľvek RFC 6238 authenticator app funguje — Google Authenticator, Microsoft
Authenticator, Authy, Aegis, FreeOTP — pretože implementácia je standard (SHA-1, 6 digits, 30-second
step); žádna proprietárna server component je zapojaná.

## Ako to funguje

- **Doména.** MFA žije na `AppUser` aggregate (Access context). Používateľ je enrolled cez
  intention-revealing methods — `BeginMfaEnrollment`, `ConfirmMfaEnrollment`, `ConsumeBackupCode`,
  `RegenerateBackupCodes`, `DisableMfa` — takže invariants (secret musí byť confirmed pred tým, ako sa aktivuje;
  backup code je single-use) sú vynútené na jednom mieste.
- **TOTP.** Generácia a verification sú za Core `ITotpAuthenticator` interface, implemented v
  Infrastructure s **Otp.NET** knižnicou. Verification tolerates ±1 time-step clock skew.
- **Secret at rest.** Authenticator secret je uložený **encrypted** cez `ISecretProtector`
  (`EncryptionPurposes.MfaSecret`) — nikdy v plaintext.
- **Backup codes.** Desať single-use recovery codes sú issued na enrollment, shown **raz** a uložené len
  ako SHA-256 hashes (`MfaBackupCodes`). Každý funguje presne raz; spent code je odmietnutý potom.

## Povolenie (profil)

Na **Account** page (`/account`) *Two-factor authentication* sekcia ukazuje current status:

1. **Enable two-factor** otvára MudBlazor dialog s **QR code** (renderované server-side ako SVG cez
   `Net.Codecrete.QrCodeGenerator`) plus manual setup key.
2. Skenujte to, zadajte 6-digit code, aby ste potvrdili — to verificuje pending secret pred aktiváciou.
3. Dialog potom ukazuje **backup codes**; uložte ich. 2FA je teraz zapnutá.

Rovnaká sekcia umožní enrolled user **regenerate backup codes** alebo **turn off** 2FA — oboje vyžaduje
account heslo, aby ste potvrdili.

## Prihlasovanie s 2FA

Login je **two-step** flow keď je 2FA zapnutá:

1. **Password step** (`POST /api/auth/login`). Na success auth cookie nie je **yet** issued; namiesto toho short-lived (5-minute), encrypted *pending* cookie je nastavený a používateľ je poslaný na `/login/2fa`.
2. **Challenge step** (`POST /api/auth/login/verify-2fa`). Používateľ zadá TOTP code **alebo** akýkoľvek unused
   backup code. Na success pending cookie je dropped a real auth cookie je issued.

Zlyhané second-factor pokusy počítajú voči existujúcemu account **lockout** (`AuthLockout`) a auth
endpoints sú rate-limited.

## Mandatory 2FA pre white-label deployment

Regulovaný reseller môže vyžadovať 2FA pre **všetky** účty:

```jsonc
// appsettings / environment
"App": { "Branding": { "RequireMfa": true } }   // App__Branding__RequireMfa=true
```

Keď `RequireMfa` je zapnuté a používateľ bez 2FA sa prihlási, password step hlási
`mfaSetupRequired` a `MfaEnforcementMiddleware` presmeruje ich page navigácie na `/account`, kým
