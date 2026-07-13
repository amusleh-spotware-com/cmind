---
description: "Opcionális TOTP kétfaktoros hitelesítés hitelesítő alkalmazás regisztrációval, egyszer használható biztonsági mentés kódok, és fehér címkés kapcsoló, hogy kötelezővé tegyük az összes felhasználó számára."
---

# Kétfaktoros hitelesítés (2FA)

A fiókok védettem lehetnek az **időalapú egyszeri jelszó (TOTP)** kétfaktoros hitelesítéssel a jelszó fölött. Ez **opcionális** a felhasználó profiljából alapértelmezés szerint, és egy fehér címkés telepítés **kötelezővé** tehetné az összes. Bármilyen RFC 6238 hitelesítő alkalmazás működik — Google Authenticator, Microsoft Authenticator, Authy, Aegis, FreeOTP — mivel az implementáció standard (SHA-1, 6 számjegy, 30-másodperces lépés); nincs szellemi szerv szerver-összetevő.

## Hogyan működik

- **Domain.** Az MFA az `AppUser` aggregátumon él (Access kontextus). A felhasználó regisztrálva van szándék-felhasználó módszereken keresztül — `BeginMfaEnrollment`, `ConfirmMfaEnrollment`, `ConsumeBackupCode`, `RegenerateBackupCodes`, `DisableMfa` — így az invariánsok (a titkos adatnak megerősített lennie kell az aktiválása előtt; egy biztonsági mentés kódja egyszeri) egyhelyen erőltetik.
- **TOTP.** Generálás és ellenőrzés az Core `ITotpAuthenticator` interfészen mögött, az Infrastructure-ben az **Otp.NET** könyvtárral végrehajtva. Az ellenőrzés tűri az ±1 időlépés óra-csúszást.
- **Titkos adatok nyugalomban.** Az hitelesítő titkos adat **titkosítottan** tárolódik az `ISecretProtector` segítségével (`EncryptionPurposes.MfaSecret`) — soha nem nyílt szövegben.
- **Biztonsági mentés kódok.** Tíz egyszeri helyreállítás kódja regisztrációkor kibocsátva, megmutatva **egyszer**, és tárolva csak SHA-256 titkosítottként (`MfaBackupCodes`). Mindegyik pontosan egyszer működik; egy költött kód ezt követően elutasított.

## Engedélyezés (profil)

A **Account** (Fiók) oldalon (`/account`) a *Kétfaktoros hitelesítés* szakasz megmutatja az aktuális státusz:

1. **Kétfaktoros engedélyezés** megnyit egy MudBlazor dialógust egy **QR kóddal** (szerver-oldali SVG-ként renderelve az `Net.Codecrete.QrCodeGenerator` segítségével) plusz a kézi beállítás kulcs.
2. Szkennelje be, írja be a 6-számjegyű kódot az megerősítéshez — ez ellenőrzi a függőben lévő titkos adatot az aktiválás előtt.
3. A dialógus ezután megmutatja a **biztonsági mentés kódok**; mente azokat. A 2FA már bekapcsolt.

Ugyanez a szakasz lehetővé teszi egy regisztrált felhasználó **regeneráló biztonsági mentés kódok** vagy **kikapcsolás** 2FA — mindkettő igényli a fiók jelszavát az megerősítéshez.

## Bejelentkezés a 2FA-val

A bejelentkezés egy **kétlépéses** áramlás amint 2FA engedélyezve van:

1. **Jelszó lépés** (`POST /api/auth/login`). Siker után az auth cookie **nem** kiadva még; helyette egy rövid élettartamú (5-perc), titkosított *függőben lévő* cookie be van állítva és a felhasználó küldve a `/login/2fa`-hoz.
2. **Challenge lépés** (`POST /api/auth/login/verify-2fa`). A felhasználó beír egy TOTP kódot **vagy** bármilyen fel nem használt biztonsági mentés kódot. Siker után a függőben lévő cookie dobódik és a valódi auth cookie kibocsátódik.

Sikertelen másodlagos tényezők száma számítódik a meglévő fiók **zárás** (`AuthLockout`) irányába, és az auth végpontok sebességkorlátozottak.

## Kötelező 2FA egy fehér címkés telepítéshez

Egy szabályozott viszonteladó követelheti a 2FA-t **minden** fiók számára:

```jsonc
// appsettings / environment
"App": { "Branding": { "RequireMfa": true } }   // App__Branding__RequireMfa=true
```

Amikor `RequireMfa` bekapcsolt és egy 2FA nélküli felhasználó bejelentkezik, a jelszó lépés jelenti `mfaSetupRequired` és `MfaEnforcementMiddleware` átirányít az oldalnavigáció `/account`-hoz amíg befejezik a regisztrációt. Ez alapértelmezésben `false`, így egy konfigurálva nem lévő telepítés megtartja a 2FA opcionálisnak. Lásd [White-label](white-label.md).

## Végpontok

| Módszer & útvonal | Cél |
| --- | --- |
| `POST /api/auth/login` | Jelszó lépés; visszatér `mfaRequired` (challenge) vagy bejelentkezik |
| `POST /api/auth/login/verify-2fa` | Másodlagos-faktor lépés (TOTP vagy biztonsági mentés kód) |
| `GET /api/auth/mfa/status` | `MfaEnabled`, függőben, fennmaradó biztonsági mentés-kód szám |
| `POST /api/auth/mfa/setup` | Regisztráció kezdete — visszatér titkos, `otpauth://` URI, QR SVG |
| `POST /api/auth/mfa/confirm` | Kódot megerősít, aktivál, biztonsági mentés kódokat ad vissza |
| `POST /api/auth/mfa/disable` | Kikapcsolás (jelszó-megerősített) |
| `POST /api/auth/mfa/backup-codes/regenerate` | Friss halmaz kibocsátása (jelszó-megerősített) |

## Tesztek

- **Egység** — `UnitTests/Access/OtpNetTotpAuthenticatorTests.cs` (RFC 6238 vektorok), `AppUserMfaTests.cs` (regisztráció/átmenet/egyszeri invariánsok), `MfaBackupCodesTests.cs`.
- **Integráció** — `IntegrationTests/MfaPersistenceTests.cs` (regisztráció → megerősítés → fogyaszt, kaskád törlés) és `MfaFlowTests.cs` (teljes HTTP kétlépéses bejelentkezés TOTP + biztonsági mentés kóddal, és a kötelező-regisztráció kapu).
- **E2E** — `E2ETests/MfaFlowTests.cs`: engedélyezés a profilból (QR + megerősítés + biztonsági mentés kódok) és teljes egy kihívott bejelentkezés, az asztali és mobil nézőpontok.
