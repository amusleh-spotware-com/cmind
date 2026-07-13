---
description: "Izbirna TOTP dvofaktorska avtentikacija z avtorizator aplikacije registracijo, enojne-vsote varnostne kopije kode in white-label stikalo da jo naredi obvezno za vse uporabnike."
---

# Dvofaktorska avtentikacija (2FA)

Računi so lahko zaščiteni s **časovno eno-tipskim geslom (TOTP)** dvofaktorsko avtentikacijo na vrh
gesla. Je **izbirna** z uporabnikovega profila privzeto, in white-label namestitev lahko naredi
obvezno za **vsakega**. Katerakoli RFC 6238 avtorizator aplikacija dela — Google Authenticator, Microsoft
Authenticator, Authy, Aegis, FreeOTP — ker je implementacija standardna (SHA-1, 6 števk, 30-sekundni korak); nobena proprietarna strežniška komponenta ni vključena.

## Kako deluje

- **Domena.** MFA živi na agregatu `AppUser` (Access context). Uporabnik je vpisan prek
  namenu razkrivajočih metod — `BeginMfaEnrollment`, `ConfirmMfaEnrollment`, `ConsumeBackupCode`,
  `RegenerateBackupCodes`, `DisableMfa` — torej invariante (skrivnost mora biti potrjena preden se aktivira;
  varnostna koda je enojna raba) so uveljavljene na enem mestu.
- **TOTP.** Generacija in verifikacija sedita za Core vmesnikom `ITotpAuthenticator`, implementiran v
  Infrastructure z **Otp.NET** knjižnico. Verifikacija tolerira ±1 časovni korak navideznega odstopanja.
- **Skrivnost pri miru.** Avtorizatorjeva skrivnost je shranjena **šifrirana** prek `ISecretProtector`
  (`EncryptionPurposes.MfaSecret`) — nikoli v navadnem besedilu.
- **Varnostne kode.** Deset enojnih-rabe kod za okrevanje je izdanih ob vpisu, prikazanih **enkrat**,
  in shranjene samo kot SHA-256 hash (`MfaBackupCodes`). Vsaka deluje natanko enkrat; porabljena koda je zavrnjena.

## Omogočanje (profil)

Na strani **Account** (`/account`) odsek *Dvofaktorska avtentikacija* prikazuje trenutno stanje:

1. **Omogoči dvofaktorsko** odpre MudBlazor dialog s **QR kodo** (upodobljeno strežniško stran kot SVG prek
   `Net.Codecrete.QrCodeGenerator`) plus ročni nastavitveni ključ.
2. Skeniraj jo, vnesi 6-števkčno kodo za potrditev — to preveri pendinge skrivnost preden aktiviramo.
3. Dialog nato prikaže **varnostne kode**; shrani jih. 2FA je zdaj vključeno.

Isti odsek omogoča vpisanemu uporabniku **regeneriraj varnostne kode** ali **izključi** 2FA — obe zahtevata
geslo računa za potrditev.

## Prijava z 2FA

Prijava je **dvo koraka** ko je 2FA omogočena:

1. **Korak gesla** (`POST /api/auth/login`). Ob uspehu avtentikacijski piškotek **še ni** izdan; namesto tega
   je nastavljen kratkoživi (5-min), šifrirani *pending* piškotek in uporabnik je poslan na `/login/2fa`.
2. **Korak izziva** (`POST /api/auth/login/verify-2fa`). Uporabnik vnese TOTP kodo **ali** katerokoli neuporabljeno
   varnostno kodo. Ob uspehu pending piškotek je izpuščen in resnični avtentikacijski piškotek izdan.

Neuspešni poskusi drugega faktorja štejejo k obstoječi računski **zaklenitvi** (`AuthLockout`), in avtentikacijske
končne točke so omejene glede na hitrost.

## Obvezna 2FA za white-label namestitev

Reguliran preprodajalec lahko zahteva 2FA za **vsak** račun:

```jsonc
// appsettings / okolje
"App": { "Branding": { "RequireMfa": true } }   // App__Branding__RequireMfa=true
```

Ko je `RequireMfa` vključeno in uporabnik brez 2FA se prijavi, korak gesla javi
`mfaSetupRequired` in `MfaEnforcementMiddleware` preusmeri njegove navigacije strani na `/account` dokler ne
konča vpisa. Privzeto je `false`, torej nenastavljena namestitev ohranja 2FA izbirno. Glej
[White-label](white-label.md).

## Končne točke

| Metoda in pot | Namen |
| --- | --- |
| `POST /api/auth/login` | Korak gesla; vrne `mfaRequired` (izziv) ali prijavi |
| `POST /api/auth/login/verify-2fa` | Drugi korak faktorja (TOTP ali varnostna koda) |
| `GET /api/auth/mfa/status` | `MfaEnabled`, pending, preostalo število varnostnih kod |
| `POST /api/auth/mfa/setup` | Začni vpis — vrne skrivnost, `otpauth://` URI, QR SVG |
| `POST /api/auth/mfa/confirm` | Potrdi kodo, aktiviraj, vrni varnostne kode |
| `POST /api/auth/mfa/disable` | Izključi (potrjeno z geslom) |
| `POST /api/auth/mfa/backup-codes/regenerate` | Izdaj sveži nabor (potrjeno z geslom) |

## Testi

- **Enote** — `UnitTests/Access/OtpNetTotpAuthenticatorTests.cs` (RFC 6238 vektorji),
  `AppUserMfaTests.cs` (vpis/prehod/enojna-raba invariance), `MfaBackupCodesTests.cs`.
- **Integracija** — `IntegrationTests/MfaPersistenceTests.cs` (vpis → potrditev → poraba, kaskadno brisanje)
  in `MfaFlowTests.cs` (poln HTTP dvo-korak login s TOTP + varnostno kodo, in obvezna-vpis vrata).
- **E2E** — `E2ETests/MfaFlowTests.cs`: omogoči iz profila (QR + potrditev + varnostne kode) in izpolni
  izzivano prijavo, na namizju in mobilnem viewportu.
